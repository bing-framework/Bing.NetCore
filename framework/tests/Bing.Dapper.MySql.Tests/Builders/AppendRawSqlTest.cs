using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - MySQL 原始 From 应保留方括号、Hint 和参数占位符。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new MySqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("[archive].[Order.Log2025] o FORCE INDEX (IX_Order) /* @TenantId ? */")
            .ToSql();

        // Assert
        Assert.Equal("Select `o`.`Id` \r\nFrom [archive].[Order.Log2025] o FORCE INDEX (IX_Order) /* @TenantId ? */", sql);
    }

    /// <summary>
    /// 测试 - MySQL 原始 Join 应保留双引号和参数占位符。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldPreserveRawSql()
    {
        var sql = new MySqlBuilder().Select("o.Id").AppendFrom("Orders o")
            .AppendJoin("\"Audit.Log\" a On a.OrderId=o.Id /* ? */").ToSql();

        Assert.Equal("Select `o`.`Id` \r\nFrom Orders o \r\nJoin \"Audit.Log\" a On a.OrderId=o.Id /* ? */", sql);
    }

    /// <summary>
    /// 测试 - MySQL 原始左连接应保留调用方提供的文本。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_ShouldPreserveRawSql()
    {
        var sql = new MySqlBuilder().Select("o.Id").AppendFrom("Orders o")
            .AppendLeftJoin("Orders l On l.Id=o.Id").ToSql();

        Assert.Equal("Select `o`.`Id` \r\nFrom Orders o \r\nLeft Join Orders l On l.Id=o.Id", sql);
    }

    /// <summary>
    /// 测试 - MySQL 原始右连接应保留反引号带点物理表名。
    /// </summary>
    [Fact]
    public void AppendRightJoin_ShouldPreserveRawSql()
    {
        var sql = new MySqlBuilder().Select("o.Id").AppendFrom("Orders o")
            .AppendRightJoin("`Payments.Log` p On p.OrderId=o.Id").ToSql();

        Assert.Equal("Select `o`.`Id` \r\nFrom Orders o \r\nRight Join `Payments.Log` p On p.OrderId=o.Id", sql);
    }
}