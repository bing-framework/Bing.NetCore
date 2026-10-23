using System.Data;
using System.Runtime.CompilerServices;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 保存一个 SQL 数据源到 EF Core context 的显式映射。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkMapping
{
    public EfCoreSqlUnitOfWorkMapping(Type contextType, string dataSourceKey)
    {
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
        DataSourceKey = dataSourceKey ?? throw new ArgumentNullException(nameof(dataSourceKey));
    }

    public Type ContextType { get; }
    public string DataSourceKey { get; }
}

/// <summary>
/// 为显式映射的数据源登记唯一 EF Core 连接资源。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkConnectionProvider : IUnitOfWorkConnectionProvider,
    IEfCoreSqlUnitOfWorkConnectionProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<EfCoreSqlUnitOfWorkMapping> _mappings;
    private readonly ISqlDataSourceResolver _dataSourceResolver;
    private readonly ISqlConnectionStringResolver _connectionStringResolver;
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    public EfCoreSqlUnitOfWorkConnectionProvider(IServiceProvider serviceProvider,
        IEnumerable<EfCoreSqlUnitOfWorkMapping> mappings, ISqlDataSourceResolver dataSourceResolver = null,
        ISqlConnectionStringResolver connectionStringResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlDatabaseIdentityResolver databaseIdentityResolver = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).ToArray();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver();
        _connectionStringResolver = connectionStringResolver ?? new DefaultSqlConnectionStringResolver();
        _databaseIdentityResolver = databaseIdentityResolver ?? new DefaultSqlDatabaseIdentityResolver();
        _databaseContextAccessor = databaseContextAccessor;
    }

    /// <inheritdoc />
    public IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope, string dataSourceKey,
        string providerKey, string tenantId, string connectionString)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            return null;
        var mapping = FindMapping(dataSourceKey);
        if (mapping == null)
            return null;

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        var configuredConnectionString = _connectionStringResolver.Resolve(dataSource);
        if (!string.Equals(configuredConnectionString, connectionString, StringComparison.Ordinal))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 的冻结连接配置与 EF Core 映射配置不一致。");
        if (!string.IsNullOrWhiteSpace(dataSource.ProviderKey) &&
            !string.Equals(dataSource.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 的冻结 Provider 与 EF Core 映射配置不一致。");
        if (_databaseContextAccessor?.Current?.TenantId != null &&
            !string.Equals(_databaseContextAccessor.Current.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("SQL Query 的冻结租户与 EF Core context 当前租户不一致。");

        var context = ResolveContext(mapping);
        context.EnsureOperationRootMayUseContext(scope);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite: false);
        return CreateResource(scope, mapping, context, dataSource, providerKey, tenantId, connectionString);
    }

    /// <inheritdoc />
    public void EnsureMappedContextMatches(UnitOfWorkBase context, string dataSourceKey)
    {
        var mapping = FindMapping(dataSourceKey);
        if (mapping == null)
            return;
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与 Shared Query context 不是同一实例。");
    }

    public bool HasMappingForContext(UnitOfWorkBase context) =>
        context != null && _mappings.Any(mapping => mapping.ContextType.IsInstanceOfType(context));

    public void ValidateContextAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context, bool isWrite)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.EnsureOperationRootMayUseContext(scope);

        var mappings = _mappings.Where(mapping => mapping.ContextType.IsInstanceOfType(context)).ToArray();
        if (mappings.Length == 0)
            return;
        var dataSourceKey = ResolveContextDataSourceKey(mappings);
        var mapping = mappings.SingleOrDefault(item => string.Equals(item.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal));
        if (mapping == null)
            throw new InvalidOperationException("当前 EF Core context 的显式数据源映射与活动 SQL 数据源不一致。");
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与当前 context 不是同一实例。");

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        var connectionString = _connectionStringResolver.Resolve(dataSource);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite);
    }

    public IUnitOfWorkConnectionResource GetResourceForContext(IUnitOfWorkScope scope, UnitOfWorkBase context)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.EnsureOperationRootMayUseContext(scope);

        var mappings = _mappings.Where(mapping => mapping.ContextType.IsInstanceOfType(context)).ToArray();
        if (mappings.Length == 0)
            return null;
        var dataSourceKey = ResolveContextDataSourceKey(mappings);
        var mapping = mappings.SingleOrDefault(item => string.Equals(item.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal));
        if (mapping == null)
            throw new InvalidOperationException("当前 EF Core context 的显式数据源映射与活动 SQL 数据源不一致。");
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与当前 context 不是同一实例。");

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        var tenantId = _databaseContextAccessor?.Current?.TenantId;
        var providerKey = string.IsNullOrWhiteSpace(dataSource.ProviderKey)
            ? GetOfficialProviderKey(dataSource.DatabaseType)
            : dataSource.ProviderKey;
        var connectionString = _connectionStringResolver.Resolve(dataSource);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite: true);
        return CreateResource(scope, mapping, context, dataSource, providerKey, tenantId, connectionString);
    }

    private void ValidateSourceAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context,
        SqlDataSourceDescriptor dataSource, string configuredConnectionString, bool isWrite)
    {
        var dataSourceKey = dataSource.Key ?? "<default>";
        if (scope.Options.IsTransactional)
        {
            if (dataSource.IsReadOnly)
                throw new InvalidOperationException($"只读 SQL 数据源 {dataSourceKey} 不能参与事务工作单元。");
            if (!dataSource.SupportsTransactions)
                throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 不支持本地事务。");
        }
        else if (dataSource.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
        {
            throw new InvalidOperationException($"非事务工作单元不能使用 PrimaryReadStrategy.Transaction：{dataSourceKey}。");
        }

        if (isWrite && dataSource.IsReadOnly)
            throw new InvalidOperationException($"只读 SQL 数据源 {dataSourceKey} 不允许 EF Core 保存。");
        if ((scope.Options.IsTransactional || isWrite) &&
            dataSource.PrimaryReadStrategy == PrimaryReadStrategy.PrimaryDataSource &&
            !string.IsNullOrWhiteSpace(dataSource.PrimaryDataSourceKey) &&
            !string.Equals(dataSource.Key, dataSource.PrimaryDataSourceKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"冻结的 SQL 数据源 {dataSourceKey} 是只读副本，当前工作单元不能在执行阶段重定向到主库。");

        var connection = context.Database.GetDbConnection();
        var actualConnectionString = connection.ConnectionString;
        if (string.IsNullOrWhiteSpace(configuredConnectionString) || string.IsNullOrWhiteSpace(actualConnectionString))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 无法校验 EF Core 物理数据库身份。");

        SqlDatabaseIdentity configuredIdentity;
        SqlDatabaseIdentity contextIdentity;
        try
        {
            configuredIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, configuredConnectionString);
            contextIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, actualConnectionString);
        }
        catch
        {
            if (string.Equals(configuredConnectionString, actualConnectionString, StringComparison.Ordinal))
                return;
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 与 EF Core context 的物理数据库身份无法比较。");
        }

        if (configuredIdentity?.IsComparable == true && contextIdentity?.IsComparable == true &&
            !configuredIdentity.IsExclusiveMemory && !contextIdentity.IsExclusiveMemory &&
            configuredIdentity.Equals(contextIdentity))
            return;

        if (string.Equals(configuredConnectionString, actualConnectionString, StringComparison.Ordinal))
            return;
        throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 与 EF Core context 指向不同或不可比较的物理数据库。");
    }

    private IUnitOfWorkConnectionResource CreateResource(IUnitOfWorkScope scope,
        EfCoreSqlUnitOfWorkMapping mapping, UnitOfWorkBase context, SqlDataSourceDescriptor dataSource,
        string providerKey, string tenantId, string connectionString)
    {
        var key = new EfCoreSqlUnitOfWorkResourceKey(mapping, context, dataSource.Key, dataSource.DatabaseType,
            providerKey, tenantId, connectionString);
        return scope.GetOrAddResource(key,
            () => new EfCoreSqlUnitOfWorkResource(key, scope, context));
    }

    private UnitOfWorkBase ResolveContext(EfCoreSqlUnitOfWorkMapping mapping)
    {
        var context = _serviceProvider.GetService(mapping.ContextType) as UnitOfWorkBase;
        return context ?? throw new InvalidOperationException(
            $"SQL 数据源 {mapping.DataSourceKey} 映射的 EF Core context {mapping.ContextType.FullName} 未注册到当前 DI scope。");
    }

    private EfCoreSqlUnitOfWorkMapping FindMapping(string dataSourceKey)
    {
        var matches = _mappings.Where(mapping => string.Equals(mapping.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 存在多个 EF Core context 映射。");
        return matches.SingleOrDefault();
    }

    private string ResolveContextDataSourceKey(IReadOnlyList<EfCoreSqlUnitOfWorkMapping> mappings)
    {
        var ambientKey = _databaseContextAccessor?.Current?.DbKey;
        if (!string.IsNullOrWhiteSpace(ambientKey))
            return ambientKey;
        if (mappings.Count == 1)
            return mappings[0].DataSourceKey;

        var defaultKey = _dataSourceResolver.Resolve().Key;
        return mappings.Any(mapping => string.Equals(mapping.DataSourceKey, defaultKey, StringComparison.Ordinal))
            ? defaultKey
            : null;
    }

    private static string GetOfficialProviderKey(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.MySql => "bing.mysql",
        DatabaseType.PgSql => "bing.postgresql",
        DatabaseType.SqlServer => "bing.sqlserver",
        DatabaseType.Sqlite => "bing.sqlite",
        DatabaseType.Oracle => "bing.oracle",
        _ => throw new NotSupportedException($"未能为数据库类型 {databaseType} 解析 EF Core SQL Provider。")
    };

    private static void ValidateDatabaseType(UnitOfWorkBase context, DatabaseType dataSourceDatabaseType)
    {
        var providerName = context.Database.ProviderName;
        var contextDatabaseType = providerName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => DatabaseType.Sqlite,
            "Microsoft.EntityFrameworkCore.SqlServer" => DatabaseType.SqlServer,
            "Pomelo.EntityFrameworkCore.MySql" or "MySql.EntityFrameworkCore" => DatabaseType.MySql,
            "Npgsql.EntityFrameworkCore.PostgreSQL" => DatabaseType.PgSql,
            "Oracle.EntityFrameworkCore" => DatabaseType.Oracle,
            _ => throw new NotSupportedException($"不支持 EF Core Provider {providerName ?? "<未指定>"} 的 SQL 工作单元映射。")
        };
        if (contextDatabaseType != dataSourceDatabaseType)
            throw new InvalidOperationException($"SQL 数据源数据库类型 {dataSourceDatabaseType} 与 EF Core Provider 类型 {contextDatabaseType} 不一致。");
    }
}

/// <summary>
/// EF Core 映射 provider 的内部校验入口。
/// </summary>
internal interface IEfCoreSqlUnitOfWorkConnectionProvider
{
    void EnsureMappedContextMatches(UnitOfWorkBase context, string dataSourceKey);
    bool HasMappingForContext(UnitOfWorkBase context);
    void ValidateContextAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context, bool isWrite);
    IUnitOfWorkConnectionResource GetResourceForContext(IUnitOfWorkScope scope, UnitOfWorkBase context);
}

/// <summary>
/// 标识一个 EF context 当前租用的 root 和数据源资源。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkResourceKey : IEquatable<EfCoreSqlUnitOfWorkResourceKey>
{
    private readonly EfCoreSqlUnitOfWorkMapping _mapping;
    private readonly UnitOfWorkBase _context;
    private readonly string _dataSourceKey;
    private readonly DatabaseType _databaseType;
    private readonly string _providerKey;
    private readonly string _tenantId;
    private readonly string _connectionString;

    public EfCoreSqlUnitOfWorkResourceKey(EfCoreSqlUnitOfWorkMapping mapping, UnitOfWorkBase context,
        string dataSourceKey, DatabaseType databaseType, string providerKey, string tenantId,
        string connectionString)
    {
        _mapping = mapping;
        _context = context;
        _dataSourceKey = dataSourceKey;
        _databaseType = databaseType;
        _providerKey = providerKey?.Trim().ToLowerInvariant();
        _tenantId = tenantId;
        _connectionString = connectionString;
    }

    public bool Equals(EfCoreSqlUnitOfWorkResourceKey other) => other != null &&
        ReferenceEquals(_mapping, other._mapping) && ReferenceEquals(_context, other._context) &&
        string.Equals(_dataSourceKey, other._dataSourceKey, StringComparison.Ordinal) &&
        _databaseType == other._databaseType &&
        string.Equals(_providerKey, other._providerKey, StringComparison.Ordinal) &&
        string.Equals(_tenantId, other._tenantId, StringComparison.Ordinal) &&
        string.Equals(_connectionString, other._connectionString, StringComparison.Ordinal);

    public override bool Equals(object obj) => Equals(obj as EfCoreSqlUnitOfWorkResourceKey);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = RuntimeHelpers.GetHashCode(_mapping);
            hash = hash * 31 + RuntimeHelpers.GetHashCode(_context);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_dataSourceKey ?? string.Empty);
            hash = hash * 31 + _databaseType.GetHashCode();
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerKey ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_tenantId ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_connectionString ?? string.Empty);
            return hash;
        }
    }
}

/// <summary>
/// 在业务工作单元中独占管理 EF Core 连接和本地事务。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkResource : IUnitOfWorkConnectionResource
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IUnitOfWorkScope _scope;
    private readonly UnitOfWorkBase _context;
    private readonly DatabaseFacade _database;
    private IDbContextTransaction _transaction;
    private bool _prepared;
    private bool _openedHere;
    private bool _completed;
    private bool _committed;
    private bool _rolledBack;
    private bool _failed;
    private bool _disposed;

    public EfCoreSqlUnitOfWorkResource(object resourceKey, IUnitOfWorkScope scope, UnitOfWorkBase context)
    {
        ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _database = context.Database;
        Connection = _database.GetDbConnection();
        EfCoreSqlUnitOfWorkContextRegistry.Claim(context, scope.Id, resourceKey,
            context.ServiceProvider.GetService<IDatabaseContextAccessor>()?.Current?.TenantId);
        _context.OnMappedSqlResourceCreated(scope.Id, resourceKey);
    }

    public object ResourceKey { get; }
    public IDbConnection Connection { get; }
    public IDbTransaction Transaction => _transaction?.GetDbTransaction();

    public void EnsureReady()
    {
        _gate.Wait();
        try
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
            if (_failed)
                throw new InvalidOperationException("EF Core 工作单元资源准备失败，不能重试。");
            ValidateNoForeignTransaction();
            try
            {
                if (Connection.State != ConnectionState.Open)
                {
                    _database.OpenConnection();
                    _openedHere = true;
                }
                if (_scope.Options.IsTransactional)
                    _transaction = _scope.Options.IsolationLevel == null
                        ? _database.BeginTransaction()
                        : _database.BeginTransaction(_scope.Options.IsolationLevel.Value);
                _prepared = true;
            }
            catch (Exception exception)
            {
                FailPreparation(exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _scope.SetRollbackOnly();
            throw;
        }
        try
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
            if (_failed)
                throw new InvalidOperationException("EF Core 工作单元资源准备失败，不能重试。");
            ValidateNoForeignTransaction();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Connection.State != ConnectionState.Open)
                {
                    await _database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                    _openedHere = true;
                }
                if (_scope.Options.IsTransactional)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _transaction = _scope.Options.IsolationLevel == null
                        ? await _database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                        : await _database.BeginTransactionAsync(_scope.Options.IsolationLevel.Value,
                            cancellationToken).ConfigureAwait(false);
                }
                _prepared = true;
            }
            catch (Exception exception)
            {
                await FailPreparationAsync(exception).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Commit()
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        _transaction?.Commit();
        _committed = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: true, rolledBack: false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        if (_transaction != null)
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: true, rolledBack: false);
    }

    public void Rollback()
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        _transaction?.Rollback();
        _rolledBack = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: false, rolledBack: true);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        if (_transaction != null)
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _rolledBack = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: false, rolledBack: true);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        var errors = new List<Exception>();
        Capture(errors, () => _transaction?.Dispose());
        if (_openedHere)
            Capture(errors, _database.CloseConnection);
        EfCoreSqlUnitOfWorkContextRegistry.Release(_context, _scope.Id, ResourceKey);
        _context.OnMappedSqlResourceDisposed(_scope.Id, ResourceKey, _committed, _rolledBack);
        ThrowCleanup(errors);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        var errors = new List<Exception>();
        if (_transaction != null)
            await CaptureAsync(errors, () => _transaction.DisposeAsync().AsTask()).ConfigureAwait(false);
        if (_openedHere)
            await CaptureAsync(errors, () => _database.CloseConnectionAsync()).ConfigureAwait(false);
        EfCoreSqlUnitOfWorkContextRegistry.Release(_context, _scope.Id, ResourceKey);
        _context.OnMappedSqlResourceDisposed(_scope.Id, ResourceKey, _committed, _rolledBack);
        ThrowCleanup(errors);
    }

    private void ValidateNoForeignTransaction()
    {
        if (_database.CurrentTransaction != null)
            throw new InvalidOperationException("EF Core context 已有非当前业务工作单元创建的事务，不能映射为新资源。");
        if (System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("当前存在环境事务，不能将 EF Core context 映射到新的本地事务资源。");
    }

    private void FailPreparation(Exception primary)
    {
        _failed = true;
        _scope.SetRollbackOnly();
        var errors = new List<Exception>();
        Capture(errors, () => _transaction?.Dispose());
        _transaction = null;
        if (_openedHere)
        {
            Capture(errors, _database.CloseConnection);
            _openedHere = false;
        }
        ThrowWithPrimary(primary, errors);
    }

    private async Task FailPreparationAsync(Exception primary)
    {
        _failed = true;
        _scope.SetRollbackOnly();
        var errors = new List<Exception>();
        if (_transaction != null)
            await CaptureAsync(errors, () => _transaction.DisposeAsync().AsTask()).ConfigureAwait(false);
        _transaction = null;
        if (_openedHere)
        {
            await CaptureAsync(errors, () => _database.CloseConnectionAsync()).ConfigureAwait(false);
            _openedHere = false;
        }
        ThrowWithPrimary(primary, errors);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EfCoreSqlUnitOfWorkResource));
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

    private static void ThrowWithPrimary(Exception primary, ICollection<Exception> errors)
    {
        if (errors.Count == 0)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        var all = new List<Exception> { primary };
        all.AddRange(errors);
        throw new AggregateException(all);
    }

    private static void ThrowCleanup(ICollection<Exception> errors)
    {
        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors.First()).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }
}

/// <summary>
/// 防止同一个 DbContext 被多个活动 root 或多个数据源租用。
/// </summary>
internal static class EfCoreSqlUnitOfWorkContextRegistry
{
    private static readonly object Sync = new();
    private static readonly ConditionalWeakTable<UnitOfWorkBase, Reservation> Reservations = new();

    public static void Claim(UnitOfWorkBase context, Guid scopeId, object resourceKey, string tenantId)
    {
        lock (Sync)
        {
            if (Reservations.TryGetValue(context, out var current))
            {
                if (current.ScopeId == scopeId && Equals(current.ResourceKey, resourceKey) &&
                    string.Equals(current.TenantId, tenantId, StringComparison.Ordinal))
                    return;
                throw new InvalidOperationException("同一个 EF Core context 不能同时加入不同工作单元、数据源或租户资源。");
            }
            Reservations.Add(context, new Reservation(scopeId, resourceKey, tenantId));
        }
    }

    public static void Release(UnitOfWorkBase context, Guid scopeId, object resourceKey)
    {
        lock (Sync)
        {
            if (Reservations.TryGetValue(context, out var current) && current.ScopeId == scopeId &&
                Equals(current.ResourceKey, resourceKey))
                Reservations.Remove(context);
        }
    }

    private sealed class Reservation
    {
        public Reservation(Guid scopeId, object resourceKey, string tenantId)
        {
            ScopeId = scopeId;
            ResourceKey = resourceKey;
            TenantId = tenantId;
        }

        public Guid ScopeId { get; }
        public object ResourceKey { get; }
        public string TenantId { get; }
    }
}
