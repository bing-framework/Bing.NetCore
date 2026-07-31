using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL Insert Select SQL 测试。
/// </summary>
public class PostgreSqlInsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - PostgreSQL 应按双引号和 @ 参数格式输出 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .InsertInto(new SqlTableReference { Schema = "archive", TableName = "archive_orders" })
            .Columns("Id", "Code")
            .Select("Id,Code")
            .From(new SqlTableReference { Schema = "sales", TableName = "orders" })
            .Where("Status", "active");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into \"archive\".\"archive_orders\" (\"Id\", \"Code\") \r\nSelect \"Id\",\"Code\" \r\nFrom \"sales\".\"orders\" \r\nWhere \"Status\"=@_p_0", sql);
    }
}