using Bing.Data.Inventory;
using Bing.FreeSQL.Inventory;
using FreeSql;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证 FreeSQL 工作单元库存适配。
/// </summary>
public sealed class FreeSqlInventoryAdapterTest
{
    /// <summary>
    /// 库存适配复用 FreeSQL 当前事务并提交条件扣减。
    /// </summary>
    [Fact]
    public async Task TryDeductInventoryAsync_ShouldReuseCurrentFreeSqlTransaction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-inventory-{Guid.NewGuid():N}.db");
        var orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={path}")
            .UseAutoSyncStructure(false)
            .Build();
        try
        {
            foreach (var statement in InventorySchema.GetCreateStatements(InventoryDatabase.Sqlite))
                orm.Ado.ExecuteNonQuery(statement);
            orm.Ado.ExecuteNonQuery("INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                                    "VALUES ('tenant', 'warehouse', 'sku', 10)");
            InventoryDeductionResult result;
            using (var unitOfWork = orm.CreateUnitOfWork())
            {
                unitOfWork.GetOrBeginTransaction();
                result = await unitOfWork.TryDeductInventoryAsync(
                    new DbInventoryStore(InventoryDatabase.Sqlite),
                    new InventoryDeductionRequest("tenant", "warehouse", "sku", 4, "freesql-request"));

                Assert.True(result.Applied);
                Assert.Equal(6, result.RemainingQuantity);
                unitOfWork.Commit();
            }

            var quantity = Convert.ToInt64(orm.Ado.ExecuteScalar(
                "SELECT Quantity FROM bing_inventory_stock WHERE TenantId = 'tenant' " +
                "AND WarehouseId = 'warehouse' AND Sku = 'sku'"));
            Assert.Equal(6, quantity);
        }
        finally
        {
            orm.Dispose();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 没有活动事务时拒绝执行库存扣减。
    /// </summary>
    [Fact]
    public async Task TryDeductInventoryAsync_WhenNoCurrentTransaction_ShouldReject()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-inventory-{Guid.NewGuid():N}.db");
        var orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={path}")
            .UseAutoSyncStructure(false)
            .Build();
        try
        {
            using var unitOfWork = orm.CreateUnitOfWork();
            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.TryDeductInventoryAsync(
                new DbInventoryStore(InventoryDatabase.Sqlite),
                new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "freesql-no-transaction")));
        }
        finally
        {
            orm.Dispose();
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
