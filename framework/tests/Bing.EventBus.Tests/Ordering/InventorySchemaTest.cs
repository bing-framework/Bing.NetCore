using Bing.Data.Inventory;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证库存及请求表的身份规则和首次部署 SQL。
/// </summary>
public sealed class InventorySchemaTest
{
    /// <summary>
    /// 验证 MySQL 库存表使用二进制身份和事务引擎。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenMySql_ShouldMatchCompleteSql() => Assert.Equal(new[]
    {
        "CREATE TABLE IF NOT EXISTS `bing_inventory_stock` (" +
        "`TenantId` VARBINARY(512) NOT NULL, `WarehouseId` VARBINARY(512) NOT NULL, " +
        "`Sku` VARBINARY(1024) NOT NULL, `Quantity` BIGINT NOT NULL CHECK (`Quantity` >= 0), " +
        "PRIMARY KEY (`TenantId`, `WarehouseId`, `Sku`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        "CREATE TABLE IF NOT EXISTS `bing_inventory_request` (" +
        "`TenantId` VARBINARY(512) NOT NULL, `RequestId` VARBINARY(512) NOT NULL, " +
        "`WarehouseId` VARBINARY(512) NOT NULL, `Sku` VARBINARY(1024) NOT NULL, " +
        "`RequestedQuantity` BIGINT NOT NULL, `Status` VARCHAR(16) NOT NULL, " +
        "`QuantityAfter` BIGINT NULL, `CreatedAtUtc` DATETIME(6) NOT NULL, " +
        "PRIMARY KEY (`TenantId`, `RequestId`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
    }, InventorySchema.GetCreateStatements(InventoryDatabase.MySql));

    /// <summary>
    /// 验证 SQLite 库存表显式使用精确文本比较。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenSqlite_ShouldMatchCompleteSql() => Assert.Equal(new[]
    {
        "CREATE TABLE IF NOT EXISTS bing_inventory_stock (" +
        "TenantId TEXT COLLATE BINARY NOT NULL, WarehouseId TEXT COLLATE BINARY NOT NULL, " +
        "Sku TEXT COLLATE BINARY NOT NULL, Quantity INTEGER NOT NULL CHECK (Quantity >= 0), " +
        "PRIMARY KEY (TenantId, WarehouseId, Sku))",
        "CREATE TABLE IF NOT EXISTS bing_inventory_request (" +
        "TenantId TEXT COLLATE BINARY NOT NULL, RequestId TEXT COLLATE BINARY NOT NULL, " +
        "WarehouseId TEXT COLLATE BINARY NOT NULL, Sku TEXT COLLATE BINARY NOT NULL, " +
        "RequestedQuantity INTEGER NOT NULL, Status TEXT NOT NULL, " +
        "QuantityAfter INTEGER NULL, CreatedAtUtc TEXT NOT NULL, PRIMARY KEY (TenantId, RequestId))"
    }, InventorySchema.GetCreateStatements(InventoryDatabase.Sqlite));

    /// <summary>
    /// 验证未知数据库语法被拒绝。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenUnknown_ShouldReject() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        InventorySchema.GetCreateStatements((InventoryDatabase)99));
}
