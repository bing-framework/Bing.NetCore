using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL SQL 查询 Join 真实执行集成测试。
/// </summary>
public sealed partial class PostgreSqlQueryTest
{
    /// <summary>
    /// 测试 - PostgreSQL 普通内连接应返回匹配产品项。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task PostgreSql_InnerJoin_ShouldReturnMatchingProductItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        await InsertProductAsync(productId, "join-inner");
        await InsertProductItemAsync(Guid.NewGuid(), productId, "inner-sku", 3);
        using var query = _fixture.CreateQuery();
        var description = query.Query().Select("p.code As ProductCode,i.sku As Sku,i.quantity As Quantity")
            .AppendFrom("public.integration_products p")
            .Join("public.integration_product_items", "i").AppendOn("i.product_id=p.id")
            .Where("p.id", productId);

        // Act
        var result = await description.FirstOrDefaultAsync<ProductItemProjection>();

        // Assert
        Assert.Equal("join-inner", result.ProductCode);
        Assert.Equal("inner-sku", result.Sku);
        Assert.Equal(3, result.Quantity);
    }

    /// <summary>
    /// 测试 - PostgreSQL 普通左连接应保留不存在产品项的产品记录。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task PostgreSql_LeftJoin_ShouldPreserveProductWithoutItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        await InsertProductAsync(productId, "join-left");
        using var query = _fixture.CreateQuery();
        var description = query.Query().Select("p.code As ProductCode,i.sku As Sku")
            .AppendFrom("public.integration_products p")
            .LeftJoin("public.integration_product_items", "i").AppendOn("i.product_id=p.id")
            .Where("p.id", productId);

        // Act
        var result = await description.FirstOrDefaultAsync<ProductItemProjection>();

        // Assert
        Assert.Equal("join-left", result.ProductCode);
        Assert.Null(result.Sku);
    }

    /// <summary>
    /// 写入 PostgreSQL 产品项测试数据。
    /// </summary>
    private async Task InsertProductItemAsync(Guid itemId, Guid productId, string sku, int quantity)
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_product_items(item_id,product_id,sku,quantity,enabled) Values(@itemId,@productId,@sku,@quantity,@enabled)",
            new { itemId, productId, sku, quantity, enabled = true });
    }

    /// <summary>
    /// PostgreSQL 产品项查询投影。
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