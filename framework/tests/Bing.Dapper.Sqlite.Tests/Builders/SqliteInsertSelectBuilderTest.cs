using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite Insert Select SQL 测试。
/// </summary>
public class SqliteInsertSelectBuilderTest
{
    /// <summary>
    /// 测试 - SQLite 应按反引号和 @ 参数格式输出 Insert Select。
    /// </summary>
    [Fact]
    public void InsertSelect_ShouldRenderSqliteSql()
    {
        // Arrange
        var source = new SqliteBuilder()
            .Select("Id,Code")
            .From("orders")
            .Where("Status", "active");
        var builder = new SqlInsertBuilder(SqliteSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .InsertInto("archive_orders")
            .Columns("Id", "Code")
            .InsertSelect(source);

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into `archive_orders` (`Id`, `Code`) \r\nSelect `Id`,`Code` \r\nFrom `orders` \r\nWhere `Status`=@_p_0", sql);
    }
}
