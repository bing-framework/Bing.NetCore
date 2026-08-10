using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL SQL 生成器聚合测试。
/// </summary>
public class MySqlBuilderAggregateTest
{
    /// <summary>
    /// 测试 - MySQL Distinct 聚合应在函数参数内引用每个限定列名称段。
    /// </summary>
    [Fact]
    public void Aggregate_WhenDistinctQualifiedColumnsAreConfigured_ShouldRenderMySqlSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct `o`.`UserId`) As `UserCount`,Sum(Distinct `o`.`Amount`) As `Amount`,Avg(Distinct `o`.`Amount`) As `Average`,Max(Distinct `o`.`Amount`) As `Maximum`,Min(Distinct `o`.`Amount`) As `Minimum` \r\nFrom `orders` As `o`";
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.CountColumn("o.UserId", "UserCount", distinct: true)
            .Sum("o.Amount", "Amount", distinct: true)
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum", distinct: true)
            .From("orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - MySQL 可转换聚合表达式应转换方括号标识符且保留 Case 表达式。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenCaseExpressionIsConfigured_ShouldRenderMySqlSql()
    {
        // Arrange
        const string expected = "Select Count(Distinct Case When `o`.`Enabled`=1 Then `o`.`UserId` End) As `EnabledUsers` \r\nFrom `orders` As `o`";
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Count,
                "Case When [o].[Enabled]=1 Then [o].[UserId] End", "EnabledUsers", distinct: true)
            .From("orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - MySQL 原始聚合不应改写 JSON Path 或字符串常量方括号。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenJsonPathAndStringBracketsAreConfigured_ShouldPreserveText()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) As `JsonCount`,Max('[abc]') As `Marker` \r\nFrom `orders` As `o`";
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .AggregateRaw(SqlAggregateFunction.Max, "'[abc]'", "Marker")
            .From("orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - MySQL Count API 应支持通配符、列和聚合参数 Distinct。
    /// </summary>
    [Fact]
    public void Count_WhenWildcardAndDistinctColumnAreConfigured_ShouldRenderMySqlSql()
    {
        // Arrange
        const string expected = "Select Count(*) As `Total`,Count(Distinct `o`.`UserId`) As `UserCount` \r\nFrom `orders` As `o`";
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.CountAll("Total").CountColumn("o.UserId", "UserCount", distinct: true)
            .From("orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - MySQL 可转换表达式应保留字符串与注释，结构化聚合应支持转义引用标识符。
    /// </summary>
    [Fact]
    public void AggregateExpressionAndStructuredIdentifier_WhenComplexContextsAreConfigured_ShouldRenderMySqlSql()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract(`o`.`Data`, '$[0]') + `o`.`Amount` /* [comment] */),Max(`Sales Order`.`Order]Name`) As `Escaped` \r\nFrom `orders` As `o`";
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0]') + [o].[Amount] /* [comment] */")
            .Aggregate(SqlAggregateFunction.Max, "[Sales Order].[Order]]Name]", "Escaped")
            .From("orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}