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
        var query = CreateTransactionQuery(dbKey, out var context);
        if (context.DataSource?.SupportsTransactions == false)
        {
            query.Dispose();
            throw new NotSupportedException($"数据源 {context.DbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
        }
        var connection = query.GetConnection();
        if (connection.State == ConnectionState.Closed)
            connection.Open();
        var transaction = connection.BeginTransaction(isolationLevel);
        return new SqlTransactionScope(context, query, connection, transaction, _queryFactory, _executorFactory);
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
    /// 创建固定在事务主库上下文中的查询对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <param name="context">事务数据库上下文。</param>
    /// <returns>查询对象。</returns>
    private ISqlQuery CreateTransactionQuery(string dbKey, out DatabaseContext context)
    {
        if (_queryFactory is SqlQueryFactory factory)
            return factory.CreateForTransaction<ISqlQuery>(dbKey, out context);
        var query = string.IsNullOrWhiteSpace(dbKey)
            ? _queryFactory.Create<ISqlQuery>()
            : _queryFactory.Create<ISqlQuery>(dbKey);
        context = query is SqlQueryBase sqlQuery
            ? sqlQuery.GetDatabaseContext()
            : null;
        if (context == null)
            throw new InvalidOperationException("事务查询对象必须提供固定数据库上下文");
        return query;
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

        private readonly DatabaseContext _context;

        public SqlTransactionScope(DatabaseContext context, ISqlQuery ownerQuery, IDbConnection connection, IDbTransaction transaction,
            ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory)
        {
            _context = CloneContext(context) ?? throw new ArgumentNullException(nameof(context));
            _ownerQuery = ownerQuery;
            _connection = connection ?? throw new InvalidOperationException("SQL 连接不能为空");
            _transaction = transaction ?? throw new InvalidOperationException("SQL 事务不能为空");
            _queryFactory = queryFactory;
            _executorFactory = executorFactory;
        }

        /// <inheritdoc />
        public string DbKey => _context.DbKey;

        /// <inheritdoc />
        public DatabaseType DatabaseType => _context.DataSource?.DatabaseType ?? default;

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
            var query = _queryFactory is SqlQueryFactory factory
                ? factory.CreateForTransaction<TQuery>(_context)
                : _queryFactory.Create<TQuery>(DbKey);
            BindTransactionContext(query);
            return query;
        }

        /// <inheritdoc />
        public ISqlExecutor CreateExecutor() => CreateExecutor<ISqlExecutor>();

        /// <inheritdoc />
        public TExecutor CreateExecutor<TExecutor>() where TExecutor : class, ISqlExecutor
        {
            ThrowIfDisposed();
            var executor = _executorFactory is SqlExecutorFactory factory
                ? factory.CreateForTransaction<TExecutor>(_context)
                : _executorFactory.Create<TExecutor>(DbKey);
            BindTransactionContext(executor);
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

        /// <summary>
        /// 为事务子对象绑定固定上下文、连接和事务。
        /// </summary>
        /// <param name="query">事务子对象。</param>
        private void BindTransactionContext(ISqlQuery query)
        {
            if (query is SqlQueryBase sqlQuery)
            {
                sqlQuery.SetTransactionContext(_context, _connection, _transaction);
                return;
            }
            query.SetTransaction(_transaction);
        }

        /// <summary>
        /// 创建事务数据库上下文快照。
        /// </summary>
        /// <param name="context">数据库上下文。</param>
        /// <returns>数据库上下文快照。</returns>
        private static DatabaseContext CloneContext(DatabaseContext context)
        {
            if (context == null)
                return null;
            return new DatabaseContext
            {
                DbKey = context.DbKey,
                TenantId = context.TenantId,
                MappingProfile = context.MappingProfile,
                ReadPreference = context.ReadPreference,
                DataSource = CloneDataSource(context.DataSource)
            };
        }

        /// <summary>
        /// 创建事务数据源描述快照。
        /// </summary>
        /// <param name="dataSource">数据源描述。</param>
        /// <returns>数据源描述快照。</returns>
        private static SqlDataSourceDescriptor CloneDataSource(SqlDataSourceDescriptor dataSource)
        {
            if (dataSource == null)
                return null;
            return new SqlDataSourceDescriptor
            {
                Key = dataSource.Key,
                DatabaseType = dataSource.DatabaseType,
                ConnectionStringName = dataSource.ConnectionStringName,
                ConnectionString = dataSource.ConnectionString,
                IsReadOnly = dataSource.IsReadOnly,
                MappingProfile = dataSource.MappingProfile,
                PrimaryReadStrategy = dataSource.PrimaryReadStrategy,
                PrimaryDataSourceKey = dataSource.PrimaryDataSourceKey,
                SupportsTransactions = dataSource.SupportsTransactions
            };
        }
    }
}
