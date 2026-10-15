using System.Data.Common;
using System.Globalization;

namespace Bing.Events.Consumption;

/// <summary>
/// 使用数据库唯一键记录事务内的事件消费。
/// </summary>
public sealed class EventConsumptionStore : IEventConsumptionStore, IEventConsumptionRetentionStore
{
    /// <summary>
    /// MySQL 首次消费写入语句。
    /// </summary>
    private const string MySqlInsertSql =
        "INSERT INTO `bing_event_consumption` (`Consumer`, `EventId`, `ProcessedAtUtc`) " +
        "VALUES (@consumer, @eventId, @processedAtUtc)";

    /// <summary>
    /// SQLite 首次消费写入语句。
    /// </summary>
    private const string SqliteInsertSql =
        "INSERT INTO bing_event_consumption (Consumer, EventId, ProcessedAtUtc) " +
        "VALUES (@consumer, @eventId, @processedAtUtc) ON CONFLICT (Consumer, EventId) DO NOTHING";

    /// <summary>
    /// MySQL 收据清理语句。
    /// </summary>
    private const string MySqlDeleteSql =
        "DELETE FROM `bing_event_consumption` WHERE `Consumer` = @consumer " +
        "AND `ProcessedAtUtc` < @cutoffUtc ORDER BY `ProcessedAtUtc`, `EventId` LIMIT @batchSize";

    /// <summary>
    /// SQLite 收据清理语句。
    /// </summary>
    private const string SqliteDeleteSql =
        "DELETE FROM bing_event_consumption WHERE rowid IN (SELECT rowid FROM bing_event_consumption " +
        "WHERE Consumer = @consumer AND ProcessedAtUtc < @cutoffUtc " +
        "ORDER BY ProcessedAtUtc, EventId LIMIT @batchSize)";

    /// <summary>
    /// 当前实例使用的数据库语法。
    /// </summary>
    private readonly EventConsumptionDatabase _database;

    /// <summary>
    /// 当前实例使用的插入语句。
    /// </summary>
    private readonly string _insertSql;

    /// <summary>
    /// 当前实例使用的收据清理语句。
    /// </summary>
    private readonly string _deleteSql;

    /// <summary>
    /// 初始化一个 <see cref="EventConsumptionStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    public EventConsumptionStore(EventConsumptionDatabase database)
    {
        _insertSql = database switch
        {
            EventConsumptionDatabase.MySql => MySqlInsertSql,
            EventConsumptionDatabase.Sqlite => SqliteInsertSql,
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "不支持的事件消费数据库类型。")
        };
        _deleteSql = database switch
        {
            EventConsumptionDatabase.MySql => MySqlDeleteSql,
            EventConsumptionDatabase.Sqlite => SqliteDeleteSql,
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "不支持的事件消费数据库类型。")
        };
        _database = database;
    }

    /// <inheritdoc />
    public async Task<bool> TryRecordAsync(DbTransaction transaction, string consumer, string eventId,
        CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        ValidateKey(consumer, 200, nameof(consumer));
        ValidateKey(eventId, 128, nameof(eventId));
        cancellationToken.ThrowIfCancellationRequested();

        var connection = transaction.Connection ??
            throw new InvalidOperationException("事件消费事务未关联数据库连接。");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _insertSql;
        AddParameter(command, "@consumer", consumer);
        AddParameter(command, "@eventId", eventId);
        var processedAtUtc = DateTime.UtcNow;
        AddParameter(command, "@processedAtUtc", _database == EventConsumptionDatabase.MySql
            ? processedAtUtc
            : processedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (DbException exception) when (_database == EventConsumptionDatabase.MySql && IsMySqlDuplicateKey(exception))
        {
            // 当前读确认目标收据已存在；其他唯一约束冲突仍保留原始数据库异常。
            using var verify = connection.CreateCommand();
            verify.Transaction = transaction;
            verify.CommandText = "SELECT 1 FROM bing_event_consumption " +
                "WHERE BINARY Consumer = BINARY @consumer AND BINARY EventId = BINARY @eventId FOR UPDATE";
            AddParameter(verify, "@consumer", consumer);
            AddParameter(verify, "@eventId", eventId);
            if (await verify.ExecuteScalarAsync(cancellationToken) != null)
                return false;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        ValidateKey(consumer, 200, nameof(consumer));
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "清理批量必须位于 1 到 10000 之间。");
        cancellationToken.ThrowIfCancellationRequested();

        var connection = transaction.Connection ??
            throw new InvalidOperationException("事件消费事务未关联数据库连接。");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _deleteSql;
        AddParameter(command, "@consumer", consumer);
        AddParameter(command, "@cutoffUtc", _database == EventConsumptionDatabase.MySql
            ? cutoffUtc
            : cutoffUtc.ToString("O", CultureInfo.InvariantCulture));
        AddParameter(command, "@batchSize", batchSize);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 识别受支持 MySQL 驱动的重复键异常。
    /// </summary>
    /// <remarks>按调用读取驱动元数据，不增加驱动依赖或持有类型的静态缓存。</remarks>
    private static bool IsMySqlDuplicateKey(DbException exception)
    {
        var type = exception.GetType();
        return type.FullName is "MySqlConnector.MySqlException" or "MySql.Data.MySqlClient.MySqlException" &&
            type.GetProperty("Number")?.GetValue(exception) is int number && number == 1062;
    }

    /// <summary>
    /// 验证消费标识满足固定表字段约束。
    /// </summary>
    private static void ValidateKey(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("消费标识不能为空。", name);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("消费标识不能包含首尾空白字符。", name);
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(name, $"消费标识不能超过 {maxLength} 个字符。");
    }

    /// <summary>
    /// 向插入命令添加参数。
    /// </summary>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
