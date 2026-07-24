using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - From 子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - 普通表来源应引用表别名和列名。
    /// </summary>
    [Fact]
    public void From_WhenTableAndAliasAreConfigured_ShouldRenderQuotedColumns()
    {
        // Arrange
        const string expected = "Select `u`.`Id`,`u`.`Status` \r\nFrom `users` As `u`";

        // Act
        var sql = _builder.Select("u.Id,u.Status").From("users", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}