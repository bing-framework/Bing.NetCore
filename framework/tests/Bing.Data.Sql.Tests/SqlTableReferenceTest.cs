using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 结构化 SQL 表引用测试。
/// </summary>
public class SqlTableReferenceTest
{
    /// <summary>
    /// 测试 - 修改表引用副本不应影响原始表引用。
    /// </summary>
    [Fact]
    public void WithAlias_WhenCreatingCopy_ShouldNotChangeOriginalReference()
    {
        // Arrange
        var reference = new SqlTableReference
        {
            Catalog = "erp",
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders"
        };

        // Act
        var copy = reference.WithAlias("o").WithPhysicalSchema("reporting");

        // Assert
        Assert.Null(reference.Alias);
        Assert.Equal("dbo", reference.PhysicalSchema);
        Assert.Equal("o", copy.Alias);
        Assert.Equal("reporting", copy.PhysicalSchema);
        Assert.Equal("erp", copy.Catalog);
        Assert.Equal("orders", copy.ResolvedTableName);
    }
}