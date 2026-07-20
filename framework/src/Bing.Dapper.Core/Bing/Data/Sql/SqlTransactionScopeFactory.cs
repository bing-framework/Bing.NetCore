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
            EnsureTransactionsSupported(context);
            var connection = query.GetConnection();
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
            EnsureTransactionsSupported(context);
            var connection = query.GetConnection();
            if (connection.State == ConnectionState.Closed)
                await SqlTransactionAsyncAdapter.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            var transaction = await SqlTransactionAsyncAdapter.BeginAsync(connection, isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            return CreateScope(context, query, connection, transaction);
        }
        catch (Exception exception)
        {
            ThrowAfterBeginFailure(exception, query);
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

    private SqlTransactionScope CreateScope(DatabaseContext context, ISqlQuery query, IDbConnection connection,
        IDbTransaction transaction) => new(context, query, connection, _queryFactory, _executorFactory, transaction);

    private static void EnsureTransactionsSupported(DatabaseContext context)
    {
        if (context?.DataSource?.SupportsTransactions == false)
            throw new NotSupportedException($"数据源 {context.DbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
    }

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
        private readonly List<ISqlQuery> _children = new();
        private readonly SqlTransactionScopeLease _lease;
        private readonly DatabaseContext _context;
        private SqlTransactionScopeState _state;
        private bool _resourcesReleased;
        private bool _isExplicitlyDisposed;

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

        public string DbKey => _context.DbKey;
        public DatabaseType DatabaseType => _context.DataSource?.DatabaseType ?? default;
        public IsolationLevel IsolationLevel => _transaction.IsolationLevel;
        public IDbConnection Connection => _connection;
        public IDbTransaction Transaction => _transaction;
        public bool IsCompleted => _state != SqlTransactionScopeState.Active;
        public string TransactionId { get; }

        public ISqlQuery CreateQuery() => CreateQuery<ISqlQuery>();

        public TQuery CreateQuery<TQuery>() where TQuery : class, ISqlQuery
        {
            ThrowIfInactive();
            var query = _queryFactory is SqlQueryFactory factory
                ? factory.CreateForTransaction<TQuery>(_context)
                : _queryFactory.Create<TQuery>(DbKey);
            BindTransactionContext(query);
            _children.Add(query);
            return query;
        }

        public ISqlExecutor CreateExecutor() => CreateExecutor<ISqlExecutor>();

        public TExecutor CreateExecutor<TExecutor>() where TExecutor : class, ISqlExecutor
        {
            ThrowIfInactive();
            var executor = _executorFactory is SqlExecutorFactory factory
                ? factory.CreateForTransaction<TExecutor>(_context)
                : _executorFactory.Create<TExecutor>(DbKey);
            BindTransactionContext(executor);
            _children.Add(executor);
            return executor;
        }

        public void Commit()
        {
            EnsureCanComplete(SqlTransactionScopeState.Committed);
            if (_state == SqlTransactionScopeState.Committed)
                return;
            EnsureActiveForCompletion();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                _transaction.Commit();
                _state = SqlTransactionScopeState.Committed;
            }
            catch (Exception exception)
            {
                operationException = exception;
                _state = SqlTransactionScopeState.Faulted;
            }
            ThrowAfterCleanup(operationException);
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanComplete(SqlTransactionScopeState.Committed);
            if (_state == SqlTransactionScopeState.Committed)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureActiveForCompletion();
            _lease.Invalidate();
            Exception operationException = null;
            try
            {
                await SqlTransactionAsyncAdapter.CommitAsync(_transaction, cancellationToken).ConfigureAwait(false);
                _state = SqlTransactionScopeState.Committed;
            }
            catch (Exception exception)
            {
                operationException = exception;
                _state = SqlTransactionScopeState.Faulted;
            }
            await ThrowAfterCleanupAsync(operationException).ConfigureAwait(false);
        }

        public void Rollback()
        {
            EnsureCanComplete(SqlTransactionScopeState.RolledBack);
            if (_state == SqlTransactionScopeState.RolledBack)
                return;
            EnsureActiveForCompletion();
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

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            EnsureCanComplete(SqlTransactionScopeState.RolledBack);
            if (_state == SqlTransactionScopeState.RolledBack)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureActiveForCompletion();
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

        public void Dispose()
        {
            if (_isExplicitlyDisposed)
                return;
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

        public async ValueTask DisposeAsync()
        {
            if (_isExplicitlyDisposed)
                return;
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

        private void ThrowIfInactive()
        {
            if (_isExplicitlyDisposed || _resourcesReleased)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
            if (_state != SqlTransactionScopeState.Active)
                throw new InvalidOperationException("SQL 事务作用域已结束，不能继续创建 Query 或 Executor。");
        }

        private void BindTransactionContext(ISqlQuery query)
        {
            if (query is SqlQueryBase sqlQuery)
            {
                sqlQuery.SetTransactionContext(_context, _connection, _transaction, _lease);
                return;
            }
            throw new InvalidOperationException("事务作用域创建的 Query 或 Executor 必须继承 SqlQueryBase，才能保证其生命周期受事务作用域管理。");
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
    public static Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
            return dbConnection.OpenAsync(cancellationToken);
        connection.Open();
        return Task.CompletedTask;
    }

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

    public static Task CommitAsync(IDbTransaction transaction, CancellationToken cancellationToken) =>
        InvokeOrRunAsync(transaction, "CommitAsync", transaction.Commit, cancellationToken);

    public static Task RollbackAsync(IDbTransaction transaction, CancellationToken cancellationToken) =>
        InvokeOrRunAsync(transaction, "RollbackAsync", transaction.Rollback, cancellationToken);

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
            fallback();
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