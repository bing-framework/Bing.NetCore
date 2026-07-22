using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - Oracle 原始 Append 文本应保留异方言引号、Hint、数据库链接和占位符。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldPreserveAllText()
    {
        // Arrange
        var builder = new OracleBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[SCOTT].[USERS] u /*+ INDEX(u IX_USERS) */")
            .AppendJoin("`AUDIT.LOG`@REPORTING a On a.UserId=u.Id /* @tenant */")
            .AppendLeftJoin("Users l On l.Id=u.Id")
            .AppendRightJoin("\"PAYMENTS.LOG\" p On p.UserId=u.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom [SCOTT].[USERS] u /*+ INDEX(u IX_USERS) */ \r\nJoin `AUDIT.LOG`@REPORTING a On a.UserId=u.Id /* @tenant */ \r\nLeft Join Users l On l.Id=u.Id \r\nRight Join \"PAYMENTS.LOG\" p On p.UserId=u.Id", sql);
    }
}