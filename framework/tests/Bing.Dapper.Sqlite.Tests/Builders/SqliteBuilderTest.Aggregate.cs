using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite SQL 生成器聚合测试。
/// </summary>
public class SqliteBuilderAggregateTest
{
    /// <summary>
    /// 测试 - SQLite Distinct 聚合应在函数参数内使用反引号限定列。
    /// </summary>
    [Fact]
    public void Aggregate_WhenDistinctQualifiedColumnsAreConfigured_ShouldRenderSqliteSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct `o`.`UserId`) As `UserCount`,Sum(Distinct `o`.`Amount`) As `Amount`,Avg(Distinct `o`.`Amount`) As `Average`,Max(Distinct `o`.`Amount`) As `Maximum`,Min(Distinct `o`.`Amount`) As `Minimum` \r\nFrom `Orders` As `o`";
        var builder = new SqliteBuilder();

        // Act
        var sql = builder.Count("o.UserId", "UserCount", distinct: true)
            .Sum("o.Amount", "Amount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - SQLite 原始聚合应保留 JSON Path，表达式聚合应转换方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenConfigured_ShouldRenderSqliteSql()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) As `JsonCount`,Sum(`o`.`Quantity` * `o`.`Price`) As `Total` \r\nFrom `Orders` As `o`";
        var builder = new SqliteBuilder();

        // Act
        var sql = builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "Total")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - SQLite 非 Count 聚合使用通配符时应抛出明确异常。
    /// </summary>
    [Fact]
    public void Aggregate_WhenNonCountFunctionUsesWildcard_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new SqliteBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Min("*", "Minimum"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Equal("Select Count(*) As `Total` \r\nFrom `Orders`", builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - SQLite 可转换表达式应保留字符串与注释，结构化聚合应支持转义引用标识符。
    /// </summary>
    [Fact]
    public void AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRenderSqliteSql()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract(`o`.`Data`, '$[0]') + `o`.`Amount` /* [comment] */),Max(`Sales Order`.`Order]Name`) As `Escaped` \r\nFrom `Orders` As `o`";
        var builder = new SqliteBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */")
            .Aggregate(SqlAggregateFunction.Max, "[Sales Order].[Order]]Name]", "Escaped")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}