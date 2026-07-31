using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL Insert Select SQL 测试。
/// </summary>
public class MySqlInsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - MySQL 应按反引号和 @ 参数格式输出 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_ShouldRenderMySqlSql()
    {
        // Arrange
        var builder = new MySqlBuilder()
            .InsertInto(new SqlTableReference { Schema = "archive", TableName = "archive_orders" })
            .Columns("Id", "Code")
            .Select("Id,Code")
            .From(new SqlTableReference { Schema = "sales", TableName = "orders" })
            .Where("Status", "active");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into `archive`.`archive_orders` (`Id`, `Code`) \r\nSelect `Id`,`Code` \r\nFrom `sales`.`orders` \r\nWhere `Status`=@_p_0", sql);
    }
}