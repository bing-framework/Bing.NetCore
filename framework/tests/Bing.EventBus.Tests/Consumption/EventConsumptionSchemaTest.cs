using Bing.Events.Consumption;

namespace Bing.EventBus.Tests.Consumption;

/// <summary>
/// 验证事件消费收据表结构脚本。
/// </summary>
public sealed class EventConsumptionSchemaTest
{
    /// <summary>
    /// 验证 MySQL 收据表使用二进制标识和留存索引。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenMySql_ShouldUseBinaryKeysAndRetentionIndex()
    {
        var statements = EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.MySql);

        Assert.Single(statements);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_event_consumption (Consumer varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "EventId varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, ProcessedAtUtc datetime(6) NOT NULL, " +
            "PRIMARY KEY (Consumer, EventId), KEY IX_bing_event_consumption_retention (Consumer, ProcessedAtUtc, EventId)) ENGINE=InnoDB",
            statements[0]);
    }

    /// <summary>
    /// 验证 SQLite 收据表和留存索引分步创建。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenSqlite_ShouldCreateTableAndRetentionIndex()
    {
        var statements = EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.Sqlite);

        Assert.Equal(2, statements.Count);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_event_consumption (Consumer TEXT COLLATE BINARY NOT NULL, " +
            "EventId TEXT COLLATE BINARY NOT NULL, ProcessedAtUtc TEXT NOT NULL, PRIMARY KEY (Consumer, EventId))",
            statements[0]);
        Assert.Equal(
            "CREATE INDEX IF NOT EXISTS IX_bing_event_consumption_retention ON bing_event_consumption (Consumer, ProcessedAtUtc, EventId)",
            statements[1]);
    }

    /// <summary>
    /// 验证不支持的数据库类型会被拒绝。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenDatabaseIsInvalid_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventConsumptionSchema.GetCreateStatements((EventConsumptionDatabase)99));
    }
}
