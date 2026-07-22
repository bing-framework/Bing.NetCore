using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - MySQL 原始 Append 文本应保留异方言引号、Hint、注释和占位符。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldPreserveAllText()
    {
        // Arrange
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("[archive].[Order.Log2025] o FORCE INDEX (IX_Order) /* @tenant */")
            .AppendJoin("\"Audit.Log\" a On a.OrderId=o.Id /* ? {0} */")
            .AppendLeftJoin("Orders l On l.Id=o.Id")
            .AppendRightJoin("`Payments.Log` p On p.OrderId=o.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select `o`.`Id` \r\nFrom [archive].[Order.Log2025] o FORCE INDEX (IX_Order) /* @tenant */ \r\nJoin \"Audit.Log\" a On a.OrderId=o.Id /* ? {0} */ \r\nLeft Join Orders l On l.Id=o.Id \r\nRight Join `Payments.Log` p On p.OrderId=o.Id", sql);
    }
}