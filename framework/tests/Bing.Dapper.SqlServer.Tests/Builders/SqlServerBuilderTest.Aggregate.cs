using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQL Server SQL 生成器聚合测试。
/// </summary>
public class SqlServerBuilderAggregateTest
{
    /// <summary>
    /// 测试 - SQL Server Distinct 聚合应在函数参数内使用方括号限定列。
    /// </summary>
    [Fact]
    public void Aggregate_WhenDistinctQualifiedColumnsAreConfigured_ShouldRenderSqlServerSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct [o].[UserId]) As [UserCount],Sum(Distinct [o].[Amount]) As [Amount],Avg(Distinct [o].[Amount]) As [Average],Max(Distinct [o].[Amount]) As [Maximum],Min(Distinct [o].[Amount]) As [Minimum] \r\nFrom [dbo].[Orders] As [o]";
        var builder = new SqlServerBuilder();

        // Act
        var sql = builder.Count("o.UserId", "UserCount", distinct: true)
            .Sum("o.Amount", "Amount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("dbo.Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - SQL Server 原始聚合应保留 JSON Path，表达式聚合应保留方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenConfigured_ShouldRenderSqlServerSql()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) As [JsonCount],Sum([o].[Quantity] * [o].[Price]) As [Total] \r\nFrom [dbo].[Orders] As [o]";
        var builder = new SqlServerBuilder();

        // Act
        var sql = builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "Total")
            .From("dbo.Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - SQL Server 非 Count 聚合使用通配符时应抛出明确异常。
    /// </summary>
    [Fact]
    public void Aggregate_WhenNonCountFunctionUsesWildcard_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new SqlServerBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Avg("*", "Average"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [dbo].[Orders]", builder.Count(alias: "Total").From("dbo.Orders").ToSql());
    }

    /// <summary>
    /// 测试 - SQL Server 可转换表达式应保留字符串与注释，结构化聚合应支持转义引用标识符。
    /// </summary>
    [Fact]
    public void AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRenderSqlServerSql()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */),Max([Sales Order].[Order]]Name]) As [Escaped] \r\nFrom [dbo].[Orders] As [o]";
        var builder = new SqlServerBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */")
            .Aggregate(SqlAggregateFunction.Max, "[Sales Order].[Order]]Name]", "Escaped")
            .From("dbo.Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}