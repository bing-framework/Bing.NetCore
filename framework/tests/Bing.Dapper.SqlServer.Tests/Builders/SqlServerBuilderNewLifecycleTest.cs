using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQL Server Builder New 生命周期测试。
/// </summary>
public class SqlServerBuilderNewLifecycleTest
{
    /// <summary>
    /// 测试 - New 应保留 SQL Server Builder 类型并隔离参数状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyIndependentSqlServerBuilder()
    {
        // Arrange
        var source = new SqlServerBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.IsType<SqlServerBuilder>(fresh);
        Assert.Equal(1, Assert.Single(source.GetParams()).Value);
        Assert.Equal(2, Assert.Single(fresh.GetParams()).Value);
    }
}