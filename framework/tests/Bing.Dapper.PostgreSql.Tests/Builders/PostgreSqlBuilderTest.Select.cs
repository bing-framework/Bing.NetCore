using Bing.Data;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSql生成器测试 - Select 子句
/// </summary>
public partial class PostgreSqlBuilderTest
{
    /// <summary>
    /// 测试 - 多列查询应按 PostgreSql 方言引用表别名和列名。
    /// </summary>
    [Fact]
    public void Select_WhenColumnsHaveTableAlias_ShouldRenderQuotedColumns()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\",\"u\".\"DisplayName\" \r\nFrom \"public\".\"users\" As \"u\"";

        // Act
        var sql = _builder.Select("Id,DisplayName", "u").From("public.users", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 限定列 Count 应分别引用表别名和列名。
    /// </summary>
    [Theory]
    [InlineData("u.Id")]
    [InlineData("\"u\".\"Id\"")]
    [InlineData("`u`.`Id`")]
    public void Count_WithQualifiedColumn_ShouldFormatEachIdentifierSegment(string column)
    {
        // Arrange
        const string expected = "Select Count(\"u\".\"Id\") As \"Count\" \r\nFrom \"public\".\"orders\" As \"u\"";

        // Act
        var sql = _builder.CountColumn(column, "Count").From("public.orders", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 限定列 Sum 应分别引用表别名和列名。
    /// </summary>
    [Theory]
    [InlineData("u.Amount")]
    [InlineData("\"u\".\"Amount\"")]
    [InlineData("`u`.`Amount`")]
    public void Sum_WithQualifiedColumn_ShouldFormatEachIdentifierSegment(string column)
    {
        // Arrange
        const string expected = "Select Sum(\"u\".\"Amount\") As \"Total\" \r\nFrom \"public\".\"orders\" As \"u\"";

        // Act
        var sql = _builder.Sum(column, "Total").From("public.orders", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 限定列 Avg、Max 与 Min 应分别引用表别名和列名。
    /// </summary>
    [Fact]
    public void Aggregates_WhenQualifiedColumnsAreUsed_ShouldFormatEachIdentifierSegment()
    {
        // Arrange
        const string expected = "Select Avg(\"u\".\"Amount\") As \"Average\",Max(\"u\".\"Amount\") As \"Maximum\",Min(\"u\".\"Amount\") As \"Minimum\" \r\nFrom \"public\".\"orders\" As \"u\"";

        // Act
        var sql = _builder.Avg("u.Amount", "Average").Max("u.Amount", "Maximum").Min("u.Amount", "Minimum")
            .From("public.orders", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 克隆后的限定列 Count 应保留聚合函数与分段引用。
    /// </summary>
    [Fact]
    public void Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation()
    {
        // Arrange
        const string expected = "Select Count(\"u\".\"Id\") As \"Count\" \r\nFrom \"public\".\"orders\" As \"u\"";
        _builder.CountColumn("u.Id", "Count").From("public.orders", "u");

        // Act
        var sql = _builder.Clone().ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 克隆后的查询应保留 Distinct 状态。
    /// </summary>
    [Fact]
    public void Clone_WhenDistinctIsConfigured_ShouldPreserveDistinct()
    {
        // Arrange
        const string expected = "Select Distinct \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\"";
        _builder.Distinct().Select("u.Id").From("public.users", "u");

        // Act
        var sourceSql = _builder.ToSql();
        var cloneSql = _builder.Clone().ToSql();

        // Assert
        Assert.Equal(expected, sourceSql);
        Assert.Equal(expected, cloneSql);
    }

    /// <summary>
    /// 测试 - Distinct 与聚合函数组合时应保留函数别名。
    /// </summary>
    [Fact]
    public void Select_WhenDistinctAndAggregateAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select Distinct Count(\"u\".\"Id\") As \"Count\",Sum(\"u\".\"Amount\") As \"Total\" \r\nFrom \"public\".\"orders\" As \"u\"";

        // Act
        var sql = _builder.Distinct().CountColumn("u.Id", "Count").Sum("u.Amount", "Total")
            .From("public.orders", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 子查询列应保留子查询参数并使用指定别名。
    /// </summary>
    [Fact]
    public void Select_WhenSubqueryColumnIsConfigured_ShouldMergeParameters()
    {
        // Arrange
        const string expected = "Select (Select Count(*) \r\nFrom \"audit\".\"logs\" \r\nWhere \"UserId\"=@_p_0) As \"LogCount\" \r\nFrom \"public\".\"users\" \r\nWhere \"Enabled\"=@_p_1";
        var subquery = _builder.New().CountAll().From("audit.logs").Where("UserId", 7);

        // Act
        var sql = _builder.Select(subquery, "LogCount").From("public.users").Where("Enabled", true).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal(7, _builder.GetParam("@_p_0"));
        Assert.True((bool)_builder.GetParam("@_p_1"));
    }
}