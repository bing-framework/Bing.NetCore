using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle Builder New 与 Clone 生命周期测试。
/// </summary>
public class OracleBuilderNewCloneLifecycleTest
{
    /// <summary>
    /// 测试 - New 应保留 Oracle Builder 类型并隔离参数状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyIndependentOracleBuilder()
    {
        // Arrange
        var source = new OracleBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.IsType<OracleBuilder>(fresh);
        Assert.Equal("1", Assert.Single(source.GetParams()).Value.ToString());
        Assert.Equal("2", Assert.Single(fresh.GetParams()).Value.ToString());
    }

    /// <summary>
    /// 测试 - Clone 后新增 Join 应保持 Oracle Clause 类型与原子带点表名策略。
    /// </summary>
    [Fact]
    public void Clone_WhenOracleBuilderAddsJoin_ShouldPreserveOracleClauseBehavior()
    {
        // Arrange
        var source = new OracleBuilder();
        source.Select("*").From("Users");
        var expected = new OracleBuilder().Select("*").From("Users").Join("Order.Items", "oi").ToSql();

        // Act
        var clone = (OracleBuilder)source.Clone();
        clone.Join("Order.Items", "oi");

        // Assert
        Assert.IsType<OracleFromClause>(clone.FromClause);
        Assert.IsType<OracleJoinClause>(clone.JoinClause);
        Assert.Equal(expected, clone.ToSql());
    }
}