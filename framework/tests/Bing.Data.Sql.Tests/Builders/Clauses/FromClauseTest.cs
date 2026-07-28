using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// From子句测试
/// </summary>
public class FromClauseTest
{
    /// <summary>
    /// From子句
    /// </summary>
    private FromClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public FromClauseTest()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns></returns>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    /// <summary>
    /// 测试目的：验证未设置来源时不应输出 From 子句。
    /// </summary>
    [Fact]
    public void ToSql_WhenSourceIsMissing_ShouldReturnNull()
    {
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试目的：验证设置表名后应输出带方言引号的 From 子句。
    /// </summary>
    [Fact]
    public void From_WhenTableProvided_ShouldRenderQuotedTable()
    {
        _clause.From("a");
        Assert.Equal("From [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证设置表名和别名后应输出格式化的别名。
    /// </summary>
    [Fact]
    public void From_WhenTableAndAliasProvided_ShouldRenderQuotedAlias()
    {
        _clause.From("a", "b");
        Assert.Equal("From [a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：独立 schema 应由结构化表引用格式化。
    /// </summary>
    [Fact]
    public void From_WhenSchemaQualifiedTableProvided_ShouldRenderStructuredReference()
    {
        _clause.From("c.a", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串中的别名应按既有规则解析。
    /// </summary>
    [Fact]
    public void From_WhenEmbeddedAliasProvided_ShouldParseAlias()
    {
        _clause.From("a.b as t");
        Assert.Equal("From [a].[b] As [t]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含 SQL 语句分隔符时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsStatementSeparator_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a;DropTable"));
    }

    /// <summary>
    /// 测试目的：SQL Server 多段字符串表名应按既有规则拆分。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsMultipleDots_ShouldFormatAsAtomicIdentifier()
    {
        _clause.From("profile.api.Event");
        Assert.Equal("From [profile].[api].[Event]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含函数结构时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsFunctionStructure_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a(b)"));
    }

    /// <summary>
    /// 测试目的：复合预加引号限定名必须被拒绝，调用方应改用独立 schema 参数。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsQualifiedQuotedIdentifier_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("[archive].[Order.Log2025]"));
    }

    /// <summary>
    /// 测试目的：字符串内的别名与显式别名冲突时应被拒绝，避免隐式覆盖。
    /// </summary>
    [Fact]
    public void From_WhenEmbeddedAliasConflictsWithExplicitAlias_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _clause.From("Order.Log2025 as log", "other"));
    }

    /// <summary>
    /// 测试目的：AppendFrom 原始 SQL 表达式应绕过结构化表名解析。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveSqlExpression()
    {
        _clause.AppendSql("(Select 1) As source");

        Assert.Equal("From (Select 1) As source", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体应解析为对应表名。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityProvided_ShouldRenderEntityTable()
    {
        _clause.From<Sample>();
        Assert.Equal("From [Sample]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体和别名应输出格式化 From 子句。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityAndAliasProvided_ShouldRenderAlias()
    {
        _clause.From<Sample>("a");
        Assert.Equal("From [Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证泛型实体、别名和架构应按约定顺序输出。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntityAliasAndSchemaProvided_ShouldRenderStructuredReference()
    {
        _clause.From<Sample>("a", "b");
        Assert.Equal("From [b].[Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证重复设置实体来源时最后一次设置应覆盖前一次。
    /// </summary>
    [Fact]
    public void From_WhenGenericEntitySetRepeatedly_ShouldUseLatestSource()
    {
        _clause.From<Sample>("a");
        _clause.From<Sample>("b");
        Assert.Equal("From [Sample] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：验证原始 SQL 表表达式应保持调用方提供的内容。
    /// </summary>
    [Fact]
    public void AppendSql_WhenRawExpressionProvided_ShouldPreserveExpression()
    {
        _clause.AppendSql("a.b as c");
        Assert.Equal("From a.b as c", GetSql());
    }

    /// <summary>
    /// 测试目的：验证来源与原始表达式混合设置时最后一次设置应生效。
    /// </summary>
    [Fact]
    public void From_WhenSourceAndRawExpressionSetRepeatedly_ShouldUseLatestExpression()
    {
        _clause.From<Sample>("a");
        _clause.AppendSql("b");
        _clause.From<Sample>("c");
        _clause.AppendSql("d");
        Assert.Equal("From d", GetSql());
    }

    /// <summary>
    /// 测试目的：验证连续追加原始表表达式应保留追加顺序。
    /// </summary>
    [Fact]
    public void AppendSql_WhenCalledRepeatedly_ShouldAppendExpressions()
    {
        _clause.AppendSql("a");
        _clause.AppendSql("b");
        Assert.Equal("From ab", GetSql());
    }

    /// <summary>
    /// 测试目的：验证自定义实体解析器应决定实体的架构和表名。
    /// </summary>
    [Fact]
    public void From_WhenCustomEntityResolverProvided_ShouldUseResolvedTable()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
        _clause.From<Sample>();
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample]", result);
    }

    /// <summary>
    /// 测试目的：验证自定义实体解析器与别名应共同输出格式化来源。
    /// </summary>
    [Fact]
    public void From_WhenCustomEntityResolverAndAliasProvided_ShouldUseResolvedTableAndAlias()
    {
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
        _clause.From<Sample>("a");
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample] As [a]", result);
    }

    /// <summary>
    /// 测试目的：验证克隆后应保留来源且与原子句保持独立。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceConfigured_ShouldPreserveSourceAndRemainIndependent()
    {
        _clause.From("a", "b");
        var copy = _clause.Clone(TestSqlBuilder.CreateTestClauseContext());
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [a] As [b]", copy.ToSql());

        copy.From("c", "d");
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [c] As [d]", copy.ToSql());
    }
}
