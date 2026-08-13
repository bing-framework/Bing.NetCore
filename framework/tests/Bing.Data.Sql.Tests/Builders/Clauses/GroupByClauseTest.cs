using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// Group By子句测试
/// </summary>
public class GroupByClauseTest
{
    /// <summary>
    /// 分组子句
    /// </summary>
    private GroupByClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public GroupByClauseTest()
    {
        _clause = new GroupByClause(TestSqlBuilder.CreateTestClauseContext());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    /// <summary>
    /// 默认输出空
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试分组
    /// </summary>
    [Fact]
    public void Test_GroupBy_1()
    {
        _clause.GroupBy("a");
        Assert.Equal("Group By [a]", GetSql());
    }

    /// <summary>
    /// 测试分组
    /// </summary>
    [Fact]
    public void Test_GroupBy_2()
    {
        _clause.GroupBy("a.B,c.D");
        Assert.Equal("Group By [a].[B],[c].[D]", GetSql());
    }

    /// <summary>
    /// 测试分组 - 验证分组字段为空
    /// </summary>
    [Fact]
    public void Test_GroupBy_3()
    {
        _clause.GroupBy("");
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试分组 - 分组条件
    /// </summary>
    [Fact]
    public void Test_GroupBy_4()
    {
        _clause.GroupBy("a");
        _clause.HavingRaw("b");
        Assert.Equal("Group By [a] Having b", GetSql());
    }

    /// <summary>
    /// 测试分组 - lambda
    /// </summary>
    [Fact]
    public void Test_GroupBy_5()
    {
        _clause.GroupBy<Sample>(t => t.Email);
        _clause.HavingRaw("b");
        Assert.Equal("Group By [Email] Having b", GetSql());
    }

    /// <summary>
    /// 测试分组 - 别名
    /// </summary>
    [Fact]
    public void Test_GroupBy_6()
    {
        _clause = new GroupByClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver(), aliasRegister: new TestEntityAliasRegister()));
        _clause.GroupBy<Sample>(t => t.Email);
        _clause.HavingRaw("b");
        Assert.Equal("Group By [as_Sample].[t_Email] Having b", GetSql());
    }

    /// <summary>
    /// 测试分组 - 多个GroupBy
    /// </summary>
    [Fact]
    public void Test_GroupBy_7()
    {
        _clause.GroupBy("a");
        _clause.HavingRaw("b");
        _clause.GroupBy<Sample>(t => t.Email);
        _clause.HavingRaw("c");
        Assert.Equal("Group By [a],[Email] Having c", GetSql());
    }

    /// <summary>
    /// 测试分组 - Append
    /// </summary>
    [Fact]
    public void Test_GroupBy_8()
    {
        _clause.GroupBy("a");
        _clause.GroupBy("b");
        _clause.AppendSql("c");
        _clause.AppendSql("d");
        Assert.Equal("Group By [a],[b],c,d", GetSql());
    }

    /// <summary>
    /// 测试目的：显式 HavingRaw 应替换旧条件并保留原始聚合表达式。
    /// </summary>
    [Fact]
    public void HavingRaw_WhenGroupConfigured_ShouldReplaceHavingCondition()
    {
        // Arrange
        _clause.GroupBy("a");
        _clause.HavingRaw("Count(*) > 1");

        // Act
        _clause.HavingRaw("Sum([Amount]) >= @minimum");

        // Assert
        Assert.Equal("Group By [a] Having Sum([Amount]) >= @minimum", GetSql());
    }

    /// <summary>
    /// 测试目的：Having 应解析方括号标识符，而 HavingRaw 必须保持完全原样。
    /// </summary>
    [Fact]
    public void Having_WhenIdentifierSyntaxProvided_ShouldResolveDialectButRawShouldNot()
    {
        // Arrange
        _clause.GroupBy("a");

        // Act
        _clause.Having("Count([a]) >= @minimum");
        var resolved = GetSql();
        _clause.HavingRaw("Count([raw]) >= @minimum");
        var raw = GetSql();

        // Assert
        Assert.Equal("Group By [a] Having Count([a]) >= @minimum", resolved);
        Assert.Equal("Group By [a] Having Count([raw]) >= @minimum", raw);
    }

    /// <summary>
    /// 测试目的：Group By 子句的列格式化失败时，不得向调用方缓冲区遗留关键字前缀。
    /// </summary>
    [Fact]
    public void AppendTo_WhenGroupColumnIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        _clause.GroupBy("Name,invalid;");
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => _clause.AppendTo(result));

        // Assert
        Assert.Equal("name", exception.ParamName);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：多个类型化分组列的后续解析失败时，不得保留前序分组项或将统一 Builder 切换为查询状态。
    /// </summary>
    [Fact]
    public void GroupBy_WhenLaterTypedColumnResolutionFails_ShouldKeepClauseAndBuilderStateUnchanged()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        _clause = new GroupByClause(TestSqlBuilder.CreateTestClauseContext(builder: builder,
            entityResolver: new FailingAfterFirstEntityResolver()));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _clause.GroupBy<Sample>(
            sample => sample.Email,
            sample => sample.Url));

        // Assert
        Assert.Equal("Group column resolution failed.", exception.Message);
        Assert.False(_clause.IsGroup);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        builder.DeleteFrom(new SqlTableReference { TableName = "samples" }).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 在第二次实体列解析时失败的测试解析器。
    /// </summary>
    private sealed class FailingAfterFirstEntityResolver : IEntityResolver
    {
        /// <summary>
        /// 内部基础解析器。
        /// </summary>
        private readonly TestEntityResolver _inner = new();

        /// <summary>
        /// 已执行的类型化列解析次数。
        /// </summary>
        private int _typedColumnResolutionCount;

        /// <inheritdoc />
        public string GetTable(Type entity) => _inner.GetTable(entity);

        /// <inheritdoc />
        public string GetSchema(Type entity) => _inner.GetSchema(entity);

        /// <inheritdoc />
        public string GetColumns<TEntity>(bool propertyAsAlias) => _inner.GetColumns<TEntity>(propertyAsAlias);

        /// <inheritdoc />
        public string GetColumns<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, object[]>> columns,
            bool propertyAsAlias) => _inner.GetColumns(columns, propertyAsAlias);

        /// <inheritdoc />
        public string GetColumn<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, object>> column)
        {
            if (++_typedColumnResolutionCount == 2)
                throw new InvalidOperationException("Group column resolution failed.");
            return _inner.GetColumn(column);
        }

        /// <inheritdoc />
        public string GetColumn(System.Linq.Expressions.Expression expression, Type entity, bool right = false) =>
            _inner.GetColumn(expression, entity, right);

        /// <inheritdoc />
        public Type GetType(System.Linq.Expressions.Expression expression, bool right = false) =>
            _inner.GetType(expression, right);
    }
}
