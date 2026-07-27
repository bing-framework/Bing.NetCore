using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL SQL 生成器聚合测试。
/// </summary>
public class PostgreSqlBuilderAggregateTest
{
    /// <summary>
    /// 测试 - PostgreSQL Distinct 聚合应在函数参数内引用每个限定列名称段。
    /// </summary>
    [Fact]
    public void Aggregate_WhenDistinctQualifiedColumnsAreConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct \"o\".\"UserId\") As \"UserCount\",Sum(Distinct \"o\".\"Amount\") As \"Amount\",Avg(Distinct \"o\".\"Amount\") As \"Average\",Max(Distinct \"o\".\"Amount\") As \"Maximum\",Min(Distinct \"o\".\"Amount\") As \"Minimum\" \r\nFrom \"public\".\"orders\" As \"o\"";
        var builder = new PostgreSqlBuilder();

        // Act
        var sql = builder.Count("o.UserId", "UserCount", distinct: true)
            .Sum("o.Amount", "Amount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("public.orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - PostgreSQL 原始聚合不应改写 JSON Path，表达式聚合应转换方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) As \"JsonCount\",Sum(\"o\".\"Quantity\" * \"o\".\"Price\") As \"Total\" \r\nFrom \"public\".\"orders\" As \"o\"";
        var builder = new PostgreSqlBuilder();

        // Act
        var sql = builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "Total")
            .From("public.orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - PostgreSQL 非 Count 聚合使用通配符时应抛出明确异常。
    /// </summary>
    [Fact]
    public void Aggregate_WhenNonCountFunctionUsesWildcard_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new PostgreSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Sum("*", "Total"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Equal("Select Count(*) As \"Total\" \r\nFrom \"public\".\"orders\"", builder.CountAll("Total").From("public.orders").ToSql());
    }

    /// <summary>
    /// 测试 - PostgreSQL 可转换表达式应保留字符串与注释，结构化聚合应支持转义引用标识符。
    /// </summary>
    [Fact]
    public void AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract(\"o\".\"Data\", '$[0]') + \"o\".\"Amount\" /* [comment] */),Max(\"Sales Order\".\"Order]Name\") As \"Escaped\" \r\nFrom \"public\".\"orders\" As \"o\"";
        var builder = new PostgreSqlBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */")
            .Aggregate(SqlAggregateFunction.Max, "[Sales Order].[Order]]Name]", "Escaped")
            .From("public.orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}