using Bing.Data;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSql生成器测试 - CTE、集合与调试 SQL
/// </summary>
public partial class PostgreSqlBuilderTest
{
    /// <summary>
    /// 测试 - CTE 应在主查询前输出并合并其参数。
    /// </summary>
    [Fact]
    public void With_WhenCteIsConfigured_ShouldRenderCteBeforeMainQuery()
    {
        // Arrange
        const string expected = "With \"active_users\" \r\nAs (Select \"Id\" \r\nFrom \"users\" \r\nWhere \"Enabled\"=@_p_0)\r\nSelect \"Id\" \r\nFrom \"active_users\"";
        var cte = _builder.New().Select("Id").From("users").Where("Enabled", true);

        // Act
        var sql = _builder.Select("Id").From("active_users").With("active_users", cte).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.True((bool)_builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - Union All 应保留两个查询并重命名冲突参数。
    /// </summary>
    [Fact]
    public void UnionAll_WhenBothQueriesHaveParameters_ShouldRenderAllParameters()
    {
        // Arrange
        const string expected = "(Select \"Id\" \r\nFrom \"users\" \r\nWhere \"Status\"=@_p_0 \r\n) \r\nUnion All \r\n(Select \"Id\" \r\nFrom \"archived_users\" \r\nWhere \"Status\"=@_p_1 \r\n)";
        var archived = _builder.New().Select("Id").From("archived_users").Where("Status", "archived");

        // Act
        var sql = _builder.Select("Id").From("users").Where("Status", "active").UnionAll(archived).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal("archived", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - 调试 SQL 应将布尔值、数值和字符串参数替换为 PostgreSql 字面量。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenParametersExist_ShouldRenderPostgreSqlLiterals()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"users\" \r\nWhere \"Enabled\"=true And \"Level\"=12 And \"Name\"='alice'";

        // Act
        var sql = _builder.From("users").Where("Enabled", true).Where("Level", 12).Where("Name", "alice").ToDebugSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}