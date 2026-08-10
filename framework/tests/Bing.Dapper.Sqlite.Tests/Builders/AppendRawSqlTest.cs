using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - SQLite 原始 From 应保留 main、索引提示和参数占位符。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new SqliteBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[main].[users] u INDEXED BY ix_users /* @TenantId */")
            .ToSql();

        // Assert
        Assert.Equal("Select `u`.`Id` \r\nFrom [main].[users] u INDEXED BY ix_users /* @TenantId */", sql);
    }

    /// <summary>
    /// 测试 - SQLite 原始 Join 应保留 temp 表和双引号。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldPreserveRawSql()
    {
        var sql = new SqliteBuilder().Select("u.Id").AppendFrom("main.Users u")
            .AppendJoin("\"temp.TempUsers\" a On a.UserId=u.Id").ToSql();

        Assert.Equal("Select `u`.`Id` \r\nFrom main.Users u \r\nJoin \"temp.TempUsers\" a On a.UserId=u.Id", sql);
    }

    /// <summary>
    /// 测试 - SQLite 原始左连接应保留 NOT INDEXED。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_ShouldPreserveRawSql()
    {
        var sql = new SqliteBuilder().Select("u.Id").AppendFrom("main.Users u")
            .AppendLeftJoin("Users l NOT INDEXED On l.Id=u.Id").ToSql();

        Assert.Equal("Select `u`.`Id` \r\nFrom main.Users u \r\nLeft Join Users l NOT INDEXED On l.Id=u.Id", sql);
    }

    /// <summary>
    /// 测试目的：SQLite 原始 Right Join 应在渲染前由 Provider Profile 拒绝。
    /// </summary>
    [Fact]
    public void AppendRightJoin_ShouldRejectUsingProviderProfile()
    {
        // Arrange
        var builder = new SqliteBuilder().Select("u.Id").AppendFrom("main.Users u")
            .AppendRightJoin("`Payments.Log` p On p.UserId=u.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", exception.Message);
    }
}