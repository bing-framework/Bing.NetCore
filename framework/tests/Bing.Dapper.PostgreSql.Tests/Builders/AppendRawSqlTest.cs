using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - PostgreSQL 原始 Append 文本应保留异方言引号、Lateral、注释和占位符。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldPreserveAllText()
    {
        // Arrange
        var builder = new PostgreSqlBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("[public].[users] u ONLY /* @tenant */")
            .AppendJoin("Lateral (Select 1 As Id) a On true /* ? {0} */")
            .AppendLeftJoin("`Audit.Log` l On l.UserId=u.Id")
            .AppendRightJoin("\"Payments.Log\" p On p.UserId=u.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom [public].[users] u ONLY /* @tenant */ \r\nJoin Lateral (Select 1 As Id) a On true /* ? {0} */ \r\nLeft Join `Audit.Log` l On l.UserId=u.Id \r\nRight Join \"Payments.Log\" p On p.UserId=u.Id", sql);
    }
}