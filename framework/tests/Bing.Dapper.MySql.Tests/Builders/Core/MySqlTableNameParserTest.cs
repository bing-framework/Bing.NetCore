using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders.Core;

/// <summary>
/// MySQL 字符串表名解析器测试。
/// </summary>
public class MySqlTableNameParserTest
{
    /// <summary>
    /// 测试目的：合法字符串表名应解析为 MySQL 原子物理表名。
    /// </summary>
    [Theory]
    [InlineData("orders", null, "orders", null, null)]
    [InlineData("Merchants.Company", null, "Merchants.Company", null, null)]
    [InlineData("`orders`", null, "orders", null, null)]
    [InlineData("`Merchants.Company`", null, "Merchants.Company", null, null)]
    [InlineData("`archive_db`.`orders`", null, "orders", null, "archive_db")]
    [InlineData("`archive_db`.`Merchants.Company`", null, "Merchants.Company", null, "archive_db")]
    [InlineData("`archive.db`.`Merchants.Company.2025`", null, "Merchants.Company.2025", null, "archive.db")]
    [InlineData("orders o", null, "orders", "o", null)]
    [InlineData("orders AS o", null, "orders", "o", null)]
    [InlineData("`archive_db`.`Merchants.Company` AS c", null, "Merchants.Company", "c", "archive_db")]
    [InlineData("`archive``db`.`Merchants.Company`", null, "Merchants.Company", null, "archive`db")]
    [InlineData("orders", "o", "orders", "o", null)]
    [InlineData("orders AS o", "o", "orders", "o", null)]
    public void Parse_WhenTableNameIsValid_ShouldReturnStructuredReference(string table, string alias,
        string expectedTableName, string expectedAlias, string expectedSchema)
    {
        var result = MySqlTableNameParser.Parse(table, alias);

        Assert.Equal(expectedTableName, result.TableName);
        Assert.Equal(expectedAlias, result.Alias);
        Assert.Equal(expectedSchema, result.Schema);
    }

    /// <summary>
    /// 测试目的：冲突的内嵌和显式别名必须被拒绝。
    /// </summary>
    [Fact]
    public void Parse_WhenAliasesConflict_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => MySqlTableNameParser.Parse("orders AS order_source", "o"));
    }

    /// <summary>
    /// 测试目的：非对象名表达式和非法标识符必须在 SQL 生成前被拒绝。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("`orders")]
    [InlineData("orders.")]
    [InlineData("`archive_db`.``")]
    [InlineData("`archive_db`..`orders`")]
    [InlineData("`archive_db`.`orders`;")]
    [InlineData("orders -- comment")]
    [InlineData("orders /* comment */")]
    [InlineData("orders\t")]
    [InlineData("(Select 1) As orders")]
    [InlineData("orders UNION")]
    [InlineData("orders JOIN users")]
    [InlineData("`archive_db`.`orders`.`history`")]
    [InlineData("`archive_db`.orders")]
    [InlineData("archive_db.`orders`")]
    [InlineData("`archive_db`.Merchants.Company")]
    [InlineData("archive_db.`Merchants.Company`")]
    public void Parse_WhenTableNameIsInvalid_ShouldThrowArgumentException(string table)
    {
        Assert.Throws<ArgumentException>(() => MySqlTableNameParser.Parse(table));
    }
}