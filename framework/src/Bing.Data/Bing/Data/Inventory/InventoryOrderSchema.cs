namespace Bing.Data.Inventory;

/// <summary>
/// 提供整单库存结果表的首次部署语句。
/// </summary>
public static class InventoryOrderSchema
{
    /// <summary>
    /// 获取整单结果建表语句。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <returns>显式部署使用的整单结果建表语句。</returns>
    /// <remarks>身份以 UTF-8 二进制保存，避免默认排序规则合并大小写或尾空格；不自动迁移旧表。</remarks>
    public static IReadOnlyList<string> GetCreateStatements(InventoryDatabase database) => database switch
    {
        InventoryDatabase.MySql => new[]
        {
            "CREATE TABLE IF NOT EXISTS `bing_inventory_order` (" +
            "`TenantId` VARBINARY(512) NOT NULL, `OrderId` VARBINARY(512) NOT NULL, " +
            "`Fingerprint` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "`Status` VARCHAR(16) NOT NULL, `ResultJson` LONGTEXT NOT NULL, " +
            "PRIMARY KEY (`TenantId`, `OrderId`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS `bing_inventory_ordered_projection` (" +
            "`Consumer` VARCHAR(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "`TenantId` VARBINARY(512) NOT NULL, `OrderId` VARBINARY(512) NOT NULL, " +
            "`Sequence` BIGINT NOT NULL, `EventId` VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "`Status` VARCHAR(32) NOT NULL, `PayloadJson` LONGTEXT NOT NULL, " +
            "`ProcessedAtUtc` DATETIME(6) NOT NULL, " +
            "PRIMARY KEY (`Consumer`, `TenantId`, `OrderId`, `Sequence`), " +
            "UNIQUE KEY `UX_bing_inventory_ordered_projection_event` (`Consumer`, `EventId`)) " +
            "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
        },
        InventoryDatabase.Sqlite => new[]
        {
            "CREATE TABLE IF NOT EXISTS bing_inventory_order (" +
            "TenantId BLOB NOT NULL, OrderId BLOB NOT NULL, Fingerprint TEXT NOT NULL, " +
            "Status TEXT NOT NULL, ResultJson TEXT NOT NULL, PRIMARY KEY (TenantId, OrderId))",
            "CREATE TABLE IF NOT EXISTS bing_inventory_ordered_projection (" +
            "Consumer TEXT COLLATE BINARY NOT NULL, TenantId BLOB NOT NULL, OrderId BLOB NOT NULL, " +
            "Sequence INTEGER NOT NULL, EventId TEXT COLLATE BINARY NOT NULL, Status TEXT NOT NULL, " +
            "PayloadJson TEXT NOT NULL, ProcessedAtUtc TEXT NOT NULL, " +
            "PRIMARY KEY (Consumer, TenantId, OrderId, Sequence), UNIQUE (Consumer, EventId))"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(database))
    };
}
