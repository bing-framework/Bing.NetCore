using Bing.Data.Sql;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询对象普通 Join 真实执行测试。
/// </summary>
public partial class MySqlQueryTest
{
    /// <summary>
    /// 测试 - MySQL 普通内连接应返回匹配的产品项。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MySql_InnerJoin_ShouldReturnMatchingProductItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        await InitProductDataAsync(productId, "join-inner");
        await InsertProductItemAsync(Guid.NewGuid(), productId, "inner-sku", 3);
        using var query = _fixture.CreateQuery();
        var description = query.Sql<ProductItemProjection>().Select("p.Code As ProductCode,i.Sku As Sku,i.Quantity As Quantity")
            .AppendFrom("Product p")
            .Join("ProductItem", "i").AppendOn("i.ProductId=p.ProductId")
            .Where("p.ProductId", productId);

        // Act
        var result = await description.FirstOrDefaultAsync();

        // Assert
        Assert.Equal("join-inner", result.ProductCode);
        Assert.Equal("inner-sku", result.Sku);
        Assert.Equal(3, result.Quantity);
    }

    /// <summary>
    /// 测试 - MySQL 普通左连接应保留不存在产品项的产品记录。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MySql_LeftJoin_ShouldPreserveProductWithoutItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        await InitProductDataAsync(productId, "join-left");
        using var query = _fixture.CreateQuery();
        var description = query.Sql<ProductItemProjection>().Select("p.Code As ProductCode,i.Sku As Sku")
            .AppendFrom("Product p")
            .LeftJoin("ProductItem", "i").AppendOn("i.ProductId=p.ProductId")
            .Where("p.ProductId", productId);

        // Act
        var result = await description.FirstOrDefaultAsync();

        // Assert
        Assert.Equal("join-left", result.ProductCode);
        Assert.Null(result.Sku);
    }

    /// <summary>
    /// 写入 MySQL 产品项测试数据。
    /// </summary>
    private async Task InsertProductItemAsync(Guid itemId, Guid productId, string sku, int quantity)
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert ProductItem(ProductItemId,ProductId,Sku,Quantity,Enabled) Values(@itemId,@productId,@sku,@quantity,@enabled)",
            new { itemId, productId, sku, quantity, enabled = true });
    }

    /// <summary>
    /// MySQL 产品项查询投影。
    /// </summary>
    private sealed class ProductItemProjection
    {
        /// <summary>
        /// 产品编码。
        /// </summary>
        public string ProductCode { get; set; }

        /// <summary>
        /// 产品项编码。
        /// </summary>
        public string Sku { get; set; }

        /// <summary>
        /// 产品项数量。
        /// </summary>
        public int Quantity { get; set; }
    }
}