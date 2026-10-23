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
    private readonly string _dataSourceKey;
    private readonly string _databaseContextKey;
    private readonly string _providerKey;
    private readonly DatabaseType _databaseType;
    private readonly string _tenantId;
    private readonly string _connectionString;
    private readonly PrimaryReadStrategy _primaryReadStrategy;
    private readonly string _primaryDataSourceKey;
    private readonly bool _isReadOnly;
    private readonly bool _supportsTransactions;
    private readonly ISqlDbConnectionFactoryResolver _connectionFactoryResolver;
    private readonly Guid? _opaqueIdentity;

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

    public override bool Equals(object obj) => Equals(obj as DapperSqlUnitOfWorkResourceKey);

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
    private readonly object _sync = new();
    private readonly IUnitOfWorkScope _scope;
    private readonly SqlDataSourceDescriptor _dataSource;
    private readonly bool _isTransactional;
    private readonly System.Data.IsolationLevel? _isolationLevel;
    private readonly SqlProviderTransactionCapabilities _transactionCapabilities;
    private readonly string _providerKey;
    private bool _prepared;
    private bool _completed;
    private bool _disposed;

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

    public object ResourceKey { get; }

    public IDbConnection Connection { get; }

    public IDbTransaction Transaction { get; private set; }

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

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DapperSqlUnitOfWorkConnectionResource));
    }

    private static void Capture(ICollection<Exception> errors, Action action)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    private static async Task CaptureAsync(ICollection<Exception> errors, Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception exception) { errors.Add(exception); }
    }
}
