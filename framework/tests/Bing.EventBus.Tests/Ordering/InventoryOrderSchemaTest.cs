using Bing.Data.Inventory;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证整单首次部署 SQL 的完整输出。
/// </summary>
public sealed class InventoryOrderSchemaTest
{
    /// <summary>
    /// 验证 SQLite 整单表使用二进制身份。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenSqlite_ShouldMatchCompleteSql()
    {
        var statements = InventoryOrderSchema.GetCreateStatements(InventoryDatabase.Sqlite);
        Assert.Equal(2, statements.Count);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_inventory_order (" +
            "TenantId BLOB NOT NULL, OrderId BLOB NOT NULL, Fingerprint TEXT NOT NULL, " +
            "Status TEXT NOT NULL, ResultJson TEXT NOT NULL, PRIMARY KEY (TenantId, OrderId))",
            statements[0]);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_inventory_ordered_projection (" +
            "Consumer TEXT COLLATE BINARY NOT NULL, TenantId BLOB NOT NULL, OrderId BLOB NOT NULL, " +
            "Sequence INTEGER NOT NULL, EventId TEXT COLLATE BINARY NOT NULL, Status TEXT NOT NULL, " +
            "PayloadJson TEXT NOT NULL, ProcessedAtUtc TEXT NOT NULL, " +
            "PRIMARY KEY (Consumer, TenantId, OrderId, Sequence), UNIQUE (Consumer, EventId))",
            statements[1]);
    }

    /// <summary>
    /// 验证 MySQL 整单表使用事务引擎和二进制身份。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenMySql_ShouldMatchCompleteSql()
    {
        var statements = InventoryOrderSchema.GetCreateStatements(InventoryDatabase.MySql);
        Assert.Equal(2, statements.Count);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS `bing_inventory_order` (" +
            "`TenantId` VARBINARY(512) NOT NULL, `OrderId` VARBINARY(512) NOT NULL, " +
            "`Fingerprint` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "`Status` VARCHAR(16) NOT NULL, `ResultJson` LONGTEXT NOT NULL, " +
            "PRIMARY KEY (`TenantId`, `OrderId`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            statements[0]);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS `bing_inventory_ordered_projection` (" +
            "`Consumer` VARCHAR(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "`TenantId` VARBINARY(512) NOT NULL, `OrderId` VARBINARY(512) NOT NULL, " +
            "`Sequence` BIGINT NOT NULL, `EventId` VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "`Status` VARCHAR(32) NOT NULL, `PayloadJson` LONGTEXT NOT NULL, " +
            "`ProcessedAtUtc` DATETIME(6) NOT NULL, " +
            "PRIMARY KEY (`Consumer`, `TenantId`, `OrderId`, `Sequence`), " +
            "UNIQUE KEY `UX_bing_inventory_ordered_projection_event` (`Consumer`, `EventId`)) " +
            "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            statements[1]);
    }

    /// <summary>
    /// 验证未知数据库类型不会返回默认语法。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenUnknown_ShouldReject() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        InventoryOrderSchema.GetCreateStatements((InventoryDatabase)99));
}
