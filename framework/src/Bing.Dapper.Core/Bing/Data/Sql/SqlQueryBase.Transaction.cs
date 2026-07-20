using System.Runtime.ExceptionServices;
using Bing.Extensions;

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
    private void SetTransactionContext(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
        SqlTransactionScopeLease lease)
    {
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
        _isTransactionScopeChildDisposed = false;
        _transaction = transaction;
        _transactionId = lease.TransactionId;
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
        _transactionOwnership = SqlResourceOwnership.External;
    }

    /// <summary>
    /// 获取内部执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    protected IDbTransaction GetExecutionTransaction() =>
        ((ISqlExecutionResourceAccessor)this).GetCurrentTransaction();

    /// <summary>
    /// 获取当前执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    IDbTransaction ISqlExecutionResourceAccessor.GetCurrentTransaction()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
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
    /// 开始 Query 内部拥有的事务。
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>内部拥有的数据库事务。</returns>
    private IDbTransaction BeginOwnedTransaction(IsolationLevel? isolationLevel = null) =>
        BeginTransactionImpl(isolationLevel);

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别</param>
    private IDbTransaction BeginTransactionImpl(IsolationLevel? isolationLevel)
    {
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
        catch
        {
            CloseOwnedConnection();
            ReleaseTransaction();
            throw;
        }
    }

    /// <summary>
    /// 确保当前数据源支持本地事务。
    /// </summary>
    private void EnsureTransactionsSupported()
    {
        var dataSource = Options.GetDatabaseContext()?.DataSource;
        if (dataSource?.SupportsTransactions != false)
            return;
        var dbKey = dataSource.Key ?? Options.GetDatabaseContext()?.DbKey ?? "<default>";
        throw new NotSupportedException($"数据源 {dbKey} 不支持本地事务。请使用不依赖事务的查询操作。");
    }

    /// <summary>
    /// 提交 Query 内部拥有的事务。
    /// </summary>
    private void CommitOwnedTransaction()
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("提交");
        try
        {
            _transaction.Commit();
        }
        catch (Exception commitException)
        {
            try
            {
                _transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(commitException, rollbackException);
            }
            ExceptionDispatchInfo.Capture(commitException).Throw();
        }
        finally
        {
            CloseOwnedConnection();
            ReleaseTransaction();
        }
    }

    /// <summary>
    /// 回滚内部拥有的事务
    /// </summary>
    private void RollbackOwnedTransaction()
    {
        if (_transaction == null)
            return;
        EnsureOwnedTransaction("回滚");
        try
        {
            if (_connection?.State != ConnectionState.Closed)
                _transaction.Rollback();
        }
        finally
        {
            CloseOwnedConnection();
            ReleaseTransaction();
        }
    }

    /// <summary>
    /// 获取当前事务诊断标识。
    /// </summary>
    /// <returns>当前事务标识，不存在时返回 null。</returns>
    string ISqlExecutionResourceAccessor.GetCurrentTransactionId()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
        return _transactionId;
    }

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
    /// 绑定框架自有连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="source">连接来源。</param>
    void ISqlExecutionResourceBinder.BindOwnedConnection(IDbConnection connection, SqlConnectionSource source) =>
        BindConnection(connection, SqlResourceOwnership.Owned, source);

    /// <summary>
    /// 绑定外部连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="source">连接来源。</param>
    void ISqlExecutionResourceBinder.BindExternalConnection(IDbConnection connection, SqlConnectionSource source) =>
        BindConnection(connection, SqlResourceOwnership.External, source);

    /// <summary>
    /// 绑定外部事务。
    /// </summary>
    /// <param name="transaction">数据库事务。</param>
    /// <param name="transactionId">诊断事务标识。</param>
    void ISqlExecutionResourceBinder.BindExternalTransaction(IDbTransaction transaction, string transactionId) =>
        BindExternalTransaction(transaction, transactionId);

    /// <summary>
    /// 绑定外部事务延迟解析器。
    /// </summary>
    /// <param name="resolver">外部事务解析器。</param>
    void ISqlExecutionResourceBinder.BindExternalTransactionResolver(Func<IDbTransaction> resolver) =>
        _externalTransactionResolver = resolver;

    /// <summary>
    /// 绑定事务作用域上下文。
    /// </summary>
    /// <param name="context">固定数据库上下文。</param>
    /// <param name="connection">事务连接。</param>
    /// <param name="transaction">事务对象。</param>
    /// <param name="lease">事务作用域执行租约。</param>
    void ISqlExecutionResourceBinder.BindTransactionScope(DatabaseContext context, IDbConnection connection,
        IDbTransaction transaction, SqlTransactionScopeLease lease) => SetTransactionContext(context, connection, transaction, lease);

}
