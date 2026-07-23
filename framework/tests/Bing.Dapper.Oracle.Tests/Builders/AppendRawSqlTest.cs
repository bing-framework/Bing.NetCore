using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - Oracle 原始 From 应保留双引号和 Hint。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new OracleBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[SCOTT].[USERS] u /*+ INDEX(u IX_USERS) */")
            .ToSql();

        // Assert
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom [SCOTT].[USERS] u /*+ INDEX(u IX_USERS) */", sql);
    }

    /// <summary>
    /// 测试 - Oracle 原始 Join 应保留数据库链接且不将 @REMOTE_DB 识别为参数。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldPreserveRawSql()
    {
        var sql = new OracleBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendJoin("`AUDIT.LOG`@REMOTE_DB a On a.UserId=u.Id And a.TenantId=:TenantId").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom Users u \r\nJoin `AUDIT.LOG`@REMOTE_DB a On a.UserId=u.Id And a.TenantId=:TenantId", sql);
    }

    /// <summary>
    /// 测试 - Oracle 原始左连接应保留调用方提供的文本。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_ShouldPreserveRawSql()
    {
        var sql = new OracleBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendLeftJoin("Users l On l.Id=u.Id").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom Users u \r\nLeft Join Users l On l.Id=u.Id", sql);
    }

    /// <summary>
    /// 测试 - Oracle 原始右连接应保留双引号带点表名。
    /// </summary>
    [Fact]
    public void AppendRightJoin_ShouldPreserveRawSql()
    {
        var sql = new OracleBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendRightJoin("\"PAYMENTS.LOG\" p On p.UserId=u.Id").ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom Users u \r\nRight Join \"PAYMENTS.LOG\" p On p.UserId=u.Id", sql);
    }
}