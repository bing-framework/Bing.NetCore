using Bing.Extensions;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象基类 - 事务
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 绑定固定事务执行上下文。
    /// </summary>
    /// <param name="context">事务数据库上下文。</param>
    /// <param name="connection">事务连接。</param>
    /// <param name="transaction">事务。</param>
    /// <param name="lease">事务作用域执行租约。</param>
    internal void SetTransactionContext(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
        ISqlTransactionScopeLease lease)
    {
        ThrowIfTransactionScopeChildDisposed();
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (lease == null)
            throw new ArgumentNullException(nameof(lease));
        if (transaction.Connection == null)
            throw new InvalidOperationException("事务作用域事务必须关联数据库连接。");
        if (ReferenceEquals(transaction.Connection, connection) == false)
            throw new InvalidOperationException("事务作用域连接与事务连接不一致。");
        if (_transaction != null && ReferenceEquals(_transaction, transaction) == false)
            throw new InvalidOperationException("当前 Query 已绑定其他事务，不能覆盖事务资源。");
        EnsureConnectionCanBeReplaced(connection);
        ValidateExternalConnectionDatabaseIdentity(connection);

        var contextSnapshot = DatabaseContextSnapshot.Create(context);
        BindConnection(connection, SqlResourceOwnership.External, SqlConnectionSource.DataSource);
        Options.DatabaseType = contextSnapshot.DataSource?.DatabaseType ?? Options.DatabaseType;
        Options.SetDatabaseContext(contextSnapshot);
        _transactionScopeLease = lease;
        _transaction = transaction;
        _transactionId = lease.TransactionId;
        _transactionExecutionMode = lease.ExecutionMode;
        _transactionOwnership = SqlResourceOwnership.External;
    }

    /// <summary>
    /// 设置数据库事务并指定诊断事务标识。
    /// </summary>
    /// <param name="transaction">数据库事务。</param>
    /// <param name="transactionId">诊断事务标识。</param>
    private void BindExternalTransaction(IDbTransaction transaction, string transactionId)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        var transactionConnection = transaction.Connection ??
                                  throw new InvalidOperationException("外部事务必须关联数据库连接。");
        if (_transaction != null && _transactionOwnership == SqlResourceOwnership.Owned)
            throw new InvalidOperationException("当前 Query 已存在自有事务，不能绑定外部事务。");
        if (_transaction != null && ReferenceEquals(_transaction, transaction) == false)
            throw new InvalidOperationException("当前 Query 已绑定其他事务，不能覆盖事务资源。");
        if (_connection != null && ReferenceEquals(_connection, transactionConnection) == false)
            throw new InvalidOperationException("外部事务连接与 Query 连接不一致。");
        if (_connection == null)
            BindConnection(transactionConnection, SqlResourceOwnership.External, SqlConnectionSource.External);
        ValidateExternalConnectionDatabaseIdentity(transactionConnection);
        _transaction = transaction;
        _transactionId = transactionId ?? Guid.NewGuid().ToString("N");
        _transactionExecutionMode = null;
        _transactionOwnership = SqlResourceOwnership.External;
    }

    /// <summary>
    /// 获取内部执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    protected IDbTransaction GetExecutionTransaction() =>
        GetCurrentTransaction();

    /// <summary>
    /// 获取当前执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    private IDbTransaction GetCurrentTransaction()
    {
        EnsureExecutionAvailable();
        if (_externalTransactionResolver != null)
        {
            var transaction = _externalTransactionResolver.Invoke();
            if (ReferenceEquals(_transaction, transaction))
                return _transaction;
            if (_transaction != null && _transactionOwnership == SqlResourceOwnership.External)
                ReleaseTransaction();
            if (transaction == null)
                return _transaction;
            BindExternalTransaction(transaction, null);
            return _transaction;
        }
        if (_transaction != null)
            return _transaction;
        return null;
    }

    /// <summary>
    /// 获取查询事务。
    /// </summary>
    /// <returns>当前查询使用的事务；无需事务时返回 null。</returns>
    protected IDbTransaction GetQueryTransaction()
    {
        _transactionScopeLease?.EnsureActive();
        var transaction = GetExecutionTransaction();
        if (transaction != null)
            return transaction;
        var context = Options.GetDatabaseContext();
        if (context?.ReadPreference != SqlReadPreference.Primary)
            return null;
        if (context.DataSource?.PrimaryReadStrategy != PrimaryReadStrategy.Transaction)
            return null;
        var primaryReadTransaction = BeginOwnedTransaction();
        _primaryReadTransactionStarted = true;
        return primaryReadTransaction;
    }

    /// <summary>
    /// 异步获取查询事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前事务，不存在时返回 null。</returns>
    private protected async Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transactionScopeLease?.EnsureActive();
        cancellationToken.ThrowIfCancellationRequested();
        var transaction = GetExecutionTransaction();
        if (transaction != null)
            return transaction;
        var context = Options.GetDatabaseContext();
        if (context?.ReadPreference != SqlReadPreference.Primary ||
            context.DataSource?.PrimaryReadStrategy != PrimaryReadStrategy.Transaction)
            return null;
        var primaryReadTransaction = await BeginOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        _primaryReadTransactionStarted = true;
        return primaryReadTransaction;
    }

    /// <summary>
    /// 完成查询事务。
    /// </summary>
    protected void CompleteQueryTransaction()
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            CommitOwnedTransaction();
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>
    /// 异步完成 Query 内部事务。
    /// </summary>
    /// <param name="cancellationToken">提交事务使用的取消令牌。</param>
    private protected async Task CompleteQueryTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            await CommitOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>
    /// 回滚查询事务。
    /// </summary>
    protected void RollbackQueryTransaction()
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            RollbackOwnedTransaction();
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>
    /// 异步回滚 Query 内部事务。
    /// </summary>
    /// <remarks>清理阶段不得继承业务操作的取消令牌。</remarks>
    private protected async Task RollbackQueryTransactionAsync()
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            await RollbackOwnedTransactionAsync().ConfigureAwait(false);
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>
    /// 开始 Query 内部拥有的事务。
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>内部拥有的数据库事务。</returns>
    private IDbTransaction BeginOwnedTransaction(IsolationLevel? isolationLevel = null) =>
        BeginTransactionImpl(isolationLevel);

    /// <summary>
    /// 异步开始 Query 内部拥有的事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>内部拥有的数据库事务。</returns>
    private Task<IDbTransaction> BeginOwnedTransactionAsync(CancellationToken cancellationToken,
        IsolationLevel? isolationLevel = null) => BeginTransactionImplAsync(isolationLevel, cancellationToken);

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <returns>已开始或复用的内部事务。</returns>
    private IDbTransaction BeginTransactionImpl(IsolationLevel? isolationLevel)
    {
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (_transaction != null)
            {
                EnsureOwnedTransaction("开始");
                return _transaction;
            }
            EnsureTransactionsSupported();
            var connection = GetExecutionConnection();
            if (connection.State == ConnectionState.Closed)
                connection.Open();
            _transaction = isolationLevel == null
                ? connection.BeginTransaction()
                : connection.BeginTransaction(isolationLevel.SafeValue());
            _transactionId = Guid.NewGuid().ToString("N");
            _transactionOwnership = SqlResourceOwnership.Owned;
            return _transaction;
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        ReleaseOwnedTransactionResources(cleanupExceptions);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return null;
    }

    /// <summary>
    /// 异步开始事务。
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示已开始或复用内部事务的异步操作。</returns>
    private async Task<IDbTransaction> BeginTransactionImplAsync(IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (_transaction != null)
            {
                EnsureOwnedTransaction("开始");
                return _transaction;
            }
            EnsureTransactionsSupported();
            var connection = GetExecutionConnection();
            if (connection.State == ConnectionState.Closed)
                await SqlTransactionAsyncAdapter.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var providerKey = GetCurrentProviderKey();
            var transactionCapabilities = GetCurrentProviderTransactionCapabilities();
            var transactionResult = await SqlTransactionAsyncAdapter.BeginWithModeAsync(connection,
                isolationLevel ?? IsolationLevel.ReadCommitted, cancellationToken, transactionCapabilities,
                providerKey).ConfigureAwait(false);
            _transaction = transactionResult.Result;
            _transactionExecutionMode = ToDiagnosticValue(transactionResult.Mode);
            _transactionId = Guid.NewGuid().ToString("N");
            _transactionOwnership = SqlResourceOwnership.Owned;
            return _transaction;
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        await ReleaseOwnedTransactionResourcesAsync(cleanupExceptions).ConfigureAwait(false);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return null;
    }

    /// <summary>
    /// 确保当前数据源支持本地事务。
    /// </summary>
    private void EnsureTransactionsSupported()
    {
        var dataSource = Options.GetDatabaseContext()?.DataSource;
        EnsureWritableDataSource(dataSource);
        var transactionCapabilities = GetCurrentProviderTransactionCapabilities();
        if (transactionCapabilities.SupportsTransactions == false)
            throw SqlCapabilityFailure.Create(transactionCapabilities.TransactionsFailureReason ??
                SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction", GetCurrentProviderKey(),
                $"Provider {GetCurrentProvider().Key} 不支持本地事务。请使用不依赖事务的查询操作。");
        if (dataSource?.SupportsTransactions != false)
            return;
        var dbKey = dataSource.Key ?? Options.GetDatabaseContext()?.DbKey ?? "<default>";
        throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction", dbKey,
            $"数据源 {dbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
    }

    /// <summary>
    /// 确保当前 Provider 支持存储过程命令。
    /// </summary>
    private protected void EnsureStoredProceduresSupported()
    {
        EnsureWritableDataSource();
        var profile = GetRequiredProviderProfile();
        if (profile.Procedure.SupportsStoredProcedures)
            return;
        throw SqlCapabilityFailure.Create(profile.Procedure.StoredProceduresFailureReason ??
            SqlCapabilityFailureReason.DatabaseUnsupported, "StoredProcedures",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持存储过程命令。");
    }

    /// <summary>
    /// 确保当前 Provider 支持调用方请求的存储过程输出参数。
    /// </summary>
    /// <param name="parameters">存储过程参数。</param>
    private protected void EnsureOutputParametersSupported(object parameters)
    {
        var profile = GetRequiredProviderProfile();
        if (HasOutputParameters(parameters) == false || profile.Procedure.SupportsOutputParameters)
            return;
        throw SqlCapabilityFailure.Create(profile.Procedure.OutputParametersFailureReason ??
            SqlCapabilityFailureReason.ProviderImplementationGap, "OutputParameters",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持存储过程输出参数。");
    }

    /// <summary>
    /// 判断参数源是否声明输出方向。
    /// </summary>
    /// <param name="parameters">待检查的参数源。</param>
    /// <returns>包含 Output、InputOutput 或 ReturnValue 参数时返回 <see langword="true"/>。</returns>
    private static bool HasOutputParameters(object parameters)
    {
        if (parameters is global::Dapper.DynamicParameters dynamicParameters)
            return DynamicParametersOutputAccessor.HasOutputParameters(dynamicParameters);
        if (parameters is SqlParam parameter)
            return IsOutputDirection(parameter.Direction);
        if (parameters is IEnumerable<SqlParam> sqlParameters)
            return sqlParameters.Any(item => item != null && IsOutputDirection(item.Direction));
        if (parameters is ISqlParameterMap parameterMap)
            return parameterMap.GetItems().Any(item => item != null && IsOutputDirection(item.Direction));
        return false;
    }

    /// <summary>
    /// 判断参数方向是否需要在过程完成后读取结果。
    /// </summary>
    /// <param name="direction">参数方向。</param>
    /// <returns>需要输出访问器时返回 <see langword="true"/>。</returns>
    private static bool IsOutputDirection(ParameterDirection? direction) => direction is ParameterDirection.Output or
        ParameterDirection.InputOutput or ParameterDirection.ReturnValue;

    /// <summary>
    /// 确保当前 Provider 支持请求的异步取消。
    /// </summary>
    /// <param name="cancellationToken">调用方传入的取消令牌。</param>
    private protected void EnsureCancellationSupported(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled == false)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        var profile = GetRequiredProviderProfile();
        if (profile.Execution.SupportsCancellation)
            return;
        throw SqlCapabilityFailure.Create(profile.Execution.CancellationFailureReason ??
            SqlCapabilityFailureReason.ProviderImplementationGap, "Cancellation",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持异步命令取消。");
    }

    /// <summary>
    /// 确保当前数据源允许执行结构化写入或事务操作。
    /// </summary>
    /// <param name="dataSource">当前执行数据源。</param>
    private protected void EnsureWritableDataSource(SqlDataSourceDescriptor dataSource = null)
    {
        dataSource ??= Options.GetDatabaseContext()?.DataSource;
        if (dataSource?.IsReadOnly != true)
            return;
        var dbKey = dataSource.Key ?? Options.GetDatabaseContext()?.DbKey ?? "<default>";
        throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "WritableDataSource", dbKey,
            $"数据源 {dbKey} 是只读数据源，不支持写入或事务操作。");
    }

    /// <summary>
    /// 提交 Query 内部拥有的事务。
    /// </summary>
    private void CommitOwnedTransaction()
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("提交");
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            _transaction.Commit();
        }
        catch (Exception commitException)
        {
            primaryException = commitException;
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => _transaction.Rollback());
        }
        ReleaseOwnedTransactionResources(cleanupExceptions);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <summary>
    /// 异步提交 Query 内部拥有的事务。
    /// </summary>
    /// <param name="cancellationToken">提交事务使用的取消令牌。</param>
    private async Task CommitOwnedTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("提交");
        var transaction = _transaction;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
                var result = await SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction, cancellationToken,
                    GetCurrentProviderTransactionCapabilities(), GetCurrentProviderKey())
                .ConfigureAwait(false);
            _transactionExecutionMode = ToDiagnosticValue(result.Mode);
        }
        catch (Exception commitException)
        {
            primaryException = commitException;
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
                async () =>
                {
                    var result = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(transaction,
                        CancellationToken.None, GetCurrentProviderTransactionCapabilities(),
                        GetCurrentProviderKey()).ConfigureAwait(false);
                    _transactionExecutionMode = ToDiagnosticValue(result.Mode);
                }).ConfigureAwait(false);
        }
        await ReleaseOwnedTransactionResourcesAsync(cleanupExceptions).ConfigureAwait(false);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <summary>
    /// 回滚内部拥有的事务
    /// </summary>
    private void RollbackOwnedTransaction()
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("回滚");
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (_connection?.State != ConnectionState.Closed)
                _transaction.Rollback();
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        ReleaseOwnedTransactionResources(cleanupExceptions);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <summary>
    /// 异步回滚内部拥有的事务。
    /// </summary>
    /// <remarks>回滚属于资源清理，始终使用不可取消令牌。</remarks>
    private async Task RollbackOwnedTransactionAsync()
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("回滚");
        var transaction = _transaction;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (_connection?.State != ConnectionState.Closed)
            {
                var result = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(transaction, CancellationToken.None,
                        GetCurrentProviderTransactionCapabilities(), GetCurrentProviderKey())
                    .ConfigureAwait(false);
                _transactionExecutionMode = ToDiagnosticValue(result.Mode);
            }
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        await ReleaseOwnedTransactionResourcesAsync(cleanupExceptions).ConfigureAwait(false);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <summary>
    /// 关闭并释放当前 Query 自有事务资源。
    /// </summary>
    /// <param name="cleanupExceptions">用于保存关闭和释放失败的异常集合。</param>
    /// <remarks>
    /// 连接关闭和事务释放必须分别尝试，避免连接关闭失败阻断事务释放。
    /// </remarks>
    private void ReleaseOwnedTransactionResources(ICollection<Exception> cleanupExceptions)
    {
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, CloseOwnedConnection);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, ReleaseTransaction);
    }

    /// <summary>
    /// 异步关闭并释放当前 Query 自有事务资源。
    /// </summary>
    /// <param name="cleanupExceptions">用于保存关闭和释放失败的异常集合。</param>
    private async Task ReleaseOwnedTransactionResourcesAsync(ICollection<Exception> cleanupExceptions)
    {
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, CloseOwnedConnectionAsync)
            .ConfigureAwait(false);
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, ReleaseTransactionAsync)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 异步关闭自有连接。
    /// </summary>
    private Task CloseOwnedConnectionAsync()
    {
        if (_connectionOwnership != SqlResourceOwnership.Owned || _connection?.State != ConnectionState.Open)
            return Task.CompletedTask;
        return SqlTransactionAsyncAdapter.CloseAsync(_connection);
    }

    /// <summary>
    /// 异步释放自有事务。
    /// </summary>
    private async Task ReleaseTransactionAsync()
    {
        var transaction = _transaction;
        var ownership = _transactionOwnership;
        _transaction = null;
        _transactionId = null;
        _transactionOwnership = SqlResourceOwnership.Owned;
        if (ownership == SqlResourceOwnership.Owned)
            await SqlTransactionAsyncAdapter.DisposeAsync(transaction).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取当前事务诊断标识。
    /// </summary>
    /// <returns>当前事务标识，不存在时返回 null。</returns>
    private string GetCurrentTransactionId()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
        return _transactionId;
    }

    /// <summary>
    /// 将内部事务执行模式转换为稳定诊断值。
    /// </summary>
    /// <param name="mode">内部执行模式。</param>
    /// <returns>诊断使用的执行模式文本。</returns>
    private static string ToDiagnosticValue(SqlTransactionExecutionMode mode) => mode switch
    {
        SqlTransactionExecutionMode.NativeAsync => "NativeAsync",
        SqlTransactionExecutionMode.SynchronousFallback => "SynchronousFallback",
        _ => null
    };

    /// <summary>
    /// 确保当前事务由 Query 或事务作用域拥有。
    /// </summary>
    /// <param name="operation">尝试执行的事务操作。</param>
    private void EnsureOwnedTransaction(string operation)
    {
        if (_transactionOwnership == SqlResourceOwnership.External)
            throw new InvalidOperationException($"当前事务由外部所有者管理，Query 不能{operation}该事务。");
    }

    /// <summary>
    /// 绑定外部事务延迟解析器。
    /// </summary>
    /// <param name="resolver">外部事务解析器。</param>
    private void BindExternalTransactionResolver(Func<IDbTransaction> resolver) =>
        _externalTransactionResolver = resolver;

}
