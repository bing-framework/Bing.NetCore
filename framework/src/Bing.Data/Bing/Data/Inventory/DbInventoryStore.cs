using System.Data.Common;
using System.Text;

namespace Bing.Data.Inventory;

/// <summary>
/// 使用数据库条件更新和唯一请求键执行库存扣减。
/// </summary>
/// <remarks>借用调用方事务；MySQL 身份列须按显式迁移使用 UTF-8 二进制和 InnoDB，存储不自动升级表结构。</remarks>
public sealed class DbInventoryStore : IInventoryStore
{
    /// <summary>
    /// 无损读写 MySQL 二进制库存身份的编码器。
    /// </summary>
    private static readonly Encoding IdentityEncoding = new UTF8Encoding(false, true);

    /// <summary>
    /// 当前实例使用的数据库语法。
    /// </summary>
    private readonly InventoryDatabase _database;
    /// <summary>
    /// 登记请求的 SQL。
    /// </summary>
    private readonly string _insertRequestSql;
    /// <summary>
    /// 读取请求结果的 SQL。
    /// </summary>
    private readonly string _selectRequestSql;
    /// <summary>
    /// 余额条件更新的 SQL。
    /// </summary>
    private readonly string _updateStockSql;
    /// <summary>
    /// 读取扣减后余额的 SQL。
    /// </summary>
    private readonly string _selectStockSql;
    /// <summary>
    /// 写入请求结果的 SQL。
    /// </summary>
    private readonly string _updateRequestSql;

    /// <summary>
    /// 初始化一个 <see cref="DbInventoryStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    public DbInventoryStore(InventoryDatabase database)
    {
        _database = database;
        var names = database == InventoryDatabase.MySql
            ? ("`bing_inventory_request`", "`bing_inventory_stock`", " LIMIT 1")
            : database == InventoryDatabase.Sqlite
                ? ("bing_inventory_request", "bing_inventory_stock", " LIMIT 1")
                : throw new ArgumentOutOfRangeException(nameof(database), database, "不支持的库存数据库类型。");
        _insertRequestSql = $"INSERT INTO {names.Item1} (TenantId, RequestId, WarehouseId, Sku, RequestedQuantity, Status, QuantityAfter, CreatedAtUtc) " +
                            "VALUES (@tenant, @request, @warehouse, @sku, @quantity, @status, @remaining, @created)";
        _selectRequestSql = $"SELECT WarehouseId, Sku, RequestedQuantity, Status, QuantityAfter FROM {names.Item1} " +
                            $"WHERE TenantId = @tenant AND RequestId = @request{names.Item3}";
        _updateStockSql = $"UPDATE {names.Item2} SET Quantity = Quantity - @quantity " +
                          "WHERE TenantId = @tenant AND WarehouseId = @warehouse AND Sku = @sku AND Quantity >= @quantity";
        _selectStockSql = $"SELECT Quantity FROM {names.Item2} WHERE TenantId = @tenant AND WarehouseId = @warehouse AND Sku = @sku{names.Item3}";
        _updateRequestSql = $"UPDATE {names.Item1} SET Status = @status, QuantityAfter = @remaining " +
                            "WHERE TenantId = @tenant AND RequestId = @request";
    }

    /// <inheritdoc />
    public async Task<InventoryDeductionResult> TryDeductAsync(DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var connection = transaction.Connection ??
            throw new InvalidOperationException("库存事务未关联数据库连接。");
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await ReadRequestAsync(connection, transaction, request, cancellationToken);
        if (existing != null)
        {
            ValidateExisting(request, existing);
            return ToResult(existing, true);
        }

        try
        {
            await InsertRequestAsync(connection, transaction, request, cancellationToken);
        }
        catch (DbException exception) when (IsDuplicateKey(exception))
        {
            // 唯一约束检测使用当前版本；冲突后的查询也必须读取已提交版本，避免重复读旧快照遗漏结果。
            existing = await ReadRequestAsync(connection, transaction, request, cancellationToken, useCurrentRead: true);
            if (existing == null)
                throw;
            ValidateExisting(request, existing);
            return ToResult(existing, true);
        }

        var affected = await UpdateStockAsync(connection, transaction, request, cancellationToken);
        if (affected == 0)
        {
            await UpdateRequestAsync(connection, transaction, request, InventoryDeductionStatus.Insufficient,
                null, cancellationToken);
            return new InventoryDeductionResult(InventoryDeductionStatus.Insufficient, false, null);
        }

        var remaining = await ReadRemainingAsync(connection, transaction, request, cancellationToken);
        await UpdateRequestAsync(connection, transaction, request, InventoryDeductionStatus.Applied,
            remaining, cancellationToken);
        return new InventoryDeductionResult(InventoryDeductionStatus.Applied, false, remaining);
    }

    /// <summary>
    /// 读取当前事务可见的请求结果。
    /// </summary>
    /// <param name="connection">当前事务的数据库连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="request">需要查询的业务请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="useCurrentRead">是否在 MySQL 唯一冲突后使用锁定当前读。</param>
    /// <returns>请求记录；当前查询没有可见结果时返回 null。</returns>
    /// <remarks>初次查询保持普通读，避免对尚不存在的请求提前加间隙锁；仅冲突恢复使用当前读。</remarks>
    private async Task<RequestRecord> ReadRequestAsync(DbConnection connection, DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken, bool useCurrentRead = false)
    {
        var sql = useCurrentRead && _database == InventoryDatabase.MySql
            ? _selectRequestSql + " FOR UPDATE"
            : _selectRequestSql;
        using var command = CreateCommand(connection, transaction, sql);
        AddIdentityParameter(command, "@tenant", request.TenantId);
        AddIdentityParameter(command, "@request", request.RequestId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new RequestRecord(ReadIdentity(reader, 0), ReadIdentity(reader, 1), reader.GetInt64(2),
            reader.GetString(3), reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4));
    }

    /// <summary>
    /// 在当前事务登记待处理请求。
    /// </summary>
    /// <param name="connection">当前事务所属连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="request">待登记的库存请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task InsertRequestAsync(DbConnection connection, DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, _insertRequestSql);
        AddRequestParameters(command, request);
        AddParameter(command, "@status", InventoryDeductionStatus.Pending.ToString());
        AddParameter(command, "@remaining", DBNull.Value);
        AddParameter(command, "@created", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 使用余额条件更新库存。
    /// </summary>
    /// <param name="connection">当前事务所属连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="request">待执行的库存请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>条件更新影响的行数。</returns>
    private async Task<int> UpdateStockAsync(DbConnection connection, DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, _updateStockSql);
        AddRequestParameters(command, request);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取当前事务中的剩余库存。
    /// </summary>
    /// <param name="connection">当前事务所属连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="request">确定库存身份的请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>库存余额；没有对应库存行时返回 null。</returns>
    private async Task<long?> ReadRemainingAsync(DbConnection connection, DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, _selectStockSql);
        AddIdentityParameter(command, "@tenant", request.TenantId);
        AddIdentityParameter(command, "@warehouse", request.WarehouseId);
        AddIdentityParameter(command, "@sku", request.Sku);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
    }

    /// <summary>
    /// 写入请求的最终结果。
    /// </summary>
    /// <param name="connection">当前事务所属连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="request">已经登记的库存请求。</param>
    /// <param name="status">最终扣减状态。</param>
    /// <param name="remaining">成功扣减后的余额；不足时为 null。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task UpdateRequestAsync(DbConnection connection, DbTransaction transaction,
        InventoryDeductionRequest request, InventoryDeductionStatus status, long? remaining,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, _updateRequestSql);
        AddParameter(command, "@status", status.ToString());
        AddParameter(command, "@remaining", remaining ?? (object)DBNull.Value);
        AddIdentityParameter(command, "@tenant", request.TenantId);
        AddIdentityParameter(command, "@request", request.RequestId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("库存请求结果未能写入。");
    }

    /// <summary>
    /// 验证重复请求的业务身份一致。
    /// </summary>
    /// <param name="request">本次提交的原始请求。</param>
    /// <param name="existing">相同租户和请求标识对应的已存记录。</param>
    private static void ValidateExisting(InventoryDeductionRequest request, RequestRecord existing)
    {
        if (!string.Equals(request.WarehouseId, existing.WarehouseId, StringComparison.Ordinal) ||
            !string.Equals(request.Sku, existing.Sku, StringComparison.Ordinal) ||
            request.Quantity != existing.Quantity)
            throw new InvalidOperationException("库存请求唯一标识已被不同业务请求占用。");
    }

    /// <summary>
    /// 将数据库请求记录转换为对外结果。
    /// </summary>
    /// <param name="existing">当前可见的请求记录。</param>
    /// <param name="duplicate">是否复用首次结果。</param>
    /// <returns>保留首次扣减状态和余额的结果。</returns>
    private static InventoryDeductionResult ToResult(RequestRecord existing, bool duplicate) =>
        new(ParseStatus(existing.Status), duplicate, existing.Remaining);

    /// <summary>
    /// 解析持久化状态。
    /// </summary>
    /// <param name="value">数据库中的状态名称。</param>
    /// <returns>识别出的库存状态。</returns>
    private static InventoryDeductionStatus ParseStatus(string value) =>
        Enum.TryParse(value, true, out InventoryDeductionStatus status)
            ? status
            : throw new InvalidOperationException("库存请求状态无法识别。");

    /// <summary>
    /// 创建绑定当前事务的数据库命令。
    /// </summary>
    /// <param name="connection">当前事务所属连接。</param>
    /// <param name="transaction">调用方拥有的活动事务。</param>
    /// <param name="text">待执行的 SQL。</param>
    /// <returns>由调用方法释放的数据库命令。</returns>
    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, string text)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        return command;
    }

    /// <summary>
    /// 添加请求公共参数。
    /// </summary>
    /// <param name="command">当前事务命令。</param>
    /// <param name="request">参数来自的库存请求。</param>
    private void AddRequestParameters(DbCommand command, InventoryDeductionRequest request)
    {
        AddIdentityParameter(command, "@tenant", request.TenantId);
        AddIdentityParameter(command, "@request", request.RequestId);
        AddIdentityParameter(command, "@warehouse", request.WarehouseId);
        AddIdentityParameter(command, "@sku", request.Sku);
        AddParameter(command, "@quantity", request.Quantity);
    }

    /// <summary>
    /// 添加库存身份参数。
    /// </summary>
    /// <param name="command">当前事务命令。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="value">已验证的身份原值。</param>
    /// <remarks>MySQL 使用 UTF-8 字节，SQLite 使用 BINARY 排序规则的文本。</remarks>
    private void AddIdentityParameter(DbCommand command, string name, string value) =>
        AddParameter(command, name, _database == InventoryDatabase.MySql
            ? (object)IdentityEncoding.GetBytes(value)
            : value);

    /// <summary>
    /// 读取持久化库存身份。
    /// </summary>
    /// <param name="reader">当前请求记录读取器。</param>
    /// <param name="ordinal">身份列位置。</param>
    /// <returns>无损解码后的身份原值。</returns>
    private static string ReadIdentity(DbDataReader reader, int ordinal) =>
        reader.GetValue(ordinal) is byte[] bytes ? IdentityEncoding.GetString(bytes) : reader.GetString(ordinal);

    /// <summary>
    /// 向命令添加参数。
    /// </summary>
    /// <param name="command">当前事务命令。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="value">参数值；null 转换为数据库空值。</param>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// 识别当前数据库的请求唯一键冲突。
    /// </summary>
    /// <param name="exception">驱动返回的数据库异常。</param>
    /// <returns>属于当前 Provider 的唯一约束冲突时为 true，否则为 false。</returns>
    private bool IsDuplicateKey(DbException exception)
    {
        var type = exception.GetType();
        if (_database == InventoryDatabase.MySql)
            return type.FullName is "MySqlConnector.MySqlException" or "MySql.Data.MySqlClient.MySqlException" &&
                   type.GetProperty("Number")?.GetValue(exception) is int number && number == 1062;
        return type.FullName == "Microsoft.Data.Sqlite.SqliteException" &&
               type.GetProperty("SqliteErrorCode")?.GetValue(exception) is int code && code == 19;
    }

    /// <summary>
    /// 当前查询读取的库存请求结果。
    /// </summary>
    private sealed class RequestRecord
    {
        /// <summary>
        /// 初始化一个 <see cref="RequestRecord" /> 类型的实例。
        /// </summary>
        /// <param name="warehouseId">持久化的精确仓库身份。</param>
        /// <param name="sku">持久化的精确 SKU 身份。</param>
        /// <param name="quantity">首次请求的扣减数量。</param>
        /// <param name="status">首次请求的状态名称。</param>
        /// <param name="remaining">首次扣减后的余额。</param>
        internal RequestRecord(string warehouseId, string sku, long quantity, string status, long? remaining)
        {
            WarehouseId = warehouseId;
            Sku = sku;
            Quantity = quantity;
            Status = status;
            Remaining = remaining;
        }
        /// <summary>
        /// 获取仓库标识。
        /// </summary>
        internal string WarehouseId { get; }
        /// <summary>
        /// 获取 SKU 标识。
        /// </summary>
        internal string Sku { get; }
        /// <summary>
        /// 获取请求数量。
        /// </summary>
        internal long Quantity { get; }
        /// <summary>
        /// 获取持久化状态。
        /// </summary>
        internal string Status { get; }
        /// <summary>
        /// 获取扣减后的剩余数量。
        /// </summary>
        internal long? Remaining { get; }
    }
}
