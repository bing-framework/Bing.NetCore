using System.Data;
using System.Runtime.CompilerServices;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Uow;

namespace Bing.Data.Sql;

/// <summary>
/// 按冻结的数据源配置隔离 Dapper Uow 连接资源。
/// </summary>
internal sealed class DapperSqlUnitOfWorkResourceKey : IEquatable<DapperSqlUnitOfWorkResourceKey>
{
    /// <summary>
    /// 保存参与资源键相等性比较的数据源键。
    /// </summary>
    private readonly string _dataSourceKey;
    /// <summary>
    /// 保存参与资源键相等性比较的数据库上下文键。
    /// </summary>
    private readonly string _databaseContextKey;
    /// <summary>
    /// 保存参与资源键相等性比较的 SQL 提供程序键。
    /// </summary>
    private readonly string _providerKey;
    /// <summary>
    /// 保存参与资源键相等性比较的数据库类型。
    /// </summary>
    private readonly DatabaseType _databaseType;
    /// <summary>
    /// 保存参与资源键相等性比较的租户标识。
    /// </summary>
    private readonly string _tenantId;
    /// <summary>
    /// 保存参与资源键相等性比较的连接字符串。
    /// </summary>
    private readonly string _connectionString;
    /// <summary>
    /// 保存参与资源键相等性比较的主库读取策略。
    /// </summary>
    private readonly PrimaryReadStrategy _primaryReadStrategy;
    /// <summary>
    /// 保存参与资源键相等性比较的主数据源键。
    /// </summary>
    private readonly string _primaryDataSourceKey;
    /// <summary>
    /// 指示资源键是否来自只读连接请求。
    /// </summary>
    private readonly bool _isReadOnly;
    /// <summary>
    /// 指示资源键关联的连接是否支持事务。
    /// </summary>
    private readonly bool _supportsTransactions;
    /// <summary>
    /// 保存决定连接工厂身份的解析器实例。
    /// </summary>
    private readonly ISqlDbConnectionFactoryResolver _connectionFactoryResolver;
    /// <summary>
    /// 保存资源键可选的不透明标识，用于区分同配置的独立资源。
    /// </summary>
    private readonly Guid? _opaqueIdentity;

    /// <summary>
    /// 初始化一个 <see cref="DapperSqlUnitOfWorkResourceKey" /> 类型的实例。
    /// </summary>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    /// <param name="dataSource">用于选择数据库连接的数据源描述信息。</param>
    /// <param name="databaseType">当前连接使用的数据库类型。</param>
    /// <param name="providerKey">数据库提供程序的规范化键。</param>
    /// <param name="connectionString">用于建立数据库连接的连接字符串。</param>
    /// <param name="connectionFactoryResolver">用于解析数据库连接工厂的解析器。</param>
    /// <param name="canCompareConfiguration">指示是否可以比较数据库连接配置。</param>
    public DapperSqlUnitOfWorkResourceKey(DatabaseContext context, SqlDataSourceDescriptor dataSource,
        DatabaseType databaseType,
        string providerKey, string connectionString, ISqlDbConnectionFactoryResolver connectionFactoryResolver,
        bool canCompareConfiguration)
    {
        _dataSourceKey = dataSource?.Key ?? context?.DbKey;
        _databaseContextKey = context?.DbKey;
        _providerKey = providerKey?.Trim().ToLowerInvariant();
        _databaseType = dataSource?.DatabaseType ?? databaseType;
        _tenantId = context?.TenantId;
        _connectionString = connectionString;
        _primaryReadStrategy = dataSource?.PrimaryReadStrategy ?? PrimaryReadStrategy.None;
        _primaryDataSourceKey = dataSource?.PrimaryDataSourceKey;
        _isReadOnly = dataSource?.IsReadOnly ?? false;
        _supportsTransactions = dataSource?.SupportsTransactions ?? true;
        _connectionFactoryResolver = connectionFactoryResolver ?? throw new ArgumentNullException(nameof(connectionFactoryResolver));
        if (!canCompareConfiguration)
            _opaqueIdentity = Guid.NewGuid();
    }

    /// <inheritdoc />
    public bool Equals(DapperSqlUnitOfWorkResourceKey other) =>
        other != null &&
        string.Equals(_dataSourceKey, other._dataSourceKey, StringComparison.Ordinal) &&
        string.Equals(_databaseContextKey, other._databaseContextKey, StringComparison.Ordinal) &&
        string.Equals(_providerKey, other._providerKey, StringComparison.Ordinal) &&
        _databaseType == other._databaseType &&
        string.Equals(_tenantId, other._tenantId, StringComparison.Ordinal) &&
        string.Equals(_connectionString, other._connectionString, StringComparison.Ordinal) &&
        _primaryReadStrategy == other._primaryReadStrategy &&
        string.Equals(_primaryDataSourceKey, other._primaryDataSourceKey, StringComparison.Ordinal) &&
        _isReadOnly == other._isReadOnly &&
        _supportsTransactions == other._supportsTransactions &&
        ReferenceEquals(_connectionFactoryResolver, other._connectionFactoryResolver) &&
        _opaqueIdentity == other._opaqueIdentity;

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as DapperSqlUnitOfWorkResourceKey);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_dataSourceKey ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_databaseContextKey ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerKey ?? string.Empty);
            hash = hash * 31 + _databaseType.GetHashCode();
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_tenantId ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_connectionString ?? string.Empty);
            hash = hash * 31 + _primaryReadStrategy.GetHashCode();
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_primaryDataSourceKey ?? string.Empty);
            hash = hash * 31 + _isReadOnly.GetHashCode();
            hash = hash * 31 + _supportsTransactions.GetHashCode();
            hash = hash * 31 + RuntimeHelpers.GetHashCode(_connectionFactoryResolver);
            hash = hash * 31 + _opaqueIdentity.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// 拥有 Dapper 连接及其 Uow 本地事务的资源。
/// </summary>
internal sealed class DapperSqlUnitOfWorkConnectionResource : IUnitOfWorkConnectionResource
{
    /// <summary>
    /// 同步连接资源准备、完成和释放状态的并发访问。
    /// </summary>
    private readonly object _sync = new();
    /// <summary>
    /// 保存创建该连接资源的工作单元作用域。
    /// </summary>
    private readonly IUnitOfWorkScope _scope;
    /// <summary>
    /// 保存连接资源对应的数据源描述。
    /// </summary>
    private readonly SqlDataSourceDescriptor _dataSource;
    /// <summary>
    /// 指示连接资源是否已开启事务。
    /// </summary>
    private readonly bool _isTransactional;
    /// <summary>
    /// 保存连接资源事务使用的隔离级别。
    /// </summary>
    private readonly System.Data.IsolationLevel? _isolationLevel;
    /// <summary>
    /// 保存连接资源所属提供程序的事务能力。
    /// </summary>
    private readonly SqlProviderTransactionCapabilities _transactionCapabilities;
    /// <summary>
    /// 保存连接资源使用的 SQL 提供程序键。
    /// </summary>
    private readonly string _providerKey;
    /// <summary>
    /// 指示连接资源是否已完成准备。
    /// </summary>
    private bool _prepared;
    /// <summary>
    /// 指示连接资源是否已完成提交或回滚。
    /// </summary>
    private bool _completed;
    /// <summary>
    /// 指示连接资源是否已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="DapperSqlUnitOfWorkConnectionResource" /> 类型的实例。
    /// </summary>
    /// <param name="resourceKey">用于识别当前工作单元资源的键。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="dataSource">用于选择数据库连接的数据源描述信息。</param>
    /// <param name="providerKey">数据库提供程序的规范化键。</param>
    /// <param name="connectionString">用于建立数据库连接的连接字符串。</param>
    /// <param name="connectionFactoryResolver">用于解析数据库连接工厂的解析器。</param>
    /// <param name="transactionCapabilities">当前 SQL 提供程序支持的事务能力。</param>
    public DapperSqlUnitOfWorkConnectionResource(object resourceKey, IUnitOfWorkScope scope,
        SqlDataSourceDescriptor dataSource, string providerKey, string connectionString,
        ISqlDbConnectionFactoryResolver connectionFactoryResolver,
        SqlProviderTransactionCapabilities transactionCapabilities)
    {
        ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _dataSource = dataSource;
        _providerKey = providerKey;
        _isTransactional = scope.Options.IsTransactional;
        _isolationLevel = scope.Options.IsolationLevel;
        _transactionCapabilities = transactionCapabilities;
        Connection = connectionFactoryResolver?.Create(providerKey, connectionString) ??
                     throw new InvalidOperationException("SQL 连接工厂返回了空连接。");
    }

    /// <inheritdoc />
    public object ResourceKey { get; }

    /// <inheritdoc />
    public IDbConnection Connection { get; }

    /// <inheritdoc />
    public IDbTransaction Transaction { get; private set; }

    /// <inheritdoc />
    public void EnsureReady()
    {
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
            var openedHere = false;
            try
            {
                ValidateTransactionSupport();
                if (Connection.State != ConnectionState.Open)
                {
                    Connection.Open();
                    openedHere = true;
                }
                if (_isTransactional)
                    Transaction = _isolationLevel == null
                        ? Connection.BeginTransaction()
                        : Connection.BeginTransaction(_isolationLevel.Value);
                _prepared = true;
            }
            catch (Exception exception)
            {
                _scope.SetRollbackOnly();
                var cleanupErrors = new List<Exception>();
                if (Transaction != null)
                {
                    Capture(cleanupErrors, Transaction.Dispose);
                    Transaction = null;
                }
                if (openedHere)
                    Capture(cleanupErrors, Connection.Close);
                SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupErrors);
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
        }
        var openedHere = false;
        try
        {
            ValidateTransactionSupport();
            cancellationToken.ThrowIfCancellationRequested();
            if (Connection.State != ConnectionState.Open)
            {
                await SqlTransactionAsyncAdapter.OpenAsync(Connection, cancellationToken).ConfigureAwait(false);
                openedHere = true;
            }
            if (_isTransactional)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var begin = _isolationLevel == null
                    ? await SqlTransactionAsyncAdapter.BeginDefaultWithModeAsync(Connection, cancellationToken,
                        _transactionCapabilities, _providerKey).ConfigureAwait(false)
                    : await SqlTransactionAsyncAdapter.BeginWithModeAsync(Connection, _isolationLevel.Value,
                        cancellationToken, _transactionCapabilities, _providerKey).ConfigureAwait(false);
                Transaction = begin.Result;
            }
            lock (_sync)
            {
                EnsureNotDisposed();
                _prepared = true;
            }
        }
        catch (Exception exception)
        {
            _scope.SetRollbackOnly();
            var cleanupErrors = new List<Exception>();
            if (Transaction != null)
            {
                await CaptureAsync(cleanupErrors, () => SqlTransactionAsyncAdapter.DisposeAsync(Transaction))
                    .ConfigureAwait(false);
                Transaction = null;
            }
            if (openedHere)
                await CaptureAsync(cleanupErrors, () => SqlTransactionAsyncAdapter.CloseAsync(Connection))
                    .ConfigureAwait(false);
            SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupErrors);
        }
    }

    /// <inheritdoc />
    public void Commit()
    {
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_completed)
                return;
            if (!_prepared || Transaction == null)
            {
                _completed = true;
                return;
            }
            Transaction.Commit();
            _completed = true;
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        IDbTransaction transaction;
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_completed)
                return;
            if (!_prepared || Transaction == null)
            {
                _completed = true;
                return;
            }
            transaction = Transaction;
        }
        await SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction, cancellationToken,
            _transactionCapabilities, _providerKey).ConfigureAwait(false);
        lock (_sync)
            _completed = true;
    }

    /// <inheritdoc />
    public void Rollback()
    {
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_completed)
                return;
            if (Transaction != null)
                Transaction.Rollback();
            _completed = true;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        IDbTransaction transaction;
        lock (_sync)
        {
            EnsureNotDisposed();
            if (_completed)
                return;
            transaction = Transaction;
        }
        if (transaction != null)
            await SqlTransactionAsyncAdapter.RollbackWithModeAsync(transaction, cancellationToken,
                _transactionCapabilities, _providerKey).ConfigureAwait(false);
        lock (_sync)
            _completed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        var errors = new List<Exception>();
        if (Transaction != null)
            Capture(errors, Transaction.Dispose);
        Capture(errors, Connection.Dispose);
        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        var errors = new List<Exception>();
        if (Transaction != null)
            await CaptureAsync(errors, () => SqlTransactionAsyncAdapter.DisposeAsync(Transaction)).ConfigureAwait(false);
        await CaptureAsync(errors, () => SqlTransactionAsyncAdapter.DisposeAsync(Connection)).ConfigureAwait(false);
        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    /// <summary>
    /// 校验数据源与 Provider 的事务支持条件。
    /// </summary>
    private void ValidateTransactionSupport()
    {
        if (!_isTransactional)
        {
            if (_dataSource?.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
                throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction",
                    _dataSource.Key ?? "<default>", "非事务工作单元不能使用 PrimaryReadStrategy.Transaction。");
            return;
        }
        if (_dataSource?.IsReadOnly == true)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction",
                _dataSource.Key ?? "<default>", "只读数据源不能参与事务工作单元。");
        if (_dataSource?.PrimaryReadStrategy == PrimaryReadStrategy.PrimaryDataSource &&
            !string.IsNullOrWhiteSpace(_dataSource.PrimaryDataSourceKey) &&
            !string.Equals(_dataSource.Key, _dataSource.PrimaryDataSourceKey, StringComparison.Ordinal))
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "PrimaryDataSource",
                _dataSource.Key ?? "<default>", "冻结的数据源是只读副本，事务工作单元不能在执行阶段重定向到主库。");
        if (_dataSource?.SupportsTransactions == false || _transactionCapabilities?.SupportsTransactions == false)
            throw SqlCapabilityFailure.Create(_transactionCapabilities?.TransactionsFailureReason ??
                SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction", _providerKey,
                $"Provider {_providerKey} 或数据源 {_dataSource?.Key ?? "<default>"} 不支持本地事务。");
    }

    /// <summary>
    /// 检查 Dapper 工作单元连接资源尚未释放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DapperSqlUnitOfWorkConnectionResource));
    }

    /// <summary>
    /// 执行清理动作并收集异常。
    /// </summary>
    /// <param name="errors">用于收集清理异常的集合。</param>
    /// <param name="action">待执行的清理动作。</param>
    private static void Capture(ICollection<Exception> errors, Action action)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    /// <summary>
    /// 执行清理动作并收集异常。
    /// </summary>
    /// <param name="errors">用于收集清理异常的集合。</param>
    /// <param name="action">待执行的清理动作。</param>
    private static async Task CaptureAsync(ICollection<Exception> errors, Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception exception) { errors.Add(exception); }
    }
}
