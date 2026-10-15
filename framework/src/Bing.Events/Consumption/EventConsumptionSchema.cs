namespace Bing.Events.Consumption;

/// <summary>
/// 提供事件消费收据表的建表脚本。
/// </summary>
/// <remarks>
/// 运行期不会自动修改生产数据库；已有部署应使用受控迁移执行相同结构的变更。
/// </remarks>
public static class EventConsumptionSchema
{
    /// <summary>
    /// 获取指定数据库的建表脚本。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <returns>按执行顺序排列的建表和索引脚本。</returns>
    public static IReadOnlyList<string> GetCreateStatements(EventConsumptionDatabase database)
    {
        return database switch
        {
            EventConsumptionDatabase.MySql => new[]
            {
                "CREATE TABLE IF NOT EXISTS bing_event_consumption (Consumer varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                "EventId varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, ProcessedAtUtc datetime(6) NOT NULL, " +
                "PRIMARY KEY (Consumer, EventId), KEY IX_bing_event_consumption_retention (Consumer, ProcessedAtUtc, EventId)) ENGINE=InnoDB"
            },
            EventConsumptionDatabase.Sqlite => new[]
            {
                "CREATE TABLE IF NOT EXISTS bing_event_consumption (Consumer TEXT COLLATE BINARY NOT NULL, " +
                "EventId TEXT COLLATE BINARY NOT NULL, ProcessedAtUtc TEXT NOT NULL, PRIMARY KEY (Consumer, EventId))",
                "CREATE INDEX IF NOT EXISTS IX_bing_event_consumption_retention ON bing_event_consumption (Consumer, ProcessedAtUtc, EventId)"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "不支持的事件消费数据库类型。")
        };
    }
}
