using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle SQL 生成器聚合测试。
/// </summary>
public class OracleBuilderAggregateTest
{
    /// <summary>
    /// 测试 - Oracle Distinct 聚合应在函数参数内使用双引号限定列且不输出 Select As。
    /// </summary>
    [Fact]
    public void Aggregate_WhenDistinctQualifiedColumnsAreConfigured_ShouldRenderOracleSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct \"o\".\"UserId\") \"UserCount\",Sum(Distinct \"o\".\"Amount\") \"Amount\",Avg(Distinct \"o\".\"Amount\") \"Average\",Max(Distinct \"o\".\"Amount\") \"Maximum\",Min(Distinct \"o\".\"Amount\") \"Minimum\" \r\nFrom \"APP\".\"ORDERS\" \"o\"";
        var builder = new OracleBuilder();

        // Act
        var sql = builder.CountColumn("o.UserId", "UserCount", distinct: true)
            .Sum("o.Amount", "Amount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("APP.ORDERS", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Oracle 原始聚合应保留 JSON Path，表达式聚合应转换方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenConfigured_ShouldRenderOracleSql()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) \"JsonCount\",Sum(\"o\".\"Quantity\" * \"o\".\"Price\") \"Total\" \r\nFrom \"APP\".\"ORDERS\" \"o\"";
        var builder = new OracleBuilder();

        // Act
        var sql = builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "Total")
            .From("APP.ORDERS", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Oracle 非 Count 聚合使用通配符时应抛出明确异常。
    /// </summary>
    [Fact]
    public void Aggregate_WhenNonCountFunctionUsesWildcard_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new OracleBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Max("*", "Maximum"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Equal("Select Count(*) \"Total\" \r\nFrom \"APP\".\"ORDERS\"", builder.CountAll("Total").From("APP.ORDERS").ToSql());
    }

    /// <summary>
    /// 测试 - Oracle 可转换表达式应保留字符串与注释，结构化聚合应支持转义引用标识符。
    /// </summary>
    [Fact]
    public void AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRenderOracleSql()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract(\"o\".\"Data\", '$[0]') + \"o\".\"Amount\" /* [comment] */),Max(\"Sales Order\".\"Order]Name\") \"Escaped\" \r\nFrom \"APP\".\"ORDERS\" \"o\"";
        var builder = new OracleBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */")
            .Aggregate(SqlAggregateFunction.Max, "[Sales Order].[Order]]Name]", "Escaped")
            .From("APP.ORDERS", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}