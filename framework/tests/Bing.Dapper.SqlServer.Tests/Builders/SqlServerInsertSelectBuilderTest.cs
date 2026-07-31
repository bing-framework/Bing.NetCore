using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQL Server Insert Select SQL 测试。
/// </summary>
public class SqlServerInsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - SQL Server 应按方括号和 @ 参数格式输出 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_ShouldRenderSqlServerSql()
    {
        // Arrange
        var builder = new SqlServerBuilder()
            .InsertInto(new SqlTableReference { Schema = "archive", TableName = "archive_orders" })
            .Columns("Id", "Code")
            .Select("Id,Code")
            .From(new SqlTableReference { Schema = "sales", TableName = "orders" })
            .Where("Status", "active");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [archive].[archive_orders] ([Id], [Code]) \r\nSelect [Id],[Code] \r\nFrom [sales].[orders] \r\nWhere [Status]=@_p_0", sql);
    }
}