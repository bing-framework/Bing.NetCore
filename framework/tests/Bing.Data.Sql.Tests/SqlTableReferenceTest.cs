using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 表引用测试。
/// </summary>
public class SqlTableReferenceTest
{
    /// <summary>
    /// 测试目的：表引用副本应保持原始名称段不变，同时允许指定别名。
    /// </summary>
    [Fact]
    public void Copy_WhenAliasChanged_ShouldKeepOriginalNameParts()
    {
        // Arrange
        var reference = new SqlTableReference
        {
            Database = "erp",
            Schema = "dbo",
            TableName = "orders"
        };

        // Act
        var copy = reference with { Alias = "o" };

        // Assert
        Assert.Null(reference.Alias);
        Assert.Equal("erp", copy.Database);
        Assert.Equal("dbo", copy.Schema);
        Assert.Equal("orders", copy.TableName);
        Assert.Equal("o", copy.Alias);
    }
}