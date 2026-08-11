using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

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

    /// <summary>
    /// 测试目的：SQLite Profile 应在生成 SQL 前拒绝运行时不支持的 Right Join。
    /// </summary>
    [Fact]
    public void ToSql_WhenRightJoinIsConfigured_ShouldRejectUsingSqliteProfile()
    {
        // Arrange
        var builder = new SqliteBuilder();
        builder.Select("o.Id").From("samples", "s")
            .RightJoin("Orders", "o").AppendOn("s.Id=o.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal(SqlQueryCapabilityState.Unsupported, SqliteSqlProvider.Instance.Profile.Query.RightJoin);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", exception.Message);
    }

    /// <summary>
    /// 测试目的：结构化 Right Join 同样应在 SQLite 生成 SQL 前按 Provider Profile 拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenStructuredRightJoinIsConfigured_ShouldRejectUsingSqliteProfile()
    {
        // Arrange
        var builder = new SqliteBuilder();
        builder.Select("o.Id").From("samples", "s")
            .RightJoin(new SqlTableReference { TableName = "Orders", Alias = "o" })
            .AppendOn("s.Id=o.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", exception.Message);
    }

    /// <summary>
    /// 测试目的：SQLite Profile 应在生成 SQL 前拒绝 Full Join，且数据源或选项不得重新启用。
    /// </summary>
    [Fact]
    public void ToSql_WhenFullJoinIsConfigured_ShouldRejectUsingSqliteProfile()
    {
        // Arrange
        var builder = new SqliteBuilder();
        builder.Select("o.Id").From("samples", "s")
            .FullJoin("Orders", "o").AppendOn("s.Id=o.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal(SqlQueryCapabilityState.Unsupported, SqliteSqlProvider.Instance.Profile.Query.FullJoin);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", exception.Message);
    }
}