using System.Data.Common;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域工厂。
/// </summary>
public sealed class SqlTransactionScopeFactory : ISqlTransactionScopeFactory
{
    private readonly ISqlQueryFactory _queryFactory;
    private readonly ISqlExecutorFactory _executorFactory;
    /// <summary>
    /// 初始化一个<see cref="SqlTransactionScopeFactory"/>类型的实例。
    /// </summary>
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
        var query = CreateTransactionQuery(dbKey, out var context);
        try
        {
            EnsureTransactionsSupported(context, query);
            var connection = SqlQueryRuntimeBridge.GetOrCreateConnection(query);
            if (connection.State == ConnectionState.Closed)
                connection.Open();
            var transaction = connection.BeginTransaction(isolationLevel);
            return CreateScope(context, query, connection, transaction);
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
        var query = CreateTransactionQuery(dbKey, out var context);
        try
        {
            EnsureTransactionsSupported(context, query);
            var connection = SqlQueryRuntimeBridge.GetOrCreateConnection(query);
            if (connection.State == ConnectionState.Closed)
                await SqlTransactionAsyncAdapter.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = await SqlTransactionAsyncAdapter.BeginAsync(connection, isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            return CreateScope(context, query, connection, transaction);
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
    /// <returns>表示资源释放完成的异步操作。</returns>
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
    /// <returns>绑定固定上下文和资源的事务作用域。</returns>
    private SqlTransactionScope CreateScope(DatabaseContext context, ISqlQuery query, IDbConnection connection,
        IDbTransaction transaction) => new(context, query, connection, _queryFactory, _executorFactory, transaction);

    /// <summary>
    /// 在访问连接前验证数据源与 Provider 的本地事务能力。
    /// </summary>
    /// <param name="context">目标数据库上下文。</param>
    /// <param name="query">用于解析 Provider Profile 的查询对象。</param>
    private static void EnsureTransactionsSupported(DatabaseContext context, ISqlQuery query)
    {
        if (context?.DataSource?.IsReadOnly == true)
            throw new NotSupportedException(
                $"数据源 {context.DataSource.Key ?? context.DbKey ?? "<default>"} 是只读数据源，不支持写入或事务操作。");
        var profile = SqlQueryRuntimeBridge.GetProviderProfile(query);
        if (profile.Transaction.SupportsTransactions == false)
            throw new NotSupportedException("当前 SQL Provider 不支持本地事务。请使用不依赖事务的查询操作。");
        if (context?.DataSource?.SupportsTransactions == false)
            throw new NotSupportedException($"数据源 {context.DbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
    }

    /// <summary>
    /// 创建并解析事务专用 Query，固定其数据库上下文快照。
    /// </summary>
    /// <param name="dbKey">目标数据源键；为空时由 Query Factory 使用当前上下文。</param>
    /// <param name="context">解析到的固定数据库上下文。</param>
    /// <returns>拥有本次事务连接生命周期的查询对象。</returns>
    private ISqlQuery CreateTransactionQuery(string dbKey, out DatabaseContext context)
    {
        if (_queryFactory is SqlQueryFactory factory)
            return factory.CreateForTransaction<ISqlQuery>(dbKey, out context);
        var query = string.IsNullOrWhiteSpace(dbKey) ? _queryFactory.Create<ISqlQuery>() : _queryFactory.Create<ISqlQuery>(dbKey);
        context = query is SqlQueryBase sqlQuery ? sqlQuery.GetDatabaseContext() : null;
        if (context == null)
            throw new InvalidOperationException("事务查询对象必须提供固定数据库上下文");
        return query;
    }

    /// <summary>
    /// SQL 事务作用域。
    /// </summary>
    private sealed class SqlTransactionScope : ISqlTransactionScope
    {
        private readonly ISqlQuery _ownerQuery;
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly ISqlQueryFactory _queryFactory;
        private readonly ISqlExecutorFactory _executorFactory;
        private readonly List<IDisposable> _children = new();
        private readonly SqlTransactionScopeLease _lease;
        private readonly DatabaseContext _context;
        private SqlTransactionScopeState _state;
        private bool _resourcesReleased;
        private bool _isExplicitlyDisposed;

        /// <summary>
        /// 初始化事务作用域并冻结资源和数据库上下文。
        /// </summary>
        /// <param name="context">事务固定数据库上下文。</param>
        /// <param name="ownerQuery">拥有连接生命周期的根查询对象。</param>
        /// <param name="connection">已打开的数据库连接。</param>
        /// <param name="queryFactory">用于创建事务子查询的工厂。</param>
        /// <param name="executorFactory">用于创建事务子执行器的工厂。</param>
        /// <param name="transaction">当前数据库事务。</param>
        public SqlTransactionScope(DatabaseContext context, ISqlQuery ownerQuery, IDbConnection connection,
            ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory, IDbTransaction transaction)
        {
            _context = DatabaseContextSnapshot.Create(context) ?? throw new ArgumentNullException(nameof(context));
            _ownerQuery = ownerQuery ?? throw new ArgumentNullException(nameof(ownerQuery));
            _connection = connection ?? throw new InvalidOperationException("SQL 连接不能为空");
            _transaction = transaction ?? throw new InvalidOperationException("SQL 事务不能为空");
            _queryFactory = queryFactory;
            _executorFactory = executorFactory;
            TransactionId = Guid.NewGuid().ToString("N");
            _lease = new SqlTransactionScopeLease(TransactionId);
            _state = SqlTransactionScopeState.Active;
        }

        /// <inheritdoc />
        public string DbKey => _context.DbKey;

        /// <inheritdoc />
        public DatabaseType DatabaseType => _context.DataSource?.DatabaseType ?? default;

        /// <inheritdoc />
        public DatabaseContext DatabaseContext => DatabaseContextSnapshot.Create(_context);

        /// <inheritdoc />
        public IsolationLevel IsolationLevel => _transaction.IsolationLevel;

        /// <inheritdoc />
        public IDbConnection Connection => _connection;

        /// <inheritdoc />
        public IDbTransaction Transaction => _transaction;

        /// <inheritdoc />
        public bool IsCompleted => _state != SqlTransactionScopeState.Active;

        /// <inheritdoc />
        public string TransactionId { get; }

        /// <inheritdoc />
        public ISqlQuery CreateQuery() => CreateQuery<ISqlQuery>();

        /// <inheritdoc />
        public TQuery CreateQuery<TQuery>() where TQuery : class, ISqlQuery
        {
            ThrowIfInactive();
            return CreateAndBindChild(() => _queryFactory is SqlQueryFactory factory
                ? factory.CreateForTransaction<TQuery>(_context)
                : _queryFactory.Create<TQuery>(DbKey));
        }

        /// <inheritdoc />
        public ISqlExecutor CreateExecutor() => CreateExecutor<ISqlExecutor>();

        /// <inheritdoc />
        public TExecutor CreateExecutor<TExecutor>() where TExecutor : class, ISqlExecutor
        {
            ThrowIfInactive();
            return CreateAndBindChild(() => _executorFactory is SqlExecutorFactory factory
                ? factory.CreateForTransaction<TExecutor>(_context)
                : _executorFactory.Create<TExecutor>(DbKey));
        }

        /// <inheritdoc />
        public void Commit()
        {
            EnsureCanComplete(SqlTransactionScopeState.Committed);
            if (_state == SqlTransactionScopeState.Committed)
                return;
            EnsureActiveForCompletion();
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                _transaction.Commit();
                _state = SqlTransactionScopeState.Committed;
            }
            catch (Exception exception)
            {
                operationException = TryRollbackAfterCommitFailure(exception);
                _state = SqlTransactionScopeState.Faulted;
            }
            ThrowAfterCleanup(operationException);
        }

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanComplete(SqlTransactionScopeState.Committed);
            if (_state == SqlTransactionScopeState.Committed)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureActiveForCompletion();
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                await SqlTransactionAsyncAdapter.CommitAsync(_transaction, cancellationToken).ConfigureAwait(false);
                _state = SqlTransactionScopeState.Committed;
            }
            catch (Exception exception)
            {
                operationException = await TryRollbackAfterCommitFailureAsync(exception).ConfigureAwait(false);
                _state = SqlTransactionScopeState.Faulted;
            }
            await ThrowAfterCleanupAsync(operationException).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Rollback()
        {
            EnsureCanComplete(SqlTransactionScopeState.RolledBack);
            if (_state == SqlTransactionScopeState.RolledBack)
                return;
            EnsureActiveForCompletion();
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                _transaction.Rollback();
                _state = SqlTransactionScopeState.RolledBack;
            }
            catch (Exception exception)
            {
                operationException = exception;
                _state = SqlTransactionScopeState.Faulted;
            }
            ThrowAfterCleanup(operationException);
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanComplete(SqlTransactionScopeState.RolledBack);
            if (_state == SqlTransactionScopeState.RolledBack)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureActiveForCompletion();
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                await SqlTransactionAsyncAdapter.RollbackAsync(_transaction, cancellationToken).ConfigureAwait(false);
                _state = SqlTransactionScopeState.RolledBack;
            }
            catch (Exception exception)
            {
                operationException = exception;
                _state = SqlTransactionScopeState.Faulted;
            }
            await ThrowAfterCleanupAsync(operationException).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 若当前作用域创建的子 Query 或 Executor 持有活动执行租约，释放会抛出异常且不改变作用域状态、租约或事务资源。
        /// 调用方释放子对象后可重试。
        /// </remarks>
        public void Dispose()
        {
            if (_isExplicitlyDisposed)
                return;
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception rollbackException = null;
            if (_state == SqlTransactionScopeState.Active)
            {
                try
                {
                    _transaction.Rollback();
                    _state = SqlTransactionScopeState.RolledBack;
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                    _state = SqlTransactionScopeState.Faulted;
                }
            }
            try
            {
                ThrowAfterCleanup(rollbackException);
            }
            finally
            {
                _isExplicitlyDisposed = true;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 若当前作用域创建的子 Query 或 Executor 持有活动执行租约，释放会抛出异常且不改变作用域状态、租约或事务资源。
        /// 调用方释放子对象后可重试。
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (_isExplicitlyDisposed)
                return;
            EnsureChildrenHaveNoActiveExecution();
            _lease.Invalidate();
            Exception rollbackException = null;
            if (_state == SqlTransactionScopeState.Active)
            {
                try
                {
                    await SqlTransactionAsyncAdapter.RollbackAsync(_transaction, CancellationToken.None).ConfigureAwait(false);
                    _state = SqlTransactionScopeState.RolledBack;
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                    _state = SqlTransactionScopeState.Faulted;
                }
            }
            try
            {
                await ThrowAfterCleanupAsync(rollbackException).ConfigureAwait(false);
            }
            finally
            {
                _isExplicitlyDisposed = true;
            }
        }

        private void EnsureCanComplete(SqlTransactionScopeState expectedState)
        {
            if (_isExplicitlyDisposed)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active && _state != expectedState)
                throw new InvalidOperationException("SQL 事务作用域已完成，不能重复提交或回滚。");
        }

        private void EnsureActiveForCompletion()
        {
            if (_isExplicitlyDisposed)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active)
                throw new InvalidOperationException("SQL 事务作用域已完成，不能重复提交或回滚。");
        }

        /// <summary>
        /// 确保所有事务作用域子对象均未持有活动执行租约。
        /// </summary>
        private void EnsureChildrenHaveNoActiveExecution()
        {
            foreach (var child in _children)
            {
                if (child is ISqlQuery query)
                    SqlQueryRuntimeBridge.TryEnsureNoActiveExecution(query);
            }
        }

        private void ThrowIfInactive()
        {
            if (_isExplicitlyDisposed || _resourcesReleased)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active)
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
                BindTransactionContext(child);
                _children.Add(child);
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
            if (resource is not ISqlQuery query)
                throw new InvalidOperationException("SQL 事务作用域子对象不支持内部运行时资源绑定。");
            SqlQueryRuntimeBridge.BindTransactionScope(query, _context, _connection, _transaction, _lease);
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
                await SqlTransactionAsyncAdapter.RollbackAsync(_transaction, CancellationToken.None).ConfigureAwait(false);
                return commitException;
            }
            catch (Exception rollbackException)
            {
                return new AggregateException(commitException, rollbackException);
            }
        }

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

        private void DisposeChildren(List<Exception> exceptions)
        {
            foreach (var child in _children)
                TryCleanup(child.Dispose, exceptions);
            _children.Clear();
        }

        private async Task DisposeChildrenAsync(List<Exception> exceptions)
        {
            foreach (var child in _children)
                await TryCleanupAsync(() => SqlTransactionAsyncAdapter.DisposeAsync(child), exceptions).ConfigureAwait(false);
            _children.Clear();
        }

        private static void TryCleanup(Action action, List<Exception> exceptions)
        {
            try { action(); }
            catch (Exception exception) { AddException(exceptions, exception); }
        }

        private static async Task TryCleanupAsync(Func<Task> action, List<Exception> exceptions)
        {
            try { await action().ConfigureAwait(false); }
            catch (Exception exception) { AddException(exceptions, exception); }
        }

        private static void AddException(List<Exception> exceptions, Exception exception)
        {
            if (exception == null)
                return;
            if (exception is AggregateException aggregate)
                exceptions.AddRange(aggregate.Flatten().InnerExceptions);
            else
                exceptions.Add(exception);
        }

        private static void ThrowCollected(List<Exception> exceptions)
        {
            if (exceptions.Count == 1)
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);
        }
    }

    private enum SqlTransactionScopeState
    {
        Active,
        Committed,
        RolledBack,
        Faulted,
    }
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
    /// <returns>表示连接已打开的异步操作。</returns>
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
        if (connection is DbConnection dbConnection)
        {
            var invocation = await TryInvokeAsync(dbConnection, "BeginTransactionAsync", isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            if (invocation.Result is IDbTransaction transaction)
                return transaction;
        }
        return connection.BeginTransaction(isolationLevel);
    }

    /// <summary>
    /// 异步关闭连接；未公开异步关闭成员时回退为同步关闭。
    /// </summary>
    /// <param name="connection">待关闭的数据库连接。</param>
    /// <returns>表示连接已关闭的异步操作。</returns>
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
    /// <returns>表示提交完成的异步操作。</returns>
    public static Task CommitAsync(IDbTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return InvokeOrRunAsync(transaction, "CommitAsync", transaction.Commit, cancellationToken);
    }

    /// <summary>
    /// 异步回滚事务；Provider 未公开兼容异步成员时回退为同步回滚。
    /// </summary>
    /// <param name="transaction">待回滚的数据库事务。</param>
    /// <param name="cancellationToken">回滚操作使用的取消令牌。</param>
    /// <returns>表示回滚完成的异步操作。</returns>
    public static Task RollbackAsync(IDbTransaction transaction, CancellationToken cancellationToken) =>
        InvokeOrRunAsync(transaction, "RollbackAsync", transaction.Rollback, cancellationToken);

    /// <summary>
    /// 优先异步释放资源；不支持异步释放时回退为同步释放。
    /// </summary>
    /// <param name="resource">待释放的资源；可为 <see langword="null"/>。</param>
    /// <returns>表示资源释放完成的异步操作。</returns>
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

    private static async Task InvokeOrRunAsync(object target, string methodName, Action fallback,
        CancellationToken cancellationToken)
    {
        var invocation = await TryInvokeAsync(target, methodName, cancellationToken).ConfigureAwait(false);
        if (invocation.IsInvoked == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fallback();
        }
    }

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
            .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length);
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

    private static bool ParametersMatch(ParameterInfo[] parameters, object[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] != null && parameters[index].ParameterType.IsInstanceOfType(arguments[index]) == false)
                return false;
        }
        return true;
    }

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