using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - 调试 SQL
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - 调试 SQL 应替换 MySQL 字符串和数值字面量，且不破坏原参数。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenParametersExist_ShouldRenderMySqlLiteralsAndPreserveParameters()
    {
        // Arrange
        const string expected = "Select * \r\nFrom `users` \r\nWhere `Status`='active' And `Id`=7";

        // Act
        var sql = _builder.From("users").Where("Status", "active").Where("Id", 7).ToDebugSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal(7, _builder.GetParam("@_p_1"));
    }
}