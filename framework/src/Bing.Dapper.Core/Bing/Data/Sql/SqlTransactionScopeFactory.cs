using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域工厂
/// </summary>
public sealed class SqlTransactionScopeFactory : ISqlTransactionScopeFactory
{
    /// <summary>
    /// SQL 查询工厂
    /// </summary>
    private readonly ISqlQueryFactory _queryFactory;

    /// <summary>
    /// SQL 执行器工厂
    /// </summary>
    private readonly ISqlExecutorFactory _executorFactory;

    /// <summary>
    /// 初始化一个<see cref="SqlTransactionScopeFactory"/>类型的实例
    /// </summary>
    /// <param name="queryFactory">SQL 查询工厂</param>
    /// <param name="executorFactory">SQL 执行器工厂</param>
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
        var query = string.IsNullOrWhiteSpace(dbKey)
            ? _queryFactory.Create<ISqlQuery>()
            : _queryFactory.Create<ISqlQuery>(dbKey);
        var connection = query.GetConnection();
        if (connection.State == ConnectionState.Closed)
            connection.Open();
        var transaction = connection.BeginTransaction(isolationLevel);
        return new SqlTransactionScope(dbKey, query, connection, transaction, _queryFactory, _executorFactory);
    }

    /// <inheritdoc />
    public Task<ISqlTransactionScope> BeginAsync(string dbKey = null, CancellationToken cancellationToken = default)
        => BeginAsync(dbKey, IsolationLevel.ReadCommitted, cancellationToken);

    /// <inheritdoc />
    public Task<ISqlTransactionScope> BeginAsync(string dbKey, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Begin(dbKey, isolationLevel));
    }

    /// <summary>
    /// SQL 事务作用域
    /// </summary>
    private sealed class SqlTransactionScope : ISqlTransactionScope
    {
        private readonly ISqlQuery _ownerQuery;
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly ISqlQueryFactory _queryFactory;
        private readonly ISqlExecutorFactory _executorFactory;
        private bool _completed;
        private bool _disposed;

        public SqlTransactionScope(string dbKey, ISqlQuery ownerQuery, IDbConnection connection, IDbTransaction transaction,
            ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory)
        {
            DbKey = dbKey;
            _ownerQuery = ownerQuery;
            _connection = connection ?? throw new InvalidOperationException("SQL 连接不能为空");
            _transaction = transaction ?? throw new InvalidOperationException("SQL 事务不能为空");
            _queryFactory = queryFactory;
            _executorFactory = executorFactory;
        }

        /// <inheritdoc />
        public string DbKey { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType => _ownerQuery is SqlQueryBase query ? query.GetDatabaseType() : default;

        /// <inheritdoc />
        public IsolationLevel IsolationLevel => _transaction.IsolationLevel;

        /// <inheritdoc />
        public IDbConnection Connection => _connection;

        /// <inheritdoc />
        public IDbTransaction Transaction => _transaction;

        /// <inheritdoc />
        public bool IsCompleted => _completed;

        /// <inheritdoc />
        public string TransactionId { get; } = Guid.NewGuid().ToString("N");

        /// <inheritdoc />
        public ISqlQuery CreateQuery() => CreateQuery<ISqlQuery>();

        /// <inheritdoc />
        public TQuery CreateQuery<TQuery>() where TQuery : class, ISqlQuery
        {
            ThrowIfDisposed();
            var query = string.IsNullOrWhiteSpace(DbKey)
                ? _queryFactory.Create<TQuery>()
                : _queryFactory.Create<TQuery>(DbKey);
            query.SetTransaction(_transaction);
            return query;
        }

        /// <inheritdoc />
        public ISqlExecutor CreateExecutor() => CreateExecutor<ISqlExecutor>();

        /// <inheritdoc />
        public TExecutor CreateExecutor<TExecutor>() where TExecutor : class, ISqlExecutor
        {
            ThrowIfDisposed();
            var executor = string.IsNullOrWhiteSpace(DbKey)
                ? _executorFactory.Create<TExecutor>()
                : _executorFactory.Create<TExecutor>(DbKey);
            executor.SetTransaction(_transaction);
            return executor;
        }

        /// <inheritdoc />
        public void Commit()
        {
            ThrowIfDisposed();
            _transaction.Commit();
            _completed = true;
            Dispose();
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Rollback()
        {
            if (_disposed)
                return;
            _transaction.Rollback();
            _completed = true;
            Dispose();
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Rollback();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;
            try
            {
                if (_completed == false)
                    _transaction.Rollback();
            }
            finally
            {
                _transaction.Dispose();
                _ownerQuery.Dispose();
                _disposed = true;
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask();
        }

        /// <summary>
        /// 已释放时抛出异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqlTransactionScope));
        }
    }
}
