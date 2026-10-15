namespace Bing.Data.Events.Ordering;

/// <summary>
/// 提供有序事件存储的建表脚本。
/// </summary>
/// <remarks>部署时执行兼容性迁移，不在应用启动中自动修改生产数据库。</remarks>
public static class OrderedEventSchema
{
    /// <summary>
    /// 获取建表脚本。
    /// </summary>
    public static IReadOnlyList<string> GetCreateStatements(OrderedEventDatabase database)
    {
        var mysql = database == OrderedEventDatabase.MySql;
        if (!mysql && database != OrderedEventDatabase.Sqlite)
            throw new ArgumentOutOfRangeException(nameof(database));
        var key = mysql ? "varchar(64) CHARACTER SET ascii COLLATE ascii_bin" : "TEXT";
        var text = mysql ? "longtext CHARACTER SET utf8mb4" : "TEXT";
        var integer = mysql ? "bigint" : "INTEGER";
        var timestamp = mysql ? "datetime(6)" : "TEXT";
        var suffix = mysql ? " ENGINE=InnoDB" : string.Empty;
        var inbox = $"CREATE TABLE IF NOT EXISTS bing_event_inbox (ConsumerKey {key} NOT NULL, StreamKey {key} NOT NULL, " +
                    $"EventKey {key} NOT NULL, EventId {text} NOT NULL, Sequence {integer} NOT NULL, Payload {text} NOT NULL, " +
                    $"ContractVersion {text} NOT NULL, CreatedAtUtc {timestamp} NOT NULL, Status INTEGER NOT NULL, Attempts INTEGER NOT NULL, LastError {text} NOT NULL, " +
                    "PRIMARY KEY (ConsumerKey, StreamKey, Sequence), UNIQUE (ConsumerKey, EventKey)" +
                    (mysql ? ", KEY IX_bing_event_inbox_pending_age (ConsumerKey, Status, CreatedAtUtc)" : string.Empty) + ")" + suffix;
        var archive = $"CREATE TABLE IF NOT EXISTS bing_event_inbox_archive (ConsumerKey {key} NOT NULL, StreamKey {key} NOT NULL, " +
                      $"EventKey {key} NOT NULL, EventId {text} NOT NULL, Sequence {integer} NOT NULL, Payload {text} NOT NULL, " +
                      $"ContractVersion {text} NOT NULL, CreatedAtUtc {timestamp} NOT NULL, ArchivedAtUtc {timestamp} NOT NULL, " +
                      "PRIMARY KEY (ConsumerKey, StreamKey, Sequence), UNIQUE (ConsumerKey, EventKey)" +
                      (mysql ? ", KEY IX_bing_event_inbox_archive_time (ConsumerKey, ArchivedAtUtc)" : string.Empty) + ")" + suffix;
        var statements = new List<string>
        {
            $"CREATE TABLE IF NOT EXISTS bing_event_sequence (StreamKey {key} NOT NULL PRIMARY KEY, LastSequence {integer} NOT NULL){suffix}",
            $"CREATE TABLE IF NOT EXISTS bing_event_cursor (ConsumerKey {key} NOT NULL, StreamKey {key} NOT NULL, " +
            $"Consumer {text} NOT NULL, TenantId {text} NOT NULL, StreamName {text} NOT NULL, EntityKey {text} NOT NULL, " +
            $"LastSequence {integer} NOT NULL, PRIMARY KEY (ConsumerKey, StreamKey)){suffix}",
            $"CREATE TABLE IF NOT EXISTS bing_event_consumer_capacity (ConsumerKey {key} NOT NULL PRIMARY KEY, PendingCount {integer} NOT NULL){suffix}",
            inbox,
            archive
        };
        if (!mysql)
        {
            statements.Add("CREATE INDEX IF NOT EXISTS IX_bing_event_inbox_pending_age ON bing_event_inbox (ConsumerKey, Status, CreatedAtUtc)");
            statements.Add("CREATE INDEX IF NOT EXISTS IX_bing_event_inbox_archive_time ON bing_event_inbox_archive (ConsumerKey, ArchivedAtUtc)");
        }
        return statements.AsReadOnly();
    }
}
