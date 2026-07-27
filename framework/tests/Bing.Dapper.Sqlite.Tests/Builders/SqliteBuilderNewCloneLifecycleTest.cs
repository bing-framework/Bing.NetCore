using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite Builder New 与 Clone 生命周期测试。
/// </summary>
public class SqliteBuilderNewCloneLifecycleTest
{
    /// <summary>
    /// 测试 - New 应保留 SQLite Builder 类型并隔离参数状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyIndependentSqliteBuilder()
    {
        // Arrange
        var source = new SqliteBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.IsType<SqliteBuilder>(fresh);
    }

    /// <summary>
    /// 测试 - Clone 应保持 SQLite Join Clause 类型和后续 Join SQL 行为。
    /// </summary>
    [Fact]
    public void Clone_WhenSqliteBuilderAddsJoin_ShouldPreserveSqliteClauseBehavior()
    {
        // Arrange
        var source = new SqliteBuilder();
        source.Select("*").From("Users");
        var expected = new SqliteBuilder().Select("*").From("Users").Join("Order.Items", "oi").ToSql();

        // Act
        var clone = (SqliteBuilder)source.Clone();
        clone.Join("Order.Items", "oi");

        // Assert
        Assert.IsType<SqliteFromClause>(clone.FromClause);
        Assert.IsType<SqliteJoinClause>(clone.JoinClause);
        Assert.Equal(expected, clone.ToSql());
    }
}