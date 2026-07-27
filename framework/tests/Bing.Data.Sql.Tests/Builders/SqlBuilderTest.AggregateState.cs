using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL 生成器测试 - 聚合状态组合。
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 测试 - Clone 应保留 Raw 和可转换表达式聚合，并与源 Builder 隔离。
    /// </summary>
    [Fact]
    public void Clone_WhenRawAndExpressionAggregatesExist_ShouldPreserveAndIsolateState()
    {
        // Arrange
        const string sourceExpected = "Select Count(JsonExtract(o.Data, '$[0]')) As [JsonCount],Sum([o].[Amount] * [o].[Quantity]) As [Amount] \r\nFrom [Orders] As [o]";
        const string cloneExpected = "Select Count(JsonExtract(o.Data, '$[0]')) As [JsonCount],Sum([o].[Amount] * [o].[Quantity]) As [Amount],Count(*) As [Total] \r\nFrom [Orders] As [o]";
        _builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount] * [o].[Quantity]", "Amount")
            .From("Orders", "o");

        // Act
        var clone = _builder.Clone();
        clone.CountAll("Total");

        // Assert
        Assert.Equal(sourceExpected, _builder.ToSql());
        Assert.Equal(cloneExpected, clone.ToSql());
    }

    /// <summary>
    /// 测试 - New 创建的 Builder 不应共享聚合列状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceHasAggregate_ShouldNotShareAggregateState()
    {
        // Arrange
        _builder.CountColumn("u.Id", "Users", distinct: true).From("Users", "u");

        // Act
        var result = _builder.New().CountAll("Total").From("Orders").ToSql();

        // Assert
        Assert.Equal("Select Count(Distinct [u].[Id]) As [Users] \r\nFrom [Users] As [u]", _builder.ToSql());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", result);
    }

    /// <summary>
    /// 测试 - 聚合 CTE 应在主查询前渲染并保持聚合参数 Distinct。
    /// </summary>
    [Fact]
    public void Cte_WhenDistinctAggregateIsConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var expected = new StringBuilder();
        expected.AppendLine("With [active_users] ");
        expected.AppendLine("As (Select Count(Distinct [u].[Id]) As [UserCount] ");
        expected.AppendLine("From [Users] As [u])");
        expected.AppendLine("Select Sum([a].[Amount]) As [Total] ");
        expected.Append("From [active_users] As [a]");
        var cte = _builder.New().CountColumn("u.Id", "UserCount", distinct: true).From("Users", "u");

        // Act
        var sql = _builder.Sum("a.Amount", "Total").From("active_users", "a").With("active_users", cte).ToSql();

        // Assert
        Assert.Equal(expected.ToString(), sql);
    }

    /// <summary>
    /// 测试 - 含 Distinct 聚合的 Union 应保留各分支 SQL 和参数。
    /// </summary>
    [Fact]
    public void Union_WhenDistinctAggregateIsConfigured_ShouldMergeCorrectly()
    {
        // Arrange
        var expected = new StringBuilder();
        expected.AppendLine("(Select Count(Distinct [u].[Id]) As [UserCount] ");
        expected.AppendLine("From [Users] As [u] ");
        expected.AppendLine("Where [u].[Enabled]=@_p_0 ");
        expected.AppendLine(") ");
        expected.AppendLine("Union ");
        expected.AppendLine("(Select Sum([a].[Amount] * 2) As [Total] ");
        expected.AppendLine("From [ArchivedUsers] As [a] ");
        expected.AppendLine("Where [a].[Enabled]=@_p_1 ");
        expected.Append(")");
        var union = _builder.New().AggregateExpression(SqlAggregateFunction.Sum, "[a].[Amount] * 2", "Total")
            .From("ArchivedUsers", "a")
            .Where("a.Enabled", false);

        // Act
        var sql = _builder.CountColumn("u.Id", "UserCount", distinct: true).From("Users", "u")
            .Where("u.Enabled", true)
            .Union(union)
            .ToSql();

        // Assert
        Assert.Equal(expected.ToString(), sql);
        Assert.Equal(true, _builder.GetParam("@_p_0"));
        Assert.Equal(false, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - 含可转换聚合表达式的子查询应合并参数并保持 SQL 稳定。
    /// </summary>
    [Fact]
    public void Subquery_WhenAggregateExpressionHasParameters_ShouldMergeParametersAndRemainStable()
    {
        // Arrange
        const string expected = "Select (Select Sum([a].[Amount] * 2) As [Total] \r\nFrom [Audit] As [a] \r\nWhere [a].[Enabled]=@_p_0) As [AuditTotal] \r\nFrom [Users] \r\nWhere [Enabled]=@_p_1";
        var subquery = _builder.New().AggregateExpression(SqlAggregateFunction.Sum, "[a].[Amount] * 2", "Total")
            .From("Audit", "a")
            .Where("a.Enabled", true);
        _builder.Select(subquery, "AuditTotal").From("Users").Where("Enabled", false);

        // Act
        var firstSql = _builder.ToSql();
        var secondSql = _builder.ToSql();

        // Assert
        Assert.Equal(expected, firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(true, _builder.GetParam("@_p_0"));
        Assert.Equal(false, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - 含聚合表达式的 AppendTo 重复调用应保持输出稳定且不清空现有内容。
    /// </summary>
    [Fact]
    public void AppendTo_WhenAggregateExpressionIsConfigured_ShouldRemainStableAcrossRepeatedCalls()
    {
        // Arrange
        var result = new StringBuilder("Prefix:");
        _builder.AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount] * 2", "Total")
            .From("Orders", "o");
        var sql = _builder.ToSql();

        // Act
        _builder.AppendTo(result);
        _builder.AppendTo(result);

        // Assert
        Assert.Equal($"Prefix:{sql}{sql}", result.ToString());
        Assert.Equal(sql, _builder.ToSql());
    }

    /// <summary>
    /// 测试 - Raw 和可转换表达式聚合应通过 AddParam 显式绑定参数并保持调试 SQL 稳定。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenParametersAreExplicit_ShouldBindAndRenderStably()
    {
        // Arrange
        const string expected = "Select Count(Case When o.Amount>@Min Then 1 End) As [RawCount],Sum(Case When [o].[Amount]>@MinAmount Then [o].[Amount] Else 0 End) As [Total] \r\nFrom [Orders] As [o]";
        const string expectedDebugSql = "Select Count(Case When o.Amount>10 Then 1 End) As [RawCount],Sum(Case When [o].[Amount]>100 Then [o].[Amount] Else 0 End) As [Total] \r\nFrom [Orders] As [o]";
        var builder = new TestSqlBuilder()
            .AggregateRaw(SqlAggregateFunction.Count, "Case When o.Amount>@Min Then 1 End", "RawCount")
            .AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [o].[Amount]>@MinAmount Then [o].[Amount] Else 0 End", "Total")
            .AddParam("Min", 10)
            .AddParam("MinAmount", 100)
            .From("Orders", "o");

        // Act
        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        // Assert
        Assert.Equal(expected, firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@Min", "@MinAmount" }, builder.GetParams().Keys);
        Assert.Equal(10, builder.GetParam("Min"));
        Assert.Equal(100, builder.GetParam("MinAmount"));
        Assert.Equal(expectedDebugSql, builder.ToDebugSql(firstSql));
    }

    /// <summary>
    /// 测试 - 聚合 CTE 的同名显式参数应重命名子查询参数且保持外层参数。
    /// </summary>
    [Fact]
    public void Cte_WhenAggregateExpressionParametersConflict_ShouldRenameCteParameter()
    {
        // Arrange
        const string expected = "With [active_orders] \r\nAs (Select Count(Case When [o].[Amount]>@_p_0 Then 1 End) As [HighCount] \r\nFrom [Orders] As [o])\r\nSelect Sum(Case When [a].[Amount]>@Min Then [a].[Amount] Else 0 End) As [Total] \r\nFrom [active_orders] As [a]";
        var cte = new TestSqlBuilder()
            .AggregateExpression(SqlAggregateFunction.Count,
                "Case When [o].[Amount]>@Min Then 1 End", "HighCount")
            .AddParam("Min", 200)
            .From("Orders", "o");

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [a].[Amount]>@Min Then [a].[Amount] Else 0 End", "Total")
            .AddParam("Min", 100)
            .From("active_orders", "a")
            .With("active_orders", cte)
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(new[] { "@Min", "@_p_0" }, _builder.GetParams().Keys);
        Assert.Equal(100, _builder.GetParam("@Min"));
        Assert.Equal(200, _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 聚合 Union 的前缀相近参数应只重命名冲突参数。
    /// </summary>
    [Fact]
    public void Union_WhenAggregateExpressionParametersHavePrefixNames_ShouldKeepTokenBoundaries()
    {
        // Arrange
        const string expected = "(Select Sum(Case When [o].[Amount]>@p Then [o].[Amount] Else 0 End) As [Total] \r\nFrom [Orders] As [o] \r\n) \r\nUnion \r\n(Select Sum(Case When [a].[Amount]>@_p_0 And [a].[Amount]>@p1 And [a].[Amount]>@p10 Then [a].[Amount] Else 0 End) As [Total] \r\nFrom [Archive] As [a] \r\n)";
        var union = new TestSqlBuilder()
            .AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [a].[Amount]>@p And [a].[Amount]>@p1 And [a].[Amount]>@p10 Then [a].[Amount] Else 0 End",
                "Total")
            .AddParam("p", 20)
            .AddParam("p1", 21)
            .AddParam("p10", 210)
            .From("Archive", "a");

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [o].[Amount]>@p Then [o].[Amount] Else 0 End", "Total")
            .AddParam("p", 10)
            .From("Orders", "o")
            .Union(union)
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(new[] { "@p", "@_p_0", "@p1", "@p10" }, _builder.GetParams().Keys);
        Assert.Equal(10, _builder.GetParam("@p"));
        Assert.Equal(20, _builder.GetParam("@_p_0"));
        Assert.Equal(21, _builder.GetParam("@p1"));
        Assert.Equal(210, _builder.GetParam("@p10"));
    }

    /// <summary>
    /// 测试 - 聚合子查询的 @Min 与 @MinAmount 冲突应分别重命名且重复渲染稳定。
    /// </summary>
    [Fact]
    public void Subquery_WhenAggregateExpressionParametersConflict_ShouldRenameEachExactToken()
    {
        // Arrange
        const string expected = "Select (Select Sum(Case When [a].[Amount]>@_p_0 And [a].[Amount]>@_p_1 Then [a].[Amount] Else 0 End) As [Total] \r\nFrom [Audit] As [a]) As [AuditTotal] \r\nFrom [Orders] As [o] \r\nWhere [o].[Amount]>@Min And [o].[Amount]>@MinAmount";
        var subquery = new TestSqlBuilder()
            .AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [a].[Amount]>@Min And [a].[Amount]>@MinAmount Then [a].[Amount] Else 0 End", "Total")
            .AddParam("Min", 200)
            .AddParam("MinAmount", 300)
            .From("Audit", "a");

        // Act
        _builder.AddParam("Min", 100)
            .AddParam("MinAmount", 150)
            .Select(subquery, "AuditTotal")
            .From("Orders", "o")
            .AppendWhere("[o].[Amount]>@Min And [o].[Amount]>@MinAmount");
        var firstSql = _builder.ToSql();
        var secondSql = _builder.ToSql();

        // Assert
        Assert.Equal(expected, firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@Min", "@MinAmount", "@_p_0", "@_p_1" }, _builder.GetParams().Keys);
        Assert.Equal(100, _builder.GetParam("@Min"));
        Assert.Equal(150, _builder.GetParam("@MinAmount"));
        Assert.Equal(200, _builder.GetParam("@_p_0"));
        Assert.Equal(300, _builder.GetParam("@_p_1"));
    }
}