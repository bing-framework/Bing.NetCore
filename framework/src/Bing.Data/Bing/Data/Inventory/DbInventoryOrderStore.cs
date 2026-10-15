using System.Data.Common;
using System.Data;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Data.Inventory;

/// <summary>
/// 使用数据库唯一身份维护整单库存结果。
/// </summary>
public sealed class DbInventoryOrderStore : IInventoryOrderStore
{
    /// <summary>
    /// 当前数据库的语法类型。
    /// </summary>
    private readonly InventoryDatabase _database;

    /// <summary>
    /// 在同一事务中执行单项扣减的存储。
    /// </summary>
    private readonly IInventoryStore _inventory;

    /// <summary>
    /// 当前数据库的整单结果表名。
    /// </summary>
    private readonly string _table;

    /// <summary>
    /// 初始化一个 <see cref="DbInventoryOrderStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <param name="inventory">与当前数据库一致的单项库存存储。</param>
    public DbInventoryOrderStore(InventoryDatabase database, IInventoryStore inventory)
    {
        if (database != InventoryDatabase.MySql && database != InventoryDatabase.Sqlite)
            throw new ArgumentOutOfRangeException(nameof(database));
        _database = database;
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _table = database == InventoryDatabase.MySql ? "`bing_inventory_order`" : "bing_inventory_order";
    }

    /// <inheritdoc />
    public Task<InventoryOrderResult> FindAsync(DbConnection connection, InventoryOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (connection.State != ConnectionState.Open)
            throw new ArgumentException("整单结果查询需要已打开的独立连接。", nameof(connection));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        return ReadResultAsync(connection, null, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryOrderResult> ExecuteAsync(DbTransaction transaction, InventoryOrderRequest request,
        Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> onCompleted,
        CancellationToken cancellationToken = default)
    {
        if (transaction?.Connection == null)
            throw new ArgumentException("整单操作必须提供关联连接的活动事务。", nameof(transaction));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (onCompleted == null)
            throw new ArgumentNullException(nameof(onCompleted));
        cancellationToken.ThrowIfCancellationRequested();
        // 插入唯一身份直接参与竞争；不以先查后写保证幂等。
        try
        {
            using var insert = CreateCommand(transaction,
                $"INSERT INTO {_table} (TenantId, OrderId, Fingerprint, Status, ResultJson) " +
                "VALUES (@tenant, @order, @fingerprint, 'Pending', '[]')", request);
            AddParameter(insert, "@fingerprint", request.Fingerprint);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception) when (IsDuplicateKey(exception))
        {
            return await ReadExistingAsync(transaction, request, cancellationToken).ConfigureAwait(false);
        }

        var savepoint = "bing_inventory_" + Guid.NewGuid().ToString("N");
        await ExecuteSqlAsync(transaction, "SAVEPOINT " + savepoint, cancellationToken).ConfigureAwait(false);
        var remaining = new List<long?>(request.Lines.Count);
        var status = InventoryDeductionStatus.Applied;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await _inventory.TryDeductAsync(transaction, request.CreateDeduction(index), cancellationToken)
                .ConfigureAwait(false);
            if (item.Duplicate || item.Status == InventoryDeductionStatus.Pending)
                throw new InvalidOperationException("整单内部请求已被占用或未完成，必须回滚当前事务。");
            if (!item.Applied)
            {
                await ExecuteSqlAsync(transaction, "ROLLBACK TO SAVEPOINT " + savepoint, cancellationToken)
                    .ConfigureAwait(false);
                remaining.Clear();
                status = InventoryDeductionStatus.Insufficient;
                break;
            }
            remaining.Add(item.RemainingQuantity);
        }
        await ExecuteSqlAsync(transaction, "RELEASE SAVEPOINT " + savepoint, cancellationToken).ConfigureAwait(false);
        var result = new InventoryOrderResult(status, false, remaining);
        await onCompleted(transaction, result, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        using var update = CreateCommand(transaction,
            $"UPDATE {_table} SET Status = @status, ResultJson = @result " +
            "WHERE TenantId = @tenant AND OrderId = @order AND Status = 'Pending'", request);
        AddParameter(update, "@status", status.ToString());
        AddParameter(update, "@result", new JArray(remaining).ToString(Formatting.None));
        if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("整单首次结果未能写入，必须回滚当前事务。");
        return result;
    }

    /// <summary>
    /// 读取唯一身份冲突对应的首次结果。
    /// </summary>
    /// <param name="transaction">当前业务事务。</param>
    /// <param name="request">需要核对的请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已完成的首次结果。</returns>
    private async Task<InventoryOrderResult> ReadExistingAsync(DbTransaction transaction,
        InventoryOrderRequest request, CancellationToken cancellationToken)
    {
        return await ReadResultAsync(transaction.Connection, transaction, request, cancellationToken)
            .ConfigureAwait(false) ??
            throw new InvalidOperationException("唯一冲突后无法读取整单结果，必须回滚当前事务。");
    }

    /// <summary>
    /// 核对请求内容并读取持久化的首次结果。
    /// </summary>
    /// <param name="connection">当前数据库连接。</param>
    /// <param name="transaction">唯一冲突恢复所用事务；独立查询时为 null。</param>
    /// <param name="request">原始整单请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>首次结果；没有可见记录时返回 null。</returns>
    private async Task<InventoryOrderResult> ReadResultAsync(DbConnection connection, DbTransaction transaction,
        InventoryOrderRequest request, CancellationToken cancellationToken)
    {
        var suffix = transaction != null && _database == InventoryDatabase.MySql ? " FOR UPDATE" : string.Empty;
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = $"SELECT Fingerprint, Status, ResultJson FROM {_table} " +
                             "WHERE TenantId = @tenant AND OrderId = @order" + suffix;
        AddParameter(select, "@tenant", Encoding.UTF8.GetBytes(request.TenantId));
        AddParameter(select, "@order", Encoding.UTF8.GetBytes(request.OrderId));
        using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        if (!string.Equals(reader.GetString(0), request.Fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("整单请求标识已被不同业务内容占用。");
        if (!Enum.TryParse(reader.GetString(1), out InventoryDeductionStatus status) ||
            (status != InventoryDeductionStatus.Applied && status != InventoryDeductionStatus.Insufficient))
            throw new InvalidOperationException("整单结果尚未完成或状态无效。");
        var quantities = JArray.Parse(reader.GetString(2)).Select(value => value.Type == JTokenType.Null
            ? (long?)null : value.Value<long>()).ToArray();
        if (status == InventoryDeductionStatus.Applied && quantities.Length != request.Lines.Count ||
            status == InventoryDeductionStatus.Insufficient && quantities.Length != 0)
            throw new InvalidOperationException("整单持久化结果与请求明细不一致。");
        return new InventoryOrderResult(status, true, quantities);
    }

    /// <summary>
    /// 执行当前业务事务的保存点语句。
    /// </summary>
    /// <param name="transaction">当前业务事务。</param>
    /// <param name="sql">仅由框架生成的保存点语句。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task ExecuteSqlAsync(DbTransaction transaction, string sql,
        CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建绑定整单身份的数据库命令。
    /// </summary>
    /// <param name="transaction">当前业务事务。</param>
    /// <param name="sql">参数化 SQL。</param>
    /// <param name="request">整单身份。</param>
    /// <returns>由调用方法释放的数据库命令。</returns>
    private static DbCommand CreateCommand(DbTransaction transaction, string sql, InventoryOrderRequest request)
    {
        var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@tenant", Encoding.UTF8.GetBytes(request.TenantId));
        AddParameter(command, "@order", Encoding.UTF8.GetBytes(request.OrderId));
        return command;
    }

    /// <summary>
    /// 添加不依赖数据库驱动的命令参数。
    /// </summary>
    /// <param name="command">当前命令。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="value">参数值。</param>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// 识别当前支持驱动的唯一键冲突。
    /// </summary>
    /// <param name="exception">驱动异常。</param>
    /// <returns>仅 MySQL 1062 或 SQLite 唯一键冲突时返回 true。</returns>
    private static bool IsDuplicateKey(DbException exception)
    {
        var type = exception.GetType();
        return type.GetProperty("Number")?.GetValue(exception) is int number && number == 1062 ||
               type.GetProperty("SqliteExtendedErrorCode")?.GetValue(exception) is int code &&
               (code == 1555 || code == 2067);
    }
}
