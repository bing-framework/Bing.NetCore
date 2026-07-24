using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - 状态隔离
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - Clone 应隔离新增条件，Clear 应清除 SQL 与参数状态。
    /// </summary>
    [Fact]
    public void CloneAndClear_WhenNormalTableStateExists_ShouldKeepInstancesIsolated()
    {
        // Arrange
        const string sourceExpected = "Select * \r\nFrom `users` As `u` \r\nWhere `u`.`Status`=@_p_0";
        const string cloneExpected = "Select * \r\nFrom `users` As `u` \r\nWhere `u`.`Status`=@_p_0 And `u`.`Role`=@_p_1";
        const string clearedExpected = "Select * \r\nFrom `roles`";
        _builder.From("users", "u").Where("u.Status", "active");

        // Act
        var clone = _builder.Clone();
        clone.Where("u.Role", "admin");
        var sourceSql = _builder.ToSql();
        var cloneSql = clone.ToSql();
        var cloneRole = clone.GetParam("@_p_1");
        var clearedSql = clone.Clear().From("roles").ToSql();

        // Assert
        Assert.Equal(sourceExpected, sourceSql);
        Assert.Equal(cloneExpected, cloneSql);
        Assert.Equal(clearedExpected, clearedSql);
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal("admin", cloneRole);
        Assert.Empty(clone.GetParams());
    }
}