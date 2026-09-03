using System.Data.Common;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域工厂。
/// </summary>
public sealed class SqlTransactionScopeFactory : ISqlTransactionScopeFactory
{
    /// <summary>
    /// 创建事务查询对象的工厂。
    /// </summary>
    private readonly ISqlQueryFactory _queryFactory;

    /// <summary>
    /// 创建事务执行器的工厂。
    /// </summary>
    private readonly ISqlExecutorFactory _executorFactory;

    /// <summary>
    /// 初始化一个 <see cref="SqlTransactionScopeFactory"/> 类型的实例。
    /// </summary>
    /// <param name="queryFactory">创建事务查询对象的工厂。</param>
    /// <param name="executorFactory">创建事务执行器的工厂。</param>
    public SqlTransactionScopeFactory(ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory)
    {
        _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
        _executorFactory = executorFactory ?? throw new ArgumentNullException(nameof(executorFactory));
    }

    /// <inheritdoc />
    public ISqlTransactionScope Begin(string dbKey = null) => Begin(dbKey, IsolationLevel.ReadCommitted);

    /// <inheritdoc />
    public ISqlTransactionScope Begin(string dbKey, IsolationLevel isolationLevel)
    {
        ISqlQuery query = null;
        try
        {
            var context = CreateTransactionQuery(dbKey, out query);
            var transactionCapabilities = EnsureTransactionsSupported(context, query);
            var connection = GetRuntimeQuery(query).GetExecutionConnection();
            if (connection.State == ConnectionState.Closed)
                connection.Open();
            var transaction = connection.BeginTransaction(isolationLevel);
            return CreateScope(context, query, connection, transaction, SqlTransactionExecutionMode.Unknown,
                transactionCapabilities, GetRuntimeQuery(query).GetCurrentProviderKey());
        }
        catch (Exception exception)
        {
            ThrowAfterBeginFailure(exception, query);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<ISqlTransactionScope> BeginAsync(string dbKey = null, CancellationToken cancellationToken = default) =>
        BeginAsync(dbKey, IsolationLevel.ReadCommitted, cancellationToken);

    /// <inheritdoc />
    public async Task<ISqlTransactionScope> BeginAsync(string dbKey, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ISqlQuery query = null;
        try
        {
            var context = CreateTransactionQuery(dbKey, out query);
            var transactionCapabilities = EnsureTransactionsSupported(context, query);
            var connection = GetRuntimeQuery(query).GetExecutionConnection();
            if (connection.State == ConnectionState.Closed)
                await SqlTransactionAsyncAdapter.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var providerKey = GetRuntimeQuery(query).GetCurrentProviderKey();
            var transactionResult = await SqlTransactionAsyncAdapter.BeginWithModeAsync(connection, isolationLevel,
                cancellationToken, transactionCapabilities, providerKey).ConfigureAwait(false);
            return CreateScope(context, query, connection, transactionResult.Result, transactionResult.Mode,
                transactionCapabilities, providerKey);
        }
        catch (Exception exception)
        {
            await ThrowAfterBeginFailureAsync(exception, query).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 处理事务开始失败后的查询资源释放。
    /// </summary>
    /// <param name="operationException">开始事务时发生的异常。</param>
    /// <param name="query">拥有连接生命周期的查询对象。</param>
    private static void ThrowAfterBeginFailure(Exception operationException, ISqlQuery query)
    {
        try
        {
            query?.Dispose();
        }
        catch (Exception cleanupException)
        {
            throw new AggregateException(operationException, cleanupException);
        }

        ExceptionDispatchInfo.Capture(operationException).Throw();
    }

    /// <summary>
    /// 异步处理事务开始失败后的查询资源释放。
    /// </summary>
    /// <param name="operationException">开始事务时发生的异常。</param>
    /// <param name="query">拥有连接生命周期的查询对象。</param>
    private static async Task ThrowAfterBeginFailureAsync(Exception operationException, ISqlQuery query)
    {
        try
        {
            await SqlTransactionAsyncAdapter.DisposeAsync(query).ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            throw new AggregateException(operationException, cleanupException);
        }

        ExceptionDispatchInfo.Capture(operationException).Throw();
    }

    /// <summary>
    /// 创建接管当前 Query、连接和事务生命周期的作用域。
    /// </summary>
    /// <param name="context">本次事务固定使用的数据库上下文。</param>
    /// <param name="query">拥有连接资源的根查询对象。</param>
    /// <param name="connection">已打开的数据库连接。</param>
    /// <param name="transaction">已开始的数据库事务。</param>
    /// <param name="executionMode">开始事务时的实际异步执行模式。</param>
    /// <param name="transactionCapabilities">当前 Provider 的事务能力声明。</param>
    /// <param name="providerKey">当前 Provider Key。</param>
    /// <returns>绑定固定上下文和资源的事务作用域。</returns>
    private SqlTransactionScope CreateScope(DatabaseContext context, ISqlQuery query, IDbConnection connection,
        IDbTransaction transaction, SqlTransactionExecutionMode executionMode,
        SqlProviderTransactionCapabilities transactionCapabilities, string providerKey) =>
        new(context, query, connection, _queryFactory, _executorFactory, transaction, executionMode,
            transactionCapabilities, providerKey);

    /// <summary>
    /// 在访问连接前验证数据源与 Provider 的本地事务能力。
    /// </summary>
    /// <param name="context">目标数据库上下文。</param>
    /// <param name="query">用于解析 Provider Profile 的查询对象。</param>
    private static SqlProviderTransactionCapabilities EnsureTransactionsSupported(DatabaseContext context,
        ISqlQuery query)
    {
        if (context?.DataSource?.IsReadOnly == true)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction",
                context.DataSource.Key ?? context.DbKey,
                $"数据源 {context.DataSource.Key ?? context.DbKey ?? "<default>"} 是只读数据源，不支持写入或事务操作。");
        var profile = GetRuntimeQuery(query).GetCurrentProviderTransactionCapabilities();
        if (profile.SupportsTransactions == false)
            throw SqlCapabilityFailure.Create(profile.TransactionsFailureReason ??
                SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction",
                GetRuntimeQuery(query).GetCurrentProviderKey(),
                "当前 SQL Provider 不支持本地事务。请使用不依赖事务的查询操作。");
        if (context?.DataSource?.SupportsTransactions == false)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "Transaction", context.DbKey,
                $"数据源 {context.DbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
        return profile;
    }

    /// <summary>
    /// 创建并解析事务专用 Query，固定其数据库上下文快照。
    /// </summary>
    /// <param name="dbKey">目标数据源键；为空时由 Query Factory 使用当前上下文。</param>
    /// <param name="query">拥有本次事务连接生命周期的查询对象。</param>
    /// <returns>解析到的固定数据库上下文。</returns>
    private DatabaseContext CreateTransactionQuery(string dbKey, out ISqlQuery query)
    {
        if (_queryFactory is SqlQueryFactory factory)
        {
            query = factory.CreateForTransaction(dbKey, out var factoryContext);
            return factoryContext;
        }
        query = _queryFactory.Create(dbKey);
        var context = query is SqlQueryBase sqlQuery ? sqlQuery.GetDatabaseContext() : null;
        if (context == null)
            throw new InvalidOperationException("事务查询对象必须提供固定数据库上下文");
        return context;
    }

    /// <summary>
    /// 获取 Dapper SQL 查询实现。
    /// </summary>
    /// <param name="query">事务作用域使用的查询对象。</param>
    /// <returns>可访问 Dapper 专用运行时状态的查询对象。</returns>
    private static SqlQueryBase GetRuntimeQuery(ISqlQuery query) => query as SqlQueryBase ??
        throw new InvalidOperationException("SQL 事务作用域仅支持 Dapper SQL 查询实现。");

    /// <summary>
    /// SQL 事务作用域。
    /// </summary>
    private sealed class SqlTransactionScope : ISqlTransactionScope, ISqlTransactionScopeRuntime
    {
        /// <summary>
        /// 拥有事务连接生命周期的根查询对象。
        /// </summary>
        private readonly ISqlQuery _ownerQuery;

        /// <summary>
        /// 当前事务使用的数据库连接。
        /// </summary>
        private readonly IDbConnection _connection;

        /// <summary>
        /// 当前事务。
        /// </summary>
        private readonly IDbTransaction _transaction;

        /// <summary>
        /// 创建事务子查询的工厂。
        /// </summary>
        private readonly ISqlQueryFactory _queryFactory;

        /// <summary>
        /// 创建事务子执行器的工厂。
        /// </summary>
        private readonly ISqlExecutorFactory _executorFactory;

        /// <summary>
        /// 当前事务作用域创建的子资源集合。
        /// </summary>
        private readonly List<IDisposable> _children = new();

        /// <summary>
        /// 保护事务执行期间资源访问的租约。
        /// </summary>
        private readonly SqlTransactionScopeLease _lease;

        /// <summary>
        /// 当前事务作用域冻结的数据库上下文。
        /// </summary>
        private readonly DatabaseContext _context;

        /// <summary>
        /// 当前事务 Provider 的事务能力声明。
        /// </summary>
        private readonly SqlProviderTransactionCapabilities _transactionCapabilities;

        /// <summary>
        /// 当前事务 Provider Key。
        /// </summary>
        private readonly string _providerKey;

        /// <summary>
        /// 保护作用域状态、子对象集合和资源释放状态的同步锁。
        /// </summary>
        private readonly object _syncRoot = new();

        /// <summary>
        /// 当前事务作用域状态。
        /// </summary>
        private SqlTransactionScopeState _state;

        /// <summary>
        /// 是否已完成事务资源释放。
        /// </summary>
        private bool _resourcesReleased;

        /// <summary>
        /// 是否已由调用方显式释放当前作用域。
        /// </summary>
        private bool _isExplicitlyDisposed;

        /// <summary>
        /// 指示已有调用独占完成或释放当前作用域。
        /// </summary>
        private bool _completionInProgress;

        /// <summary>
        /// 初始化事务作用域并冻结资源和数据库上下文。
        /// </summary>
        /// <param name="context">事务固定数据库上下文。</param>
        /// <param name="ownerQuery">拥有连接生命周期的根查询对象。</param>
        /// <param name="connection">已打开的数据库连接。</param>
        /// <param name="queryFactory">用于创建事务子查询的工厂。</param>
        /// <param name="executorFactory">用于创建事务子执行器的工厂。</param>
        /// <param name="transaction">当前数据库事务。</param>
        /// <param name="executionMode">开始事务时的实际异步执行模式。</param>
        /// <param name="transactionCapabilities">当前 Provider 的事务能力声明。</param>
        /// <param name="providerKey">当前 Provider Key。</param>
        public SqlTransactionScope(DatabaseContext context, ISqlQuery ownerQuery, IDbConnection connection,
            ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory, IDbTransaction transaction,
            SqlTransactionExecutionMode executionMode, SqlProviderTransactionCapabilities transactionCapabilities,
            string providerKey)
        {
            _context = DatabaseContextSnapshot.Create(context) ?? throw new ArgumentNullException(nameof(context));
            _ownerQuery = ownerQuery ?? throw new ArgumentNullException(nameof(ownerQuery));
            _connection = connection ?? throw new InvalidOperationException("SQL 连接不能为空");
            _transaction = transaction ?? throw new InvalidOperationException("SQL 事务不能为空");
            _queryFactory = queryFactory;
            _executorFactory = executorFactory;
            _transactionCapabilities = transactionCapabilities ?? throw new ArgumentNullException(nameof(transactionCapabilities));
            _providerKey = providerKey ?? throw new ArgumentNullException(nameof(providerKey));
            TransactionId = Guid.NewGuid().ToString("N");
            _lease = new SqlTransactionScopeLease(TransactionId, executionMode);
            _state = SqlTransactionScopeState.Active;
        }

        /// <inheritdoc />
        public string DbKey => _context.DbKey;

        /// <inheritdoc />
        public DatabaseType DatabaseType => _context.DataSource?.DatabaseType ?? default;

        /// <inheritdoc />
        public string MappingProfile => _context.MappingProfile;

        /// <inheritdoc />
        public SqlReadPreference ReadPreference => _context.ReadPreference;

        /// <inheritdoc />
        public IsolationLevel IsolationLevel => _transaction.IsolationLevel;

        /// <inheritdoc />
        DatabaseContext ISqlTransactionScopeRuntime.DatabaseContext => DatabaseContextSnapshot.Create(_context);

        /// <inheritdoc />
        IDbConnection ISqlTransactionScopeRuntime.Connection => _connection;

        /// <inheritdoc />
        IDbTransaction ISqlTransactionScopeRuntime.Transaction => _transaction;

        /// <inheritdoc />
        string ISqlTransactionScopeRuntime.ExecutionMode => _lease.ExecutionMode;

        /// <inheritdoc />
        public bool IsCompleted
        {
            get
            {
                lock (_syncRoot)
                    return _state != SqlTransactionScopeState.Active;
            }
        }

        /// <inheritdoc />
        public string TransactionId { get; }

        /// <inheritdoc />
        public ISqlQuery CreateQuery()
        {
            return CreateAndBindChild(() => _queryFactory is SqlQueryFactory factory
                ? factory.CreateForTransaction(_context)
                : _queryFactory.Create(DbKey));
        }

        /// <inheritdoc />
        public ISqlExecutor CreateExecutor()
        {
            return CreateAndBindChild(() => _executorFactory is SqlExecutorFactory factory
                ? factory.CreateForTransaction(_context)
                : _executorFactory.Create(DbKey));
        }

        /// <inheritdoc />
        public void Commit()
        {
            if (TryBeginCompletion(SqlTransactionScopeState.Committed) == false)
                return;
            Exception operationException = null;
            var finalState = SqlTransactionScopeState.Committed;
            try
            {
                _transaction.Commit();
            }
            catch (Exception exception)
            {
                operationException = TryRollbackAfterCommitFailure(exception);
                finalState = SqlTransactionScopeState.Faulted;
            }
            try
            {
                ThrowAfterCleanup(operationException);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryBeginCompletion(SqlTransactionScopeState.Committed) == false)
                return;
            Exception operationException = null;
            var finalState = SqlTransactionScopeState.Committed;
            try
            {
                var result = await SqlTransactionAsyncAdapter.CommitWithModeAsync(_transaction, cancellationToken,
                    _transactionCapabilities, _providerKey)
                    .ConfigureAwait(false);
                _lease.SetExecutionMode(result.Mode);
            }
            catch (Exception exception)
            {
                operationException = await TryRollbackAfterCommitFailureAsync(exception).ConfigureAwait(false);
                finalState = SqlTransactionScopeState.Faulted;
            }
            try
            {
                await ThrowAfterCleanupAsync(operationException).ConfigureAwait(false);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <inheritdoc />
        public void Rollback()
        {
            if (TryBeginCompletion(SqlTransactionScopeState.RolledBack) == false)
                return;
            Exception operationException = null;
            var finalState = SqlTransactionScopeState.RolledBack;
            try
            {
                _transaction.Rollback();
            }
            catch (Exception exception)
            {
                operationException = exception;
                finalState = SqlTransactionScopeState.Faulted;
            }
            try
            {
                ThrowAfterCleanup(operationException);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryBeginCompletion(SqlTransactionScopeState.RolledBack) == false)
                return;
            Exception operationException = null;
            var finalState = SqlTransactionScopeState.RolledBack;
            try
            {
                var result = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(_transaction, cancellationToken,
                    _transactionCapabilities, _providerKey)
                    .ConfigureAwait(false);
                _lease.SetExecutionMode(result.Mode);
            }
            catch (Exception exception)
            {
                operationException = exception;
                finalState = SqlTransactionScopeState.Faulted;
            }
            try
            {
                await ThrowAfterCleanupAsync(operationException).ConfigureAwait(false);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <summary>
        /// 异步释放事务作用域及其拥有的事务资源。
        /// </summary>
        /// <remarks>
        /// 若当前作用域创建的子 Query 或 Executor 持有活动执行租约，释放会抛出异常且不改变作用域状态、租约或事务资源。
        /// 调用方释放子对象后可重试。
        /// </remarks>
        public void Dispose()
        {
            if (TryBeginDispose(out var shouldRollback, out var finalState) == false)
                return;
            Exception rollbackException = null;
            if (shouldRollback)
            {
                try
                {
                    _transaction.Rollback();
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                    finalState = SqlTransactionScopeState.Faulted;
                }
            }
            try
            {
                ThrowAfterCleanup(rollbackException);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <summary>
        /// 异步释放事务作用域及其拥有的事务资源。
        /// </summary>
        /// <remarks>
        /// 若当前作用域创建的子 Query 或 Executor 持有活动执行租约，释放会抛出异常且不改变作用域状态、租约或事务资源。
        /// 调用方释放子对象后可重试。
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (TryBeginDispose(out var shouldRollback, out var finalState) == false)
                return;
            Exception rollbackException = null;
            if (shouldRollback)
            {
                try
                {
                    var result = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(_transaction,
                        CancellationToken.None, _transactionCapabilities, _providerKey).ConfigureAwait(false);
                    _lease.SetExecutionMode(result.Mode);
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                    finalState = SqlTransactionScopeState.Faulted;
                }
            }
            try
            {
                await ThrowAfterCleanupAsync(rollbackException).ConfigureAwait(false);
            }
            finally
            {
                EndCompletion(finalState);
            }
        }

        /// <summary>
        /// 原子开始提交或回滚，并阻止后续子对象创建与执行。
        /// </summary>
        /// <param name="expectedState">本次完成操作期望到达的终态。</param>
        /// <returns>是否由当前调用获得完成操作所有权。</returns>
        private bool TryBeginCompletion(SqlTransactionScopeState expectedState)
        {
            lock (_syncRoot)
            {
                EnsureCanComplete(expectedState);
                if (_state == expectedState)
                    return false;
                if (_completionInProgress)
                    throw new InvalidOperationException("SQL 事务作用域正在完成，不能并发提交、回滚或释放。");
                _lease.InvalidateWhenNoActiveExecution();
                _completionInProgress = true;
                return true;
            }
        }

        /// <summary>
        /// 原子开始释放，并在需要时保留回滚所有权。
        /// </summary>
        /// <param name="shouldRollback">是否应回滚活动事务。</param>
        /// <param name="finalState">释放成功后应设置的事务状态。</param>
        /// <returns>是否由当前调用获得释放操作所有权。</returns>
        private bool TryBeginDispose(out bool shouldRollback, out SqlTransactionScopeState finalState)
        {
            lock (_syncRoot)
            {
                shouldRollback = false;
                finalState = _state;
                if (_isExplicitlyDisposed)
                    return false;
                if (_completionInProgress)
                    throw new InvalidOperationException("SQL 事务作用域正在完成，不能并发提交、回滚或释放。");
                _lease.InvalidateWhenNoActiveExecution();
                _completionInProgress = true;
                _isExplicitlyDisposed = true;
                shouldRollback = _state == SqlTransactionScopeState.Active;
                if (shouldRollback)
                    finalState = SqlTransactionScopeState.RolledBack;
                return true;
            }
        }

        /// <summary>
        /// 收口本次独占完成操作的最终状态。
        /// </summary>
        /// <param name="state">要写入的最终事务状态。</param>
        private void EndCompletion(SqlTransactionScopeState state)
        {
            lock (_syncRoot)
            {
                _state = state;
                _completionInProgress = false;
            }
        }

        /// <summary>
        /// 验证事务作用域可以进入指定的完成状态。
        /// </summary>
        /// <param name="expectedState">本次完成操作期望到达的终态。</param>
        private void EnsureCanComplete(SqlTransactionScopeState expectedState)
        {
            if (_isExplicitlyDisposed)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active && _state != expectedState)
                throw new InvalidOperationException("SQL 事务作用域已完成，不能重复提交或回滚。");
        }

        /// <summary>
        /// 确保事务作用域仍可创建和绑定子对象。
        /// </summary>
        private void ThrowIfInactive()
        {
            if (_isExplicitlyDisposed || _resourcesReleased)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active || _completionInProgress)
                throw new InvalidOperationException("SQL 事务作用域已结束，不能继续创建 Query 或 Executor。");
        }

        /// <summary>
        /// 创建并原子绑定事务作用域子对象。
        /// </summary>
        /// <remarks>
        /// 绑定失败时会释放已创建的子对象；若释放也失败，则按绑定异常在前、释放异常在后的顺序聚合。
        /// </remarks>
        /// <typeparam name="TService">子对象类型。</typeparam>
        /// <param name="creator">子对象创建委托。</param>
        /// <returns>已绑定事务资源的子对象。</returns>
        private TService CreateAndBindChild<TService>(Func<TService> creator) where TService : class, IDisposable
        {
            TService child = null;
            try
            {
                child = creator();
                lock (_syncRoot)
                {
                    ThrowIfInactive();
                    BindTransactionContext(child);
                    _children.Add(child);
                }
                return child;
            }
            catch (Exception bindException)
            {
                if (child == null)
                    ExceptionDispatchInfo.Capture(bindException).Throw();
                try
                {
                    child.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(bindException, cleanupException);
                }

                ExceptionDispatchInfo.Capture(bindException).Throw();
                throw;
            }
        }

        /// <summary>
        /// 将当前作用域的固定上下文、连接、事务与租约绑定到子 Query。
        /// </summary>
        /// <param name="resource">由事务作用域创建的子资源。</param>
        /// <remarks>
        /// 仅框架内部 Query 实现支持运行时资源绑定；其他 <see cref="ISqlExecutor"/> 或 <see cref="ISqlQuery"/>
        /// 实现会被拒绝，避免脱离当前事务执行。
        /// </remarks>
        private void BindTransactionContext(IDisposable resource)
        {
            if (resource is not SqlQueryBase query)
                throw new InvalidOperationException("SQL 事务作用域仅支持 Dapper SQL 查询实现。");
            query.SetTransactionContext(_context, _connection, _transaction, _lease);
        }

        /// <summary>
        /// 提交失败后尝试回滚事务。
        /// </summary>
        /// <param name="commitException">提交异常。</param>
        /// <returns>保留提交异常或聚合提交、回滚异常。</returns>
        private Exception TryRollbackAfterCommitFailure(Exception commitException)
        {
            try
            {
                _transaction.Rollback();
                return commitException;
            }
            catch (Exception rollbackException)
            {
                return new AggregateException(commitException, rollbackException);
            }
        }

        /// <summary>
        /// 异步提交失败后尝试回滚事务。
        /// </summary>
        /// <param name="commitException">提交异常。</param>
        /// <returns>保留提交异常或聚合提交、回滚异常。</returns>
        private async Task<Exception> TryRollbackAfterCommitFailureAsync(Exception commitException)
        {
            try
            {
                var result = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(_transaction,
                    CancellationToken.None, _transactionCapabilities, _providerKey).ConfigureAwait(false);
                _lease.SetExecutionMode(result.Mode);
                return commitException;
            }
            catch (Exception rollbackException)
            {
                return new AggregateException(commitException, rollbackException);
            }
        }

        /// <summary>
        /// 释放事务作用域资源并传播操作或清理异常。
        /// </summary>
        /// <param name="operationException">完成操作期间发生的主异常。</param>
        private void ThrowAfterCleanup(Exception operationException)
        {
            var exceptions = new List<Exception>();
            AddException(exceptions, operationException);
            if (_resourcesReleased)
            {
                ThrowCollected(exceptions);
                return;
            }
            _resourcesReleased = true;
            DisposeChildren(exceptions);
            TryCleanup(_transaction.Dispose, exceptions);
            TryCleanup(_ownerQuery.Dispose, exceptions);
            ThrowCollected(exceptions);
        }

        /// <summary>
        /// 异步释放事务作用域资源并传播操作或清理异常。
        /// </summary>
        /// <param name="operationException">完成操作期间发生的主异常。</param>
        private async Task ThrowAfterCleanupAsync(Exception operationException)
        {
            var exceptions = new List<Exception>();
            AddException(exceptions, operationException);
            if (_resourcesReleased)
            {
                ThrowCollected(exceptions);
                return;
            }
            _resourcesReleased = true;
            await DisposeChildrenAsync(exceptions).ConfigureAwait(false);
            await TryCleanupAsync(() => SqlTransactionAsyncAdapter.DisposeAsync(_transaction), exceptions).ConfigureAwait(false);
            await TryCleanupAsync(() => SqlTransactionAsyncAdapter.DisposeAsync(_ownerQuery), exceptions).ConfigureAwait(false);
            ThrowCollected(exceptions);
        }

        /// <summary>
        /// 释放事务作用域创建的全部子资源并收集异常。
        /// </summary>
        /// <param name="exceptions">用于收集释放异常的列表。</param>
        private void DisposeChildren(List<Exception> exceptions)
        {
            foreach (var child in _children)
                TryCleanup(child.Dispose, exceptions);
            _children.Clear();
        }

        /// <summary>
        /// 异步释放事务作用域创建的全部子资源并收集异常。
        /// </summary>
        /// <param name="exceptions">用于收集释放异常的列表。</param>
        private async Task DisposeChildrenAsync(List<Exception> exceptions)
        {
            foreach (var child in _children)
                await TryCleanupAsync(() => SqlTransactionAsyncAdapter.DisposeAsync(child), exceptions).ConfigureAwait(false);
            _children.Clear();
        }

        /// <summary>
        /// 执行同步清理操作并收集其异常。
        /// </summary>
        /// <param name="action">待执行的清理操作。</param>
        /// <param name="exceptions">用于收集清理异常的列表。</param>
        private static void TryCleanup(Action action, List<Exception> exceptions)
        {
            try { action(); }
            catch (Exception exception) { AddException(exceptions, exception); }
        }

        /// <summary>
        /// 执行异步清理操作并收集其异常。
        /// </summary>
        /// <param name="action">待执行的异步清理操作。</param>
        /// <param name="exceptions">用于收集清理异常的列表。</param>
        private static async Task TryCleanupAsync(Func<Task> action, List<Exception> exceptions)
        {
            try { await action().ConfigureAwait(false); }
            catch (Exception exception) { AddException(exceptions, exception); }
        }

        /// <summary>
        /// 将异常展开并追加到异常集合。
        /// </summary>
        /// <param name="exceptions">用于收集异常的列表。</param>
        /// <param name="exception">待追加的异常。</param>
        private static void AddException(List<Exception> exceptions, Exception exception)
        {
            if (exception == null)
                return;
            if (exception is AggregateException aggregate)
                exceptions.AddRange(aggregate.Flatten().InnerExceptions);
            else
                exceptions.Add(exception);
        }

        /// <summary>
        /// 按数量重新抛出收集到的异常。
        /// </summary>
        /// <param name="exceptions">已收集的异常列表。</param>
        private static void ThrowCollected(List<Exception> exceptions)
        {
            if (exceptions.Count == 1)
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);
        }
    }

    /// <summary>
    /// 事务作用域的生命周期状态。
    /// </summary>
    private enum SqlTransactionScopeState
    {
        /// <summary>
        /// 事务仍处于活动状态。
        /// </summary>
        Active,

        /// <summary>
        /// 事务已提交。
        /// </summary>
        Committed,

        /// <summary>
        /// 事务已回滚。
        /// </summary>
        RolledBack,

        /// <summary>
        /// 事务完成或清理失败。
        /// </summary>
        Faulted,
    }
}

/// <summary>
/// SQL 事务作用域内部资源访问契约。
/// </summary>
internal interface ISqlTransactionScopeRuntime
{
    /// <summary>
    /// 获取冻结的数据库上下文。
    /// </summary>
    DatabaseContext DatabaseContext { get; }

    /// <summary>
    /// 获取事务连接。
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// 获取数据库事务。
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// 当前事务最近一次异步事务操作的执行模式。
    /// </summary>
    string ExecutionMode { get; }
}

internal enum SqlTransactionExecutionMode
{
    Unknown = 0,
    NativeAsync = 1,
    SynchronousFallback = 2
}

/// <summary>
/// ADO.NET 异步事务调用结果。
/// </summary>
/// <typeparam name="T">调用结果类型。</typeparam>
internal sealed class SqlTransactionAsyncOperationResult<T>
{
    /// <summary>
    /// 初始化异步事务调用结果。
    /// </summary>
    /// <param name="result">调用结果。</param>
    /// <param name="mode">实际执行模式。</param>
    public SqlTransactionAsyncOperationResult(T result, SqlTransactionExecutionMode mode)
    {
        Result = result;
        Mode = mode;
    }

    /// <summary>
    /// 调用结果。
    /// </summary>
    public T Result { get; }

    /// <summary>
    /// 实际执行模式。
    /// </summary>
    public SqlTransactionExecutionMode Mode { get; }
}

/// <summary>
/// ADO.NET 异步事务成员适配器。
/// </summary>
internal static class SqlTransactionAsyncAdapter
{
    /// <summary>
    /// 异步打开连接；非 <see cref="DbConnection"/> 实现回退为同步打开。
    /// </summary>
    /// <param name="connection">待打开的数据库连接。</param>
    /// <param name="cancellationToken">打开操作使用的取消令牌。</param>
    public static Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
            return dbConnection.OpenAsync(cancellationToken);
        connection.Open();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 异步开始事务；Provider 未公开兼容异步成员时回退为同步开始。
    /// </summary>
    /// <param name="connection">已打开的数据库连接。</param>
    /// <param name="isolationLevel">请求的事务隔离级别。</param>
    /// <param name="cancellationToken">开始事务使用的取消令牌。</param>
    /// <returns>已开始的数据库事务。</returns>
    public static async Task<IDbTransaction> BeginAsync(IDbConnection connection, IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var result = await BeginWithModeAsync(connection, isolationLevel, cancellationToken).ConfigureAwait(false);
        return result.Result;
    }

    /// <summary>
    /// 异步开始事务并返回实际执行模式。
    /// </summary>
    /// <param name="connection">已打开的数据库连接。</param>
    /// <param name="isolationLevel">请求的事务隔离级别。</param>
    /// <param name="cancellationToken">开始事务使用的取消令牌。</param>
    /// <returns>事务和实际执行模式。</returns>
    internal static async Task<SqlTransactionAsyncOperationResult<IDbTransaction>> BeginWithModeAsync(
        IDbConnection connection, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await BeginWithModeAsync(connection, isolationLevel, cancellationToken, null, null)
            .ConfigureAwait(false);

    /// <summary>
    /// 异步开始事务，并按 Provider Profile 校验实际异步成员。
    /// </summary>
    /// <param name="connection">已打开的数据库连接。</param>
    /// <param name="isolationLevel">请求的事务隔离级别。</param>
    /// <param name="cancellationToken">开始事务使用的取消令牌。</param>
    /// <param name="capabilities">当前 Provider 的事务能力声明。</param>
    /// <param name="providerKey">当前 Provider Key。</param>
    /// <returns>事务和实际执行模式。</returns>
    internal static async Task<SqlTransactionAsyncOperationResult<IDbTransaction>> BeginWithModeAsync(
        IDbConnection connection, IsolationLevel isolationLevel, CancellationToken cancellationToken,
        SqlProviderTransactionCapabilities capabilities, string providerKey)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasNativeMember = HasProviderAsyncMember(connection, "BeginTransactionAsync", isolationLevel,
            cancellationToken);
        EnsureProfileMatches(capabilities?.SupportsNativeAsyncBegin == true, hasNativeMember, "Begin", providerKey);
        if (capabilities == null && hasNativeMember)
        {
            var invocation = await TryInvokeAsync(connection, "BeginTransactionAsync", isolationLevel,
                cancellationToken).ConfigureAwait(false);
            if (invocation.Result is IDbTransaction transaction)
                return new SqlTransactionAsyncOperationResult<IDbTransaction>(transaction,
                    SqlTransactionExecutionMode.NativeAsync);
        }
        if (capabilities?.SupportsNativeAsyncBegin == true && hasNativeMember)
        {
            var invocation = await TryInvokeAsync(connection, "BeginTransactionAsync", isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            if (invocation.Result is IDbTransaction transaction)
                return new SqlTransactionAsyncOperationResult<IDbTransaction>(transaction,
                    SqlTransactionExecutionMode.NativeAsync);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new SqlTransactionAsyncOperationResult<IDbTransaction>(connection.BeginTransaction(isolationLevel),
            SqlTransactionExecutionMode.SynchronousFallback);
    }

    /// <summary>
    /// 异步关闭连接；未公开异步关闭成员时回退为同步关闭。
    /// </summary>
    /// <param name="connection">待关闭的数据库连接。</param>
    public static async Task CloseAsync(IDbConnection connection)
    {
        var invocation = await TryInvokeAsync(connection, "CloseAsync").ConfigureAwait(false);
        if (invocation.IsInvoked == false)
            connection.Close();
    }

    /// <summary>
    /// 异步提交事务，并在同步回退前检查取消状态。
    /// </summary>
    /// <param name="transaction">待提交的数据库事务。</param>
    /// <param name="cancellationToken">提交操作使用的取消令牌。</param>
    public static async Task CommitAsync(IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await CommitWithModeAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步提交事务并返回实际执行模式。
    /// </summary>
    /// <param name="transaction">待提交的数据库事务。</param>
    /// <param name="cancellationToken">提交操作使用的取消令牌。</param>
    /// <returns>实际执行模式。</returns>
    internal static Task<SqlTransactionAsyncOperationResult<object>> CommitWithModeAsync(
        IDbTransaction transaction, CancellationToken cancellationToken)
        => CommitWithModeAsync(transaction, cancellationToken, null, null);

    /// <summary>
    /// 异步提交事务，并按 Provider Profile 校验实际异步成员。
    /// </summary>
    /// <param name="transaction">待提交的数据库事务。</param>
    /// <param name="cancellationToken">提交操作使用的取消令牌。</param>
    /// <param name="capabilities">当前 Provider 的事务能力声明。</param>
    /// <param name="providerKey">当前 Provider Key。</param>
    /// <returns>实际执行模式。</returns>
    internal static Task<SqlTransactionAsyncOperationResult<object>> CommitWithModeAsync(
        IDbTransaction transaction, CancellationToken cancellationToken,
        SqlProviderTransactionCapabilities capabilities, string providerKey)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return InvokeOrRunWithModeAsync(transaction, "CommitAsync", transaction.Commit, cancellationToken,
            capabilities?.SupportsNativeAsyncCommit, providerKey);
    }

    /// <summary>
    /// 异步回滚事务；Provider 未公开兼容异步成员时回退为同步回滚。
    /// </summary>
    /// <param name="transaction">待回滚的数据库事务。</param>
    /// <param name="cancellationToken">回滚操作使用的取消令牌。</param>
    public static async Task RollbackAsync(IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await RollbackWithModeAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步回滚事务并返回实际执行模式。
    /// </summary>
    /// <param name="transaction">待回滚的数据库事务。</param>
    /// <param name="cancellationToken">回滚操作使用的取消令牌。</param>
    /// <returns>实际执行模式。</returns>
    internal static Task<SqlTransactionAsyncOperationResult<object>> RollbackWithModeAsync(
        IDbTransaction transaction, CancellationToken cancellationToken)
        => RollbackWithModeAsync(transaction, cancellationToken, null, null);

    /// <summary>
    /// 异步回滚事务，并按 Provider Profile 校验实际异步成员。
    /// </summary>
    /// <param name="transaction">待回滚的数据库事务。</param>
    /// <param name="cancellationToken">回滚操作使用的取消令牌。</param>
    /// <param name="capabilities">当前 Provider 的事务能力声明。</param>
    /// <param name="providerKey">当前 Provider Key。</param>
    /// <returns>实际执行模式。</returns>
    internal static Task<SqlTransactionAsyncOperationResult<object>> RollbackWithModeAsync(
        IDbTransaction transaction, CancellationToken cancellationToken,
        SqlProviderTransactionCapabilities capabilities, string providerKey)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return InvokeOrRunWithModeAsync(transaction, "RollbackAsync", transaction.Rollback, cancellationToken,
            capabilities?.SupportsNativeAsyncRollback, providerKey);
    }

    /// <summary>
    /// 优先异步释放资源；不支持异步释放时回退为同步释放。
    /// </summary>
    /// <param name="resource">待释放的资源；可为 <see langword="null"/>。</param>
    public static async Task DisposeAsync(object resource)
    {
        if (resource is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }
        if (resource is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// 调用对象的异步成员；不存在时执行同步回退操作。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="methodName">异步成员名称。</param>
    /// <param name="fallback">异步成员不存在时执行的同步操作。</param>
    /// <param name="cancellationToken">调用操作使用的取消令牌。</param>
    /// <param name="declaredNative">Provider 是否声明该操作支持原生异步。</param>
    /// <param name="providerKey">当前 Provider Key。</param>
    /// <returns>调用结果及实际执行模式。</returns>
    private static async Task<SqlTransactionAsyncOperationResult<object>> InvokeOrRunWithModeAsync(object target,
        string methodName, Action fallback, CancellationToken cancellationToken, bool? declaredNative,
        string providerKey)
    {
        var hasNativeMember = HasProviderAsyncMember(target, methodName, cancellationToken);
        if (declaredNative == true && hasNativeMember == false)
            throw CreateImplementationGap(methodName, providerKey);
        if (declaredNative == null && hasNativeMember)
        {
            await TryInvokeAsync(target, methodName, cancellationToken).ConfigureAwait(false);
            return new SqlTransactionAsyncOperationResult<object>(null, SqlTransactionExecutionMode.NativeAsync);
        }
        if (declaredNative == true && hasNativeMember)
        {
            await TryInvokeAsync(target, methodName, cancellationToken).ConfigureAwait(false);
            return new SqlTransactionAsyncOperationResult<object>(null, SqlTransactionExecutionMode.NativeAsync);
        }
        if (declaredNative == false || declaredNative == null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fallback();
            return new SqlTransactionAsyncOperationResult<object>(null,
                SqlTransactionExecutionMode.SynchronousFallback);
        }
        throw CreateImplementationGap(methodName, providerKey);
    }

    /// <summary>
    /// 判断目标对象是否存在由 Provider 提供的异步事务成员。
    /// </summary>
    private static bool HasProviderAsyncMember(object target, string methodName, params object[] arguments)
    {
        if (target == null)
            return false;
        return target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length)
            .Where(method => IsProviderAsyncMember(target, methodName, method))
            .Any(method => ParametersMatch(method.GetParameters(), arguments));
    }

    /// <summary>
    /// 校验 Profile 声明与运行时异步成员是否一致。
    /// </summary>
    private static void EnsureProfileMatches(bool declaredNative, bool hasNativeMember, string operation,
        string providerKey)
    {
        if (declaredNative && hasNativeMember == false)
            throw CreateImplementationGap(operation, providerKey);
    }

    /// <summary>
    /// 创建 Provider 声明与实现不一致的明确异常。
    /// </summary>
    private static NotSupportedException CreateImplementationGap(string operation, string providerKey) =>
        SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderImplementationGap,
            $"Transaction:{operation}", providerKey,
            $"Provider {providerKey ?? "<unknown>"} 声明支持原生异步事务 {operation}，但运行时对象未提供对应成员。" +
            "[ProviderImplementationGap][ProfileMismatch]");

    /// <summary>
    /// 尝试调用对象公开的异步成员。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="methodName">异步成员名称。</param>
    /// <param name="arguments">异步成员参数。</param>
    /// <returns>异步成员是否命中及其完成后的结果。</returns>
    private static async Task<AsyncInvocationResult> TryInvokeAsync(object target, string methodName,
        params object[] arguments)
    {
        var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length)
            .Where(method => IsProviderAsyncMember(target, methodName, method));
        var methodInfo = methods.FirstOrDefault(method => ParametersMatch(method.GetParameters(), arguments));
        if (methodInfo == null)
            return AsyncInvocationResult.NotInvoked;
        try
        {
            var result = methodInfo.Invoke(target, arguments);
            return new AsyncInvocationResult(true, await AwaitResult(result).ConfigureAwait(false));
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// 判断异步成员是否由 Provider 覆盖实现，排除 ADO.NET 基类的同步默认包装。
    /// </summary>
    /// <param name="target">异步成员目标对象。</param>
    /// <param name="methodName">异步成员名称。</param>
    /// <param name="method">候选异步成员。</param>
    /// <returns>由具体 Provider 覆盖实现时返回 true。</returns>
    private static bool IsProviderAsyncMember(object target, string methodName, MethodInfo method)
    {
        if (methodName == "BeginTransactionAsync" && target is DbConnection &&
            method.DeclaringType == typeof(DbConnection))
        {
            var beginMethod = target.GetType().GetMethod("BeginDbTransactionAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return beginMethod?.DeclaringType != typeof(DbConnection);
        }
        if ((methodName == "CommitAsync" || methodName == "RollbackAsync") && target is DbTransaction &&
            method.DeclaringType == typeof(DbTransaction))
            return false;
        return true;
    }

    /// <summary>
    /// 判断反射方法的参数类型是否与实际参数匹配。
    /// </summary>
    /// <param name="parameters">反射方法声明的参数信息。</param>
    /// <param name="arguments">准备传入的实际参数。</param>
    /// <returns>全部参数兼容时返回 true；否则返回 false。</returns>
    private static bool ParametersMatch(ParameterInfo[] parameters, object[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] != null && parameters[index].ParameterType.IsInstanceOfType(arguments[index]) == false)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 等待异步调用结果并提取其最终值。
    /// </summary>
    /// <param name="result">反射调用返回的异步或同步结果。</param>
    /// <returns>异步操作完成后的结果值；无结果或输入为空时返回 null。</returns>
    private static async Task<object> AwaitResult(object result)
    {
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }
        if (result == null)
            return null;
        var asTask = result.GetType().GetMethod("AsTask", Type.EmptyTypes);
        if (asTask?.Invoke(result, null) is Task valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return valueTask.GetType().GetProperty("Result")?.GetValue(valueTask);
        }
        return result;
    }

    /// <summary>
    /// 异步成员调用结果。
    /// </summary>
    private sealed class AsyncInvocationResult
    {
        /// <summary>
        /// 未命中异步成员的调用结果。
        /// </summary>
        public static readonly AsyncInvocationResult NotInvoked = new(false, null);

        /// <summary>
        /// 初始化异步成员调用结果。
        /// </summary>
        /// <param name="isInvoked">是否命中并调用异步成员。</param>
        /// <param name="result">异步成员完成后的结果。</param>
        public AsyncInvocationResult(bool isInvoked, object result)
        {
            IsInvoked = isInvoked;
            Result = result;
        }

        /// <summary>
        /// 是否命中并调用异步成员。
        /// </summary>
        public bool IsInvoked { get; }

        /// <summary>
        /// 异步成员完成后的结果。
        /// </summary>
        public object Result { get; }
    }
}