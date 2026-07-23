using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - PostgreSQL 原始 From 应保留 ONLY 和参数占位符。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new PostgreSqlBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[public].[users] u ONLY /* @TenantId */")
            .ToSql();

        // Assert
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom [public].[users] u ONLY /* @TenantId */", sql);
    }

    /// <summary>
    /// 测试 - PostgreSQL 原始 Join 应保留 LATERAL、JSON 操作符和位置参数。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldPreserveRawSql()
    {
        var sql = new PostgreSqlBuilder().Select("u.Id").AppendFrom("users u")
            .AppendJoin("Lateral (Select payload->>'name' As Name Where Id=$1) a On true").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom users u \r\nJoin Lateral (Select payload->>'name' As Name Where Id=$1) a On true", sql);
    }

    /// <summary>
    /// 测试 - PostgreSQL 原始左连接应保留反引号表名。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_ShouldPreserveRawSql()
    {
        var sql = new PostgreSqlBuilder().Select("u.Id").AppendFrom("users u")
            .AppendLeftJoin("`Audit.Log` l On l.UserId=u.Id").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom users u \r\nLeft Join `Audit.Log` l On l.UserId=u.Id", sql);
    }

    /// <summary>
    /// 测试 - PostgreSQL 原始右连接应保留双引号带点表名。
    /// </summary>
    [Fact]
    public void AppendRightJoin_ShouldPreserveRawSql()
    {
        var sql = new PostgreSqlBuilder().Select("u.Id").AppendFrom("users u")
            .AppendRightJoin("\"Payments.Log\" p On p.UserId=u.Id").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom users u \r\nRight Join \"Payments.Log\" p On p.UserId=u.Id", sql);
    }
}