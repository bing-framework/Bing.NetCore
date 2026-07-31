using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle Insert Select SQL 测试。
/// </summary>
public class OracleInsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - Oracle 应按双引号和冒号参数格式输出 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_ShouldRenderOracleSql()
    {
        // Arrange
        var builder = new OracleBuilder()
            .InsertInto(new SqlTableReference { Schema = "archive", TableName = "archive_orders" })
            .Columns("Id", "Code")
            .Select("Id,Code")
            .From(new SqlTableReference { Schema = "sales", TableName = "orders" })
            .Where("Status", "active");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into \"archive\".\"archive_orders\" (\"Id\", \"Code\") \r\nSelect \"Id\",\"Code\" \r\nFrom \"sales\".\"orders\" \r\nWhere \"Status\"=:p_0", sql);
    }
}