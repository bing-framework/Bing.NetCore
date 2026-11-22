using System.Runtime.ExceptionServices;
using System.Collections;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Bing.Data.Sql;

using Bing.Extensions;
using Bing.Uow;
/// <summary>管理执行资源所有权、短期执行租约与可选事务参与；不持有查询或写入 Builder。</summary>
internal abstract partial class SqlExecutionResources : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 当前资源所有者的 Root 清理协调器。
    /// </summary>
    private readonly SqlExecutionResourceCleanupCoordinator _cleanupCoordinator;

    /// <summary>
    /// 当前资源所有者的工作单元参与协调器。
    /// </summary>
    private SqlUnitOfWorkResourceCoordinator _unitOfWorkCoordinator;

    private readonly Func<SqlOptions> _options;
    private readonly Func<ISqlProvider> _providerResolver;
    private readonly Func<string> _connectionStringResolver;
    private readonly Func<bool> _isRuntimeInitialized;
    private readonly Func<SqlProviderTransactionCapabilities> _transactionCapabilities;
    private readonly Action<SqlDataSourceDescriptor> _ensureWritable;
    internal IServiceProvider ServiceProvider { get; }
    internal SqlOptions Options => _options();
    internal SqlExecutionResources(IServiceProvider services, Func<SqlOptions> options,
        Func<ISqlProvider> providerResolver, Func<string> connectionStringResolver,
        Func<bool> isRuntimeInitialized, Func<SqlProviderTransactionCapabilities> transactionCapabilities,
        Action<SqlDataSourceDescriptor> ensureWritable)
    {
        ServiceProvider = services;
        _cleanupCoordinator = new SqlExecutionResourceCleanupCoordinator(this);
        _unitOfWorkCoordinator = new SqlUnitOfWorkResourceCoordinator(this);
        _options = options;
        _providerResolver = providerResolver;
        _connectionStringResolver = connectionStringResolver;
        _isRuntimeInitialized = isRuntimeInitialized;
        _transactionCapabilities = transactionCapabilities;
        _ensureWritable = ensureWritable;
        _connection = Options.Connection;
        if (_connection != null) _connectionOwnership = SqlResourceOwnership.External;
    }
    internal ISqlProvider GetCurrentProvider() => _providerResolver();
    internal string GetCurrentProviderKey() => GetCurrentProvider().Key;
    internal SqlProviderTransactionCapabilities GetCurrentProviderTransactionCapabilities() => _transactionCapabilities();
    internal string ResolveConnectionString() => _connectionStringResolver();
    internal void EnsureWritableDataSource(SqlDataSourceDescriptor source = null) => _ensureWritable(source);
    internal SqlConnectionSource ConnectionSource => _connectionSource;
    internal SqlResourceOwnership ConnectionOwnership => _connectionOwnership;
    internal SqlResourceOwnership TransactionOwnership => _transactionOwnership;
    internal bool HasTransaction => _transaction != null;
    internal string TransactionId => _transactionId;
    internal string TransactionExecutionMode => _transactionScopeLease?.ExecutionMode ?? _transactionExecutionMode;

    /// <summary>
    /// 以引用访问具象资源所有者的本地状态，避免复制值类型。
    /// </summary>
    internal abstract ref SqlExecutionResourceCore ResourceCore { get; }

    // 保留原有内部字段名称作为轻量转发属性，复用连接与事务的通用生命周期算法。
    internal IDbConnection _connection
    {
        get => ResourceCore.ConnectionState.Connection;
        set => ResourceCore.ConnectionState.Connection = value;
    }

    internal SqlResourceOwnership _connectionOwnership
    {
        get => ResourceCore.ConnectionState.ConnectionOwnership;
        set => ResourceCore.ConnectionState.ConnectionOwnership = value;
    }

    internal IDbTransaction _transaction
    {
        get => ResourceCore.TransactionState.Transaction;
        set => ResourceCore.TransactionState.Transaction = value;
    }

    internal string _transactionId
    {
        get => ResourceCore.TransactionState.TransactionId;
        set => ResourceCore.TransactionState.TransactionId = value;
    }

    internal string _transactionExecutionMode
    {
        get => ResourceCore.TransactionState.TransactionExecutionMode;
        set => ResourceCore.TransactionState.TransactionExecutionMode = value;
    }

    internal ISqlTransactionScopeLease _transactionScopeLease
    {
        get => ResourceCore.TransactionState.TransactionScopeLease;
        set => ResourceCore.TransactionState.TransactionScopeLease = value;
    }

    internal bool _isTransactionScopeChildDisposed
    {
        get => ResourceCore.TransactionState.IsTransactionScopeChildDisposed;
        set => ResourceCore.TransactionState.IsTransactionScopeChildDisposed = value;
    }

    internal SqlResourceOwnership _transactionOwnership
    {
        get => ResourceCore.TransactionState.TransactionOwnership;
        set => ResourceCore.TransactionState.TransactionOwnership = value;
    }

    internal Func<IDbTransaction> _externalTransactionResolver
    {
        get => ResourceCore.TransactionState.ExternalTransactionResolver;
        set => ResourceCore.TransactionState.ExternalTransactionResolver = value;
    }

    internal SqlConnectionSource _connectionSource
    {
        get => ResourceCore.ConnectionState.ConnectionSource;
        set => ResourceCore.ConnectionState.ConnectionSource = value;
    }

    internal int _executionLease
    {
        get => ResourceCore.LifecycleState.ExecutionLease;
        set => ResourceCore.LifecycleState.ExecutionLease = value;
    }

    private object _executionStateLock => ResourceCore.LifecycleState.ExecutionStateLock;

    internal int _isDisposed
    {
        get => ResourceCore.LifecycleState.IsDisposed;
        set => ResourceCore.LifecycleState.IsDisposed = value;
    }

    /// <summary>
    /// 获取或创建内部执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    internal IDbConnection GetExecutionConnection() =>
        GetOrCreateConnection();

    /// <summary>
    /// 将当前连接的释放责任转移给事务作用域。
    /// </summary>
    /// <remarks>
    /// 旧版自定义 Query 工厂没有独立事务资源 SPI，只能先创建 Query 再取得连接。
    /// 转移后 Query 仍可安全释放，但不再释放连接；事务作用域使用返回的 owner 管理连接，
    /// 从而避免把 Query 作为事务资源根对象。
    /// </remarks>
    /// <returns>持有连接释放责任的事务连接 owner。</returns>
    internal SqlTransactionConnectionOwner TransferConnectionOwnership()
    {
        EnsureExecutionAvailable();
        var connection = GetOrCreateConnection();
        var provider = GetCurrentProvider();
        var context = DatabaseContextSnapshot.Create(Options.GetDatabaseContext());
        var ownsConnection = _connectionOwnership == SqlResourceOwnership.Owned;
        _connectionOwnership = SqlResourceOwnership.External;
        return new SqlTransactionConnectionOwner(context, provider, connection, ownsConnection);
    }

    /// <summary>
    /// 获取或设置当前实例的非阻塞执行租约。
    /// </summary>
    /// <remarks>
    /// <para>Query 和 Executor 保存可变的 Builder、连接与事务状态；同一实例只允许一个执行操作。</para>
    /// <para>必须在操作结束时释放的执行租约。</para>
    /// </remarks>
    internal Func<IDisposable> ExecutionLeaseFactory { get; set; }

    /// <summary>
    /// 获取当前实例的执行租约。
    /// </summary>
    /// <returns>用于在操作结束时释放执行占用的租约。</returns>
    internal IDisposable AcquireExecutionLease()
    {
        if (ExecutionLeaseFactory != null)
            return ExecutionLeaseFactory();
        var transactionScopeExecutionLease = _transactionScopeLease?.AcquireExecutionLease();
        IDisposable unitOfWorkExecutionLease = null;
        var acquiredInstanceLease = false;
        try
        {
            lock (_executionStateLock)
            {
                EnsureExecutionAvailable();
                if (_executionLease != 0)
                    throw new InvalidOperationException("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。");
                _executionLease = 1;
                acquiredInstanceLease = true;
            }
            unitOfWorkExecutionLease = AcquireUnitOfWorkResourceLease();
            return new ExecutionLease(this, transactionScopeExecutionLease, unitOfWorkExecutionLease);
        }
        catch
        {
            unitOfWorkExecutionLease?.Dispose();
            if (acquiredInstanceLease)
                ReleaseExecutionLease();
            transactionScopeExecutionLease?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 归还当前实例的执行租约。
    /// </summary>
    internal void ReleaseExecutionLease()
    {
        lock (_executionStateLock)
            _executionLease = 0;
    }

    /// <summary>
    /// 当前实例执行租约。
    /// </summary>
    private sealed class ExecutionLease : IDisposable
    {
        /// <summary>
        /// 所属查询对象。
        /// </summary>
        private SqlExecutionResources _owner;

        /// <summary>
        /// 当前执行关联的事务作用域租约。
        /// </summary>
        private IDisposable _transactionScopeExecutionLease;

        /// <summary>
        /// 当前执行关联的 Uow 资源租约。
        /// </summary>
        private IDisposable _unitOfWorkExecutionLease;

        /// <summary>
        /// 初始化一个 <see cref="ExecutionLease"/> 类型的实例。
        /// </summary>
        /// <param name="owner">所属查询对象。</param>
        /// <param name="transactionScopeExecutionLease">关联的事务作用域执行租约。</param>
        /// <param name="unitOfWorkExecutionLease">关联的 Uow 资源租约。</param>
        public ExecutionLease(SqlExecutionResources owner, IDisposable transactionScopeExecutionLease,
            IDisposable unitOfWorkExecutionLease)
        {
            _owner = owner;
            _transactionScopeExecutionLease = transactionScopeExecutionLease;
            _unitOfWorkExecutionLease = unitOfWorkExecutionLease;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            Interlocked.Exchange(ref _unitOfWorkExecutionLease, null)?.Dispose();
            owner?.ReleaseExecutionLease();
            Interlocked.Exchange(ref _transactionScopeExecutionLease, null)?.Dispose();
        }
    }

    /// <summary>
    /// 获取或创建执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    internal IDbConnection GetOrCreateConnection()
    {
        EnsureExecutionAvailable();
        if (_connection != null)
            return _connection;
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空");
        var resolver = ServiceProvider.GetService<ISqlDbConnectionFactoryResolver>();
        if (resolver == null)
            throw new InvalidOperationException("未注册 SQL 数据库连接工厂解析器。");
        _connection = resolver.Create(GetCurrentProvider().Key, connectionString);
        _connectionOwnership = SqlResourceOwnership.Owned;
        if (_connection == null)
            throw new InvalidOperationException("数据库连接不能为空");
        return _connection;
    }

    /// <summary>
    /// 绑定对象创建后不可变的数据库上下文。
    /// </summary>
    /// <param name="context">由工厂或框架适配层创建的数据库上下文。</param>
    internal void BindDatabaseContext(DatabaseContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        EnsureExecutionAvailable();
        if (Volatile.Read(ref ResourceCore.LifecycleState.ExecutionLease) != 0)
            throw new InvalidOperationException("当前 SQL Query 或 Executor 正在执行，不能修改数据库上下文。");
        if (_isRuntimeInitialized() || _connectionOwnership == SqlResourceOwnership.Owned && _connection != null)
            throw new InvalidOperationException("SQL Query 已初始化 Provider、Builder 或连接，不能修改数据库上下文。");
        Options.SetDatabaseContext(DatabaseContextSnapshot.Create(context));
    }


    /// <summary>
    /// 绑定执行连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="ownership">连接所有权。</param>
    /// <param name="source">连接来源。</param>
    /// <param name="validateExternalIdentity">是否按外部连接契约校验物理身份。</param>
    internal void BindConnection(IDbConnection connection, SqlResourceOwnership ownership, SqlConnectionSource source,
        bool validateExternalIdentity = true)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        EnsureConnectionCanBeReplaced(connection);
        if (validateExternalIdentity)
            ValidateExternalConnectionDatabaseIdentity(connection);
        if (_connection != null && ReferenceEquals(_connection, connection))
        {
            _connectionOwnership = ownership;
            _connectionSource = source;
            return;
        }
        if (_connection != null && _connectionOwnership == SqlResourceOwnership.External)
            throw new InvalidOperationException("当前 Query 已绑定其他外部连接，不能静默替换连接资源。");
        ReleaseOwnedConnectionBeforeReplacement();
        _connection = connection;
        _connectionOwnership = ownership;
        _connectionSource = source;
    }

    /// <summary>
    /// 确保当前连接可以被替换。
    /// </summary>
    /// <param name="connection">待绑定连接。</param>
    internal void EnsureConnectionCanBeReplaced(IDbConnection connection)
    {
        if (_transaction == null)
            return;
        if (ReferenceEquals(_transaction.Connection, connection))
            return;
        throw new InvalidOperationException("当前 Query 存在活动事务，不能替换数据库连接。");
    }

    /// <summary>
    /// 在替换连接前释放原有自有连接。
    /// </summary>
    internal void ReleaseOwnedConnectionBeforeReplacement()
    {
        if (_connection == null || _connectionOwnership != SqlResourceOwnership.Owned)
            return;
        var connection = _connection;
        Exception closeException = null;
        try
        {
            if (connection.State != ConnectionState.Closed)
                connection.Close();
        }
        catch (Exception exception)
        {
            closeException = exception;
        }
        try
        {
            connection.Dispose();
        }
        catch (Exception exception)
        {
            if (closeException != null)
                throw new AggregateException(closeException, exception);
            throw;
        }
        if (closeException != null)
            ExceptionDispatchInfo.Capture(closeException).Throw();
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
        _connectionSource = SqlConnectionSource.Unknown;
    }

    /// <summary>
    /// 校验外部连接与当前固定数据库上下文的物理身份。
    /// </summary>
    /// <param name="connection">外部数据库连接。</param>
    internal void ValidateExternalConnectionDatabaseIdentity(IDbConnection connection)
    {
        var context = Options.GetDatabaseContext();
        var dataSource = context?.DataSource;
        if (dataSource == null ||
            (string.IsNullOrWhiteSpace(dataSource.ConnectionString) &&
             string.IsNullOrWhiteSpace(dataSource.ConnectionStringName)))
            return;
        if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            throw new InvalidOperationException("外部连接缺少用于校验数据库身份的连接字符串。");
        var resolver = ServiceProvider.GetService<ISqlDatabaseIdentityResolver>();
        if (resolver == null)
            throw new InvalidOperationException("未注册 SQL 数据库身份解析器，无法校验外部连接。");
        var connectionStringResolver = ServiceProvider.GetService<ISqlConnectionStringResolver>() ??
                                       new DefaultSqlConnectionStringResolver(
                                           ServiceProvider.GetService<ConnectionStringCollection>());
        var expectedConnectionString = connectionStringResolver.Resolve(dataSource);
        var databaseType = dataSource.DatabaseType == default ? Options.DatabaseType : dataSource.DatabaseType;
        var queryIdentity = resolver.Resolve(databaseType, expectedConnectionString);
        var externalIdentity = resolver.Resolve(databaseType, connection.ConnectionString);
        if (queryIdentity.IsComparable && externalIdentity.IsComparable && queryIdentity.Equals(externalIdentity))
            return;
        throw new InvalidOperationException(
            $"外部连接数据库身份与 Query 上下文不一致。Query DbKey={context?.DbKey ?? "<default>"}; " +
            $"Query DatabaseType={databaseType}; ExternalIdentity={FormatDatabaseIdentity(externalIdentity)}; " +
            $"QueryIdentity={FormatDatabaseIdentity(queryIdentity)}。");
    }

    /// <summary>
    /// 格式化不含凭据的数据库物理身份。
    /// </summary>
    /// <param name="identity">数据库物理身份。</param>
    /// <returns>诊断使用的脱敏身份文本。</returns>
    internal static string FormatDatabaseIdentity(SqlDatabaseIdentity identity)
    {
        if (identity == null)
            return "<null>";
        return $"Type={identity.DatabaseType};Server={identity.Server};Port={identity.Port};Database={identity.Database};" +
               $"FilePath={identity.FilePath};ServiceName={identity.ServiceName};Sid={identity.Sid};Alias={identity.OracleAlias}";
    }


    /// <inheritdoc />
    public void Dispose()
        => _cleanupCoordinator.Dispose();

    /// <summary>
    /// 确保当前 Root 对象未在执行中。
    /// </summary>
    internal void EnsureNoActiveExecutionForDispose()
    {
        lock (_executionStateLock)
        {
            if (_executionLease != 0)
                throw new InvalidOperationException("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
        }
    }

    /// <summary>
    /// 原子标记当前 Root 对象进入释放状态。
    /// </summary>
    /// <returns>首次开始释放时返回 true；已释放时返回 false。</returns>
    internal bool TryBeginDispose()
    {
        lock (_executionStateLock)
        {
            if (_executionLease != 0)
                throw new InvalidOperationException("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
            if (_isDisposed != 0)
                return false;
            _isDisposed = 1;
            return true;
        }
    }

    /// <summary>
    /// 确保未继续使用已释放的事务作用域子对象。
    /// </summary>
    internal void ThrowIfTransactionScopeChildDisposed()
    {
        if (_isTransactionScopeChildDisposed)
            throw new ObjectDisposedException(nameof(SqlExecutionResources), "事务作用域创建的 Query 或 Executor 已释放，不能继续使用。");
    }

    /// <summary>
    /// 在 Root 清理完成后标记事务作用域子对象已经释放。
    /// </summary>
    internal void MarkTransactionScopeChildDisposed()
    {
        if (_transactionScopeLease != null)
            _isTransactionScopeChildDisposed = true;
    }

    /// <summary>
    /// 确保当前查询可以在其所属事务作用域中继续执行。
    /// </summary>
    /// <remarks>
    /// 事务作用域结束应优先于对象释放状态报告，以便调用方识别跨作用域复用错误。
    /// </remarks>
    internal void EnsureExecutionAvailable()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
        ThrowIfDisposed();
    }

    /// <summary>
    /// 确保 Root Query 未被释放。
    /// </summary>
    internal void ThrowIfDisposed()
    {
        if (Volatile.Read(ref ResourceCore.LifecycleState.IsDisposed) != 0)
            throw new ObjectDisposedException(nameof(SqlExecutionResources), "SQL Query 或 Executor 已释放，不能继续执行或获取资源。");
    }

    /// <summary>
    /// 释放事务
    /// </summary>
    internal void ReleaseTransaction()
    {
        var transaction = _transaction;
        var ownership = _transactionOwnership;
        _transaction = null;
        _transactionId = null;
        _transactionOwnership = SqlResourceOwnership.Owned;
        if (ownership == SqlResourceOwnership.Owned)
            transaction?.Dispose();
    }

    /// <summary>
    /// 释放连接
    /// </summary>
    internal void ReleaseConnection()
    {
        var connection = _connection;
        var ownership = _connectionOwnership;
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
        _connectionSource = SqlConnectionSource.Unknown;
        if (ownership == SqlResourceOwnership.Owned)
            connection?.Dispose();
    }

    /// <summary>
    /// 异步释放连接。
    /// </summary>
    internal async Task ReleaseConnectionAsync()
    {
        var connection = _connection;
        var ownership = _connectionOwnership;
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
        _connectionSource = SqlConnectionSource.Unknown;
        if (ownership == SqlResourceOwnership.Owned)
            await SqlTransactionAsyncAdapter.DisposeAsync(connection).ConfigureAwait(false);
    }

    /// <summary>
    /// 关闭并释放内部拥有的连接
    /// </summary>
    internal void CloseOwnedConnection()
    {
        if (_connectionOwnership != SqlResourceOwnership.Owned)
            return;
        if (_connection?.State == ConnectionState.Open)
            _connection.Close();
    }


    /// <inheritdoc />
    /// <remarks>
    /// 异步释放自有事务、连接和运行时注册。
    ///
    /// 活动执行期间不得释放 Root 对象，避免中断流式读取或结果集生命周期。
    /// </remarks>
    ValueTask IAsyncDisposable.DisposeAsync()
        => _cleanupCoordinator.DisposeAsync();

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
    internal void BindExternalTransaction(IDbTransaction transaction, string transactionId)
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
    internal IDbTransaction GetExecutionTransaction() =>
        GetCurrentTransaction();

    /// <summary>
    /// 获取当前执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    internal IDbTransaction GetCurrentTransaction()
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
    /// 开始 Query 内部拥有的事务。
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>内部拥有的数据库事务。</returns>
    internal IDbTransaction BeginOwnedTransaction(IsolationLevel? isolationLevel = null) =>
        BeginTransactionImpl(isolationLevel);

    /// <summary>
    /// 异步开始 Query 内部拥有的事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>内部拥有的数据库事务。</returns>
    internal Task<IDbTransaction> BeginOwnedTransactionAsync(CancellationToken cancellationToken,
        IsolationLevel? isolationLevel = null) => BeginTransactionImplAsync(isolationLevel, cancellationToken);

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <returns>已开始或复用的内部事务。</returns>
    internal IDbTransaction BeginTransactionImpl(IsolationLevel? isolationLevel)
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
    internal async Task<IDbTransaction> BeginTransactionImplAsync(IsolationLevel? isolationLevel,
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
    internal void EnsureTransactionsSupported()
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
    /// 提交 Query 内部拥有的事务。
    /// </summary>
    internal void CommitOwnedTransaction()
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
    internal async Task CommitOwnedTransactionAsync(CancellationToken cancellationToken)
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
    internal void RollbackOwnedTransaction()
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
    internal async Task RollbackOwnedTransactionAsync()
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
    internal void ReleaseOwnedTransactionResources(ICollection<Exception> cleanupExceptions)
    {
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, CloseOwnedConnection);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, ReleaseTransaction);
    }

    /// <summary>
    /// 异步关闭并释放当前 Query 自有事务资源。
    /// </summary>
    /// <param name="cleanupExceptions">用于保存关闭和释放失败的异常集合。</param>
    internal async Task ReleaseOwnedTransactionResourcesAsync(ICollection<Exception> cleanupExceptions)
    {
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, CloseOwnedConnectionAsync)
            .ConfigureAwait(false);
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, ReleaseTransactionAsync)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 异步关闭自有连接。
    /// </summary>
    internal Task CloseOwnedConnectionAsync()
    {
        if (_connectionOwnership != SqlResourceOwnership.Owned || _connection?.State != ConnectionState.Open)
            return Task.CompletedTask;
        return SqlTransactionAsyncAdapter.CloseAsync(_connection);
    }

    /// <summary>
    /// 异步释放自有事务。
    /// </summary>
    internal async Task ReleaseTransactionAsync()
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
    internal string GetCurrentTransactionId()
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
    internal static string ToDiagnosticValue(SqlTransactionExecutionMode mode) => mode switch
    {
        SqlTransactionExecutionMode.NativeAsync => "NativeAsync",
        SqlTransactionExecutionMode.SynchronousFallback => "SynchronousFallback",
        _ => null
    };

    /// <summary>
    /// 确保当前事务由 Query 或事务作用域拥有。
    /// </summary>
    /// <param name="operation">尝试执行的事务操作。</param>
    internal void EnsureOwnedTransaction(string operation)
    {
        if (_transactionOwnership == SqlResourceOwnership.External)
            throw new InvalidOperationException($"当前事务由外部所有者管理，Query 不能{operation}该事务。");
    }

    /// <summary>
    /// 绑定外部事务延迟解析器。
    /// </summary>
    /// <param name="resolver">外部事务解析器。</param>
    internal void BindExternalTransactionResolver(Func<IDbTransaction> resolver) =>
        _externalTransactionResolver = resolver;

    /// <summary>
    /// 标记由显式 SQL 事务作用域创建的 owner Query，使其保持独立连接所有权。
    /// </summary>
    internal void SetExplicitTransactionScopeMode() => _unitOfWorkCoordinator.SetExplicitTransactionScopeMode();

    /// <summary>在首次执行时关联工作单元并获取共享资源租约。</summary>
    internal IDisposable AcquireUnitOfWorkResourceLease() => _unitOfWorkCoordinator.AcquireResourceLease();

    /// <summary>为执行过程关联工作单元连接资源。</summary>
    internal void AttachUnitOfWorkResourceForExecution(IUnitOfWorkScope scope) =>
        _unitOfWorkCoordinator.AttachResourceForExecution(scope);

    /// <summary>在预取消路径登记工作单元参与关系并标记回滚。</summary>
    internal void MarkPreCancelledUnitOfWorkExecution() => _unitOfWorkCoordinator.MarkPreCancelledExecution();

    /// <summary>准备工作单元连接资源并获取事务。</summary>
    internal IDbTransaction EnsureUnitOfWorkTransaction() => _unitOfWorkCoordinator.EnsureTransaction();

    /// <summary>异步准备工作单元连接资源并获取事务。</summary>
    internal Task<IDbTransaction> EnsureUnitOfWorkTransactionAsync(CancellationToken cancellationToken) =>
        _unitOfWorkCoordinator.EnsureTransactionAsync(cancellationToken);

    /// <summary>将共享资源执行失败传递到业务 root。</summary>
    internal void MarkUnitOfWorkRollbackOnly() => _unitOfWorkCoordinator.MarkRollbackOnly();

    /// <summary>获取是否已创建工作单元资源。</summary>
    internal bool HasUnitOfWorkResource => _unitOfWorkCoordinator.HasResource;

    /// <summary>获取根工作单元是否启用事务。</summary>
    internal bool IsUnitOfWorkRootTransactional => _unitOfWorkCoordinator.IsRootTransactional;

    /// <summary>
    /// 合并调用方与工作单元操作的取消令牌。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>持有有效取消令牌及所需链接源的租约。</returns>
    /// <remarks>调用方应持有租约直到 I/O 生命周期结束，并释放可能创建的链接取消源。</remarks>
    internal ISqlOperationCancellationLease CreateOperationCancellationLease(
        CancellationToken cancellationToken)
    {
        var scopeToken = UnitOfWorkOperationCancellation.GetCurrentToken(
            ServiceProvider.GetService<IUnitOfWorkScopeManager>());
        if (!scopeToken.CanBeCanceled || scopeToken == cancellationToken)
            return new OperationCancellationLease(cancellationToken, null);
        if (!cancellationToken.CanBeCanceled)
            return new OperationCancellationLease(scopeToken, null);
        var source = CancellationTokenSource.CreateLinkedTokenSource(scopeToken, cancellationToken);
        return new OperationCancellationLease(source.Token, source);
    }

    /// <summary>
    /// 持有合成取消源，直到对应命令、流或 reader 完全结束。
    /// </summary>
    internal sealed class OperationCancellationLease : ISqlOperationCancellationLease
    {
        /// <summary>
        /// 保存该取消租约对应的取消源。
        /// </summary>
        private CancellationTokenSource _source;

        /// <summary>
        /// 初始化一个 <see cref="OperationCancellationLease" /> 类型的实例。
        /// </summary>
        /// <param name="token">用于取消操作的令牌。</param>
        /// <param name="source">当前操作读取内容或状态的数据源。</param>
        internal OperationCancellationLease(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            _source = source;
        }

        /// <summary>
        /// 获取调用方令牌与业务操作令牌合成后的令牌。
        /// </summary>
        public CancellationToken Token { get; }

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _source, null)?.Dispose();
    }

    /// <summary>
    /// 将当前工作单元标记为执行失败。
    /// </summary>
    /// <remarks>通过仅回滚标记阻止失败工作单元继续提交。</remarks>
    internal void MarkUnitOfWorkExecutionFailure() => _unitOfWorkCoordinator.MarkExecutionFailure();

    /// <summary>
    /// 拒绝在 Query 已加入 Uow 后覆盖其连接或事务绑定。
    /// </summary>
    internal void EnsureNoUnitOfWorkRuntimeBinding() => _unitOfWorkCoordinator.EnsureNoRuntimeBinding();
}
