using System.Data;
using System.Data.Common;
using System.Text;
using Bing.Data.Events.Ordering;

namespace Bing.Data.Inventory;

/// <summary>
/// 保存有序库存完成通知的业务投影。
/// </summary>
/// <remarks>投影写入必须与有序收件箱游标使用同一数据库事务。</remarks>
public interface IInventoryOrderProjectionStore
{
    /// <summary>
    /// 保存一条有序库存完成投影。
    /// </summary>
    /// <param name="transaction">当前有序处理事务。</param>
    /// <param name="consumer">稳定的逻辑消费者名称。</param>
    /// <param name="stream">事件所属业务流。</param>
    /// <param name="message">当前事件。</param>
    /// <param name="status">库存整单状态。</param>
    /// <param name="payload">原始事件正文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示投影写入的异步操作。</returns>
    Task SaveAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        OrderedEventMessage message, string status, string payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 使用数据库原生 SQL 保存库存完成投影。
/// </summary>
public sealed class DbInventoryOrderProjectionStore : IInventoryOrderProjectionStore
{
    /// <summary>
    /// 当前数据库的投影表名。
    /// </summary>
    private readonly string _table;

    /// <summary>
    /// 初始化一个 <see cref="DbInventoryOrderProjectionStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    public DbInventoryOrderProjectionStore(InventoryDatabase database)
    {
        if (database != InventoryDatabase.MySql && database != InventoryDatabase.Sqlite)
            throw new ArgumentOutOfRangeException(nameof(database));
        _table = database == InventoryDatabase.MySql
            ? "`bing_inventory_ordered_projection`"
            : "bing_inventory_ordered_projection";
    }

    /// <inheritdoc />
    public async Task SaveAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        OrderedEventMessage message, string status, string payload,
        CancellationToken cancellationToken = default)
    {
        if (transaction?.Connection == null)
            throw new ArgumentException("库存投影必须使用关联连接的活动事务。", nameof(transaction));
        if (string.IsNullOrWhiteSpace(consumer) || consumer.Length > 200)
            throw new ArgumentException("逻辑消费者不能为空且不能超过 200 个字符。", nameof(consumer));
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(status) || status.Length > 32)
            throw new ArgumentException("库存状态不能为空且不能超过 32 个字符。", nameof(status));
        if (!string.Equals(status, "Applied", StringComparison.Ordinal) &&
            !string.Equals(status, "Insufficient", StringComparison.Ordinal))
            throw new ArgumentException("库存状态只允许为 Applied 或 Insufficient。", nameof(status));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (Encoding.UTF8.GetByteCount(payload) > 65536)
            throw new ArgumentException("库存事件正文不能超过 65536 字节。", nameof(payload));
        cancellationToken.ThrowIfCancellationRequested();

        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {_table} " +
                              "(Consumer, TenantId, OrderId, Sequence, EventId, Status, PayloadJson, ProcessedAtUtc) " +
                              "VALUES (@consumer, @tenant, @order, @sequence, @event, @status, @payload, @processed)";
        AddParameter(command, "@consumer", consumer);
        AddParameter(command, "@tenant", Encoding.UTF8.GetBytes(stream.TenantId));
        AddParameter(command, "@order", Encoding.UTF8.GetBytes(stream.EntityKey));
        AddParameter(command, "@sequence", message.Sequence);
        AddParameter(command, "@event", message.EventId);
        AddParameter(command, "@status", status);
        AddParameter(command, "@payload", payload);
        AddParameter(command, "@processed", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 添加数据库命令参数。
    /// </summary>
    /// <param name="command">当前数据库命令。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="value">参数值。</param>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
