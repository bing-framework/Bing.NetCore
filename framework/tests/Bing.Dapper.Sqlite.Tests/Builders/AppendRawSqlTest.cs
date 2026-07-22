using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - SQLite 原始 Append 文本应保留异方言引号、索引提示、注释和占位符。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldPreserveAllText()
    {
        // Arrange
        var builder = new SqliteBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[main].[users] u INDEXED BY ix_users /* @tenant */")
            .AppendJoin("\"Audit.Log\" a On a.UserId=u.Id /* ? {0} */")
            .AppendLeftJoin("Users l NOT INDEXED On l.Id=u.Id")
            .AppendRightJoin("`Payments.Log` p On p.UserId=u.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select `u`.`Id` \r\nFrom [main].[users] u INDEXED BY ix_users /* @tenant */ \r\nJoin \"Audit.Log\" a On a.UserId=u.Id /* ? {0} */ \r\nLeft Join Users l NOT INDEXED On l.Id=u.Id \r\nRight Join `Payments.Log` p On p.UserId=u.Id", sql);
    }
}