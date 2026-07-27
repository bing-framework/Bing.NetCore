using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL Builder New 生命周期测试。
/// </summary>
public class PostgreSqlBuilderNewLifecycleTest
{
    /// <summary>
    /// 测试 - New 应保留 PostgreSQL Builder 类型并隔离参数状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyIndependentPostgreSqlBuilder()
    {
        // Arrange
        var source = new PostgreSqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.IsType<PostgreSqlBuilder>(fresh);
        Assert.Equal(1, Assert.Single(source.GetParams()).Value);
        Assert.Equal(2, Assert.Single(fresh.GetParams()).Value);
    }
}