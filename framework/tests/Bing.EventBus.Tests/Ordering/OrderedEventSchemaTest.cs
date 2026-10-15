using System.Text;
using Microsoft.Data.Sqlite;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件建表脚本的完整 SQL 契约。
/// </summary>
public sealed class OrderedEventSchemaTest
{
    /// <summary>
    /// 验证 MySQL 收件箱在建表时包含年龄字段和查询索引。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenMySql_ShouldIncludePendingAgeIndex()
    {
        var statements = Bing.Data.Events.Ordering.OrderedEventSchema
            .GetCreateStatements(Bing.Data.Events.Ordering.OrderedEventDatabase.MySql);

        Assert.Equal(5, statements.Count);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_event_inbox (ConsumerKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, StreamKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "EventKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, EventId longtext CHARACTER SET utf8mb4 NOT NULL, Sequence bigint NOT NULL, Payload longtext CHARACTER SET utf8mb4 NOT NULL, " +
            "ContractVersion longtext CHARACTER SET utf8mb4 NOT NULL, CreatedAtUtc datetime(6) NOT NULL, Status INTEGER NOT NULL, Attempts INTEGER NOT NULL, LastError longtext CHARACTER SET utf8mb4 NOT NULL, " +
            "PRIMARY KEY (ConsumerKey, StreamKey, Sequence), UNIQUE (ConsumerKey, EventKey), KEY IX_bing_event_inbox_pending_age (ConsumerKey, Status, CreatedAtUtc)) ENGINE=InnoDB",
            statements[3]);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS bing_event_inbox_archive (ConsumerKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, StreamKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, " +
            "EventKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, EventId longtext CHARACTER SET utf8mb4 NOT NULL, Sequence bigint NOT NULL, Payload longtext CHARACTER SET utf8mb4 NOT NULL, " +
            "ContractVersion longtext CHARACTER SET utf8mb4 NOT NULL, CreatedAtUtc datetime(6) NOT NULL, ArchivedAtUtc datetime(6) NOT NULL, " +
            "PRIMARY KEY (ConsumerKey, StreamKey, Sequence), UNIQUE (ConsumerKey, EventKey), KEY IX_bing_event_inbox_archive_time (ConsumerKey, ArchivedAtUtc)) ENGINE=InnoDB",
            statements[4]);
    }

    /// <summary>
    /// 验证 SQLite 使用独立的幂等索引脚本。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenSqlite_ShouldAddPendingAgeIndex()
    {
        var statements = Bing.Data.Events.Ordering.OrderedEventSchema
            .GetCreateStatements(Bing.Data.Events.Ordering.OrderedEventDatabase.Sqlite);

        Assert.Equal(7, statements.Count);
        Assert.Equal(
            "CREATE INDEX IF NOT EXISTS IX_bing_event_inbox_pending_age ON bing_event_inbox (ConsumerKey, Status, CreatedAtUtc)",
            statements[5]);
        Assert.Equal(
            "CREATE INDEX IF NOT EXISTS IX_bing_event_inbox_archive_time ON bing_event_inbox_archive (ConsumerKey, ArchivedAtUtc)",
            statements[6]);
    }

    /// <summary>
    /// 验证 SQLite 容量迁移会回填已有收件箱的未完成数量。
    /// </summary>
    [Fact]
    public async Task ConsumerCapacityMigration_WhenExistingInbox_ShouldBackfillPendingCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var statements = Bing.Data.Events.Ordering.OrderedEventSchema
            .GetCreateStatements(Bing.Data.Events.Ordering.OrderedEventDatabase.Sqlite);
        for (var index = 0; index < statements.Count; index++)
        {
            if (index == 2)
                continue;
            await ExecuteAsync(connection, statements[index]);
        }

        await ExecuteAsync(connection,
            "INSERT INTO bing_event_inbox " +
            "(ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, Status, Attempts, LastError) " +
            "VALUES ('consumer', 'stream-1', 'event-1', 'event-1', 1, '{}', 'v1', '2026-01-01T00:00:00Z', 0, 0, ''), " +
            "('consumer', 'stream-2', 'event-2', 'event-2', 1, '{}', 'v1', '2026-01-01T00:00:00Z', 1, 1, 'failed'), " +
            "('consumer', 'stream-3', 'event-3', 'event-3', 1, '{}', 'v1', '2026-01-01T00:00:00Z', 2, 0, '')");

        var root = FindRepositoryRoot();
        var migrationPath = Path.Combine(root, "deploy", "migrations", "ordered-events",
            "003-consumer-capacity.sqlite.sql");
        var migration = File.ReadAllText(migrationPath, Encoding.UTF8);
        await ExecuteAsync(connection, migration);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PendingCount FROM bing_event_consumer_capacity WHERE ConsumerKey = 'consumer'";
        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    /// <summary>
    /// 执行 SQLite 测试语句。
    /// </summary>
    /// <param name="connection">当前 SQLite 连接。</param>
    /// <param name="sql">待执行的 SQL。</param>
    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 查找当前仓库根目录。
    /// </summary>
    /// <returns>包含 global.json 的目录路径。</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("未找到仓库根目录。");
    }
}
