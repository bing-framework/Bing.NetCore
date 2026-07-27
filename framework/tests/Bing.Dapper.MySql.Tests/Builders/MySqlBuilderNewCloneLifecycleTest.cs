using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL Builder New 与 Clone 生命周期测试。
/// </summary>
public class MySqlBuilderNewCloneLifecycleTest
{
    /// <summary>
    /// 测试 - New 应保留 MySQL Builder 类型并使用独立的空参数管理器。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyIndependentMySqlBuilder()
    {
        // Arrange
        var source = new MySqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.IsType<MySqlBuilder>(fresh);
        Assert.Equal(1, Assert.Single(source.GetParams()).Value);
        Assert.Equal(2, Assert.Single(fresh.GetParams()).Value);
    }

    /// <summary>
    /// 测试 - Clone 应保持 MySQL Join Clause 类型和带点物理表名策略。
    /// </summary>
    [Fact]
    public void Clone_WhenMySqlJoinClauseExists_ShouldPreserveProviderClauseTypeAndBehavior()
    {
        // Arrange
        var source = new MySqlBuilder();
        source.Select("*").From("Orders");

        // Act
        var clone = (MySqlBuilder)source.Clone();
        clone.Join("`archive_db`.`Order.Log2026`", "audit");

        // Assert
        Assert.IsType<MySqlFromClause>(clone.FromClause);
        Assert.IsType<MySqlJoinClause>(clone.JoinClause);
        Assert.Equal("Select * \r\nFrom `Orders` \r\nJoin `archive_db`.`Order.Log2026` As `audit`", clone.ToSql());
    }
}