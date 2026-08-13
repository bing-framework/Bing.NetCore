using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// Lambda 参数表源绑定范围测试。
/// </summary>
public class SqlParameterBindingScopeTest
{
    /// <summary>
    /// 测试目的：同一实体类型的两个 Lambda 参数应按参数位置绑定到不同表源实例。
    /// </summary>
    [Fact]
    public void GetSource_WhenSameEntityParametersProvided_ShouldResolveByParameterPosition()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("owner");
        clause.AppendRoot<Sample>("reviewer");
        Expression<Func<Sample, Sample, bool>> expression =
            (owner, reviewer) => owner.TestValue == reviewer.TestValue;
        var scope = new SqlParameterBindingScope(expression, clause.Sources);
        var comparison = (BinaryExpression)expression.Body;

        // Act
        var left = scope.GetSource(comparison.Left);
        var right = scope.GetSource(comparison.Right);

        // Assert
        Assert.Equal("source_0", left.SourceId);
        Assert.Equal("source_1", right.SourceId);
        Assert.NotSame(left, right);
    }

    /// <summary>
    /// 测试目的：同类型多表比较条件应使用 Lambda 参数各自绑定的来源别名生成完整 SQL。
    /// </summary>
    [Fact]
    public void ResolveMultiSourcePredicate_WhenSameEntityColumnsCompared_ShouldUseBoundSourceAliases()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("owner");
        clause.AppendRoot<Sample>("reviewer");
        Expression<Func<Sample, Sample, bool>> expression =
            (owner, reviewer) => owner.TestValue == reviewer.TestValue;

        // Act
        var condition = clause.ResolveMultiSourcePredicate(expression);

        // Assert
        Assert.Equal("[owner].[TestValue]=[reviewer].[TestValue]", condition.GetCondition());
    }

    /// <summary>
    /// 测试目的：不支持的多表二元运算符必须在解析常量参数前失败，避免失败调用污染后续查询参数。
    /// </summary>
    [Fact]
    public void ResolveMultiSourcePredicate_WhenBinaryOperatorIsUnsupported_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var parameterManager = new ParameterManager(TestDialect.Instance);
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(parameterManager: parameterManager));
        clause.From<Sample>("sample");
        Expression<Func<Sample, bool>> expression = sample => sample.BoolValue ^ true;

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => clause.ResolveMultiSourcePredicate(expression));

        // Assert
        Assert.Equal("不支持的多表谓词运算符: ExclusiveOr。", exception.Message);
        Assert.Empty(parameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：组合多表谓词的后续分支解析失败时，已解析分支也不得向查询参数留下副作用。
    /// </summary>
    [Fact]
    public void ResolveMultiSourcePredicate_WhenLaterCombinedBranchIsUnsupported_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var parameterManager = new ParameterManager(TestDialect.Instance);
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(parameterManager: parameterManager));
        clause.From<Sample>("sample");
        Expression<Func<Sample, bool>> expression = sample => sample.IntValue == 1 && (sample.BoolValue ^ true);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => clause.ResolveMultiSourcePredicate(expression));

        // Assert
        Assert.Equal("不支持的多表谓词运算符: ExclusiveOr。", exception.Message);
        Assert.Empty(parameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：Lambda 参数类型与表源实体类型不一致时，应在建立绑定时拒绝该表达式。
    /// </summary>
    [Fact]
    public void Constructor_WhenParameterTypeDiffersFromSourceEntity_ShouldThrowArgumentException()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("s");
        Expression<Func<Sample2, bool>> expression = sample => sample.IntValue > 0;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new SqlParameterBindingScope(expression, clause.Sources));

        // Assert
        Assert.Equal("Lambda 参数类型必须与对应表源实体类型一致。 (Parameter 'sources')", exception.Message);
    }

    /// <summary>
    /// 测试目的：闭包或常量表达式未引用当前 Lambda 参数时，解析表源必须显式失败。
    /// </summary>
    [Fact]
    public void GetSource_WhenExpressionDoesNotReferenceBoundParameter_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        clause.From<Sample>("s");
        Expression<Func<Sample, bool>> expression = sample => sample.BoolValue;
        var scope = new SqlParameterBindingScope(expression, clause.Sources);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => scope.GetSource(Expression.Constant(1)));

        // Assert
        Assert.Equal("表达式未绑定到当前查询的表源实例。", exception.Message);
    }
}