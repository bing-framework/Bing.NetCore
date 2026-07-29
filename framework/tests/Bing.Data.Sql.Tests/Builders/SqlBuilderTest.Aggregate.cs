using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL 生成器测试 - 统一聚合。
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 测试 - Count 通配符应作为结构化聚合参数渲染。
    /// </summary>
    [Fact]
    public void Count_WhenWildcardIsConfigured_ShouldRenderCountWildcard()
    {
        // Arrange
        const string expected = "Select Count(*) As [Total] \r\nFrom [Users]";

        // Act
        var sql = _builder.Count(alias: "Total").From("Users").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 查询级 Distinct 与聚合参数 Distinct 应分别渲染到各自位置。
    /// </summary>
    [Fact]
    public void Distinct_WhenQueryAndAggregateDistinctAreConfigured_ShouldNotMixSemantics()
    {
        // Arrange
        const string queryDistinctExpected = "Select Distinct Count([u].[Id]) As [Count] \r\nFrom [Users] As [u]";
        const string aggregateDistinctExpected = "Select Count(Distinct [u].[Id]) As [Count] \r\nFrom [Users] As [u]";
        const string bothDistinctExpected = "Select Distinct Count(Distinct [u].[Id]) As [Count] \r\nFrom [Users] As [u]";

        // Act
        var queryDistinctSql = new TestSqlBuilder().Distinct().Count("u.Id", "Count").From("Users", "u").ToSql();
        var aggregateDistinctSql = new TestSqlBuilder().Count("u.Id", "Count", distinct: true).From("Users", "u").ToSql();
        var bothDistinctSql = new TestSqlBuilder().Distinct().Count("u.Id", "Count", distinct: true)
            .From("Users", "u").ToSql();

        // Assert
        Assert.Equal(queryDistinctExpected, queryDistinctSql);
        Assert.Equal(aggregateDistinctExpected, aggregateDistinctSql);
        Assert.Equal(bothDistinctExpected, bothDistinctSql);
    }

    /// <summary>
    /// 测试 - 所有标准聚合函数应委托统一结构化模型并支持参数级 Distinct。
    /// </summary>
    [Fact]
    public void Aggregate_WhenStandardFunctionsAreMixed_ShouldRenderCompleteSql()
    {
        // Arrange
        const string expected = "Select Count([u].[Id]) As [Count],Sum(Distinct [o].[Amount]) As [DistinctAmount],Avg(Distinct [o].[Amount]) As [Average],Max(Distinct [o].[Amount]) As [Maximum],Min(Distinct [o].[Amount]) As [Minimum] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.Count("u.Id", "Count")
            .Sum("o.Amount", "DistinctAmount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Lambda 聚合应使用实体列映射并保留表别名。
    /// </summary>
    [Fact]
    public void Aggregate_WhenLambdaUsesDistinct_ShouldResolveMappedQualifiedColumn()
    {
        // Arrange
        const string expected = "Select Sum(Distinct [s].[DoubleValue]) As [Total] \r\nFrom [Sample] As [s]";

        // Act
        var sql = _builder.Aggregate<Sample>(SqlAggregateFunction.Sum, item => item.DoubleValue, "Total",
                distinct: true)
            .From<Sample>("s")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式应转换方括号标识符且保留表达式结构。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenExpressionIsConfigured_ShouldPreserveExpression()
    {
        // Arrange
        const string expected = "Select Sum([o].[Quantity] * [o].[Price]) As [TotalAmount],Count(Distinct Case When [o].[Enabled]=1 Then [o].[UserId] End) As [EnabledUsers] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "TotalAmount")
            .AggregateExpression(SqlAggregateFunction.Count, "Case When [o].[Enabled]=1 Then [o].[UserId] End",
                "EnabledUsers", distinct: true)
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Count 的 Distinct 通配符参数应被拒绝，避免生成不可移植 SQL。
    /// </summary>
    [Fact]
    public void Count_WhenDistinctWildcardIsRequested_ShouldThrowArgumentException()
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Count("*", "Total", distinct: true));

        // Assert
        Assert.Equal("distinct", exception.ParamName);
    }

    /// <summary>
    /// 测试 - 聚合状态在克隆后应独立保留。
    /// </summary>
    [Fact]
    public void Clone_WhenDistinctAggregateIsConfigured_ShouldPreserveAndIsolateAggregationState()
    {
        // Arrange
        const string sourceExpected = "Select Count(Distinct [u].[Id]) As [Users] \r\nFrom [Users] As [u]";
        const string cloneExpected = "Select Count(Distinct [u].[Id]) As [Users],Sum([u].[Amount]) As [Total] \r\nFrom [Users] As [u]";
        _builder.Count("u.Id", "Users", distinct: true).From("Users", "u");

        // Act
        var clone = _builder.Clone();
        clone.Sum("u.Amount", "Total");

        // Assert
        Assert.Equal(sourceExpected, _builder.ToSql());
        Assert.Equal(cloneExpected, clone.ToSql());
    }

    /// <summary>
    /// 测试 - ClearSelect 应清除查询级 Distinct 和所有聚合状态。
    /// </summary>
    [Fact]
    public void ClearSelect_WhenDistinctAggregateExists_ShouldRemoveAllSelectState()
    {
        // Arrange
        const string expected = "Select Sum(Distinct [u].[Amount]) As [Amount] \r\nFrom [Users] As [u]";
        _builder.Distinct().Count("u.Id", "Users", distinct: true).From("Users", "u");

        // Act
        var sql = _builder.ClearSelect().Sum("u.Amount", "Amount", distinct: true).ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}