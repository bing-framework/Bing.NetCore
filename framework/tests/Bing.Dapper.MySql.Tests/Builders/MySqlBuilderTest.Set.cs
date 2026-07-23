using Bing.Data;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql生成器测试 - 集合操作
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - Union All 应保留两个查询并重命名冲突参数。
    /// </summary>
    [Fact]
    public void UnionAll_WhenBothQueriesHaveParameters_ShouldRenderAllParameters()
    {
        // Arrange
        const string expected = "(Select `Id` \r\nFrom `users` \r\nWhere `Status`=@_p_1 \r\n) \r\nUnion All \r\n(Select `Id` \r\nFrom `archived_users` \r\nWhere `Status`=@_p_0 \r\n)";
        var archived = _builder.New().Select("Id").From("archived_users").Where("Status", "archived");

        // Act
        var sql = _builder.Select("Id").From("users").Where("Status", "active").UnionAll(archived).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal("archived", _builder.GetParam("@_p_0"));
        Assert.Equal("active", _builder.GetParam("@_p_1"));
    }
}