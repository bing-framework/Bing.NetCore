namespace Bing.Data.Inventory;

/// <summary>
/// 生成库存及请求记录的首次部署表结构。
/// </summary>
public static class InventorySchema
{
    /// <summary>
    /// 获取指定数据库的建表语句。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <returns>库存表和请求幂等表的建表语句。</returns>
    /// <remarks>身份按 UTF-8 精确比较，MySQL 显式使用 InnoDB；已有表须由部署迁移，不在注册或扣减时修改。</remarks>
    public static IReadOnlyList<string> GetCreateStatements(InventoryDatabase database) => database switch
    {
        InventoryDatabase.MySql => new[]
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
        },
        InventoryDatabase.Sqlite => new[]
        {
            "CREATE TABLE IF NOT EXISTS bing_inventory_stock (" +
            "TenantId TEXT COLLATE BINARY NOT NULL, WarehouseId TEXT COLLATE BINARY NOT NULL, " +
            "Sku TEXT COLLATE BINARY NOT NULL, " +
            "Quantity INTEGER NOT NULL CHECK (Quantity >= 0), " +
            "PRIMARY KEY (TenantId, WarehouseId, Sku))",
            "CREATE TABLE IF NOT EXISTS bing_inventory_request (" +
            "TenantId TEXT COLLATE BINARY NOT NULL, RequestId TEXT COLLATE BINARY NOT NULL, " +
            "WarehouseId TEXT COLLATE BINARY NOT NULL, Sku TEXT COLLATE BINARY NOT NULL, " +
            "RequestedQuantity INTEGER NOT NULL, Status TEXT NOT NULL, " +
            "QuantityAfter INTEGER NULL, CreatedAtUtc TEXT NOT NULL, " +
            "PRIMARY KEY (TenantId, RequestId))"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "不支持的库存数据库类型。")
    };
}
