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
    internal void SetTransactionContext(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
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
        if (transaction.Connection != null && ReferenceEquals(transaction.Connection, connection) == false)
            throw new InvalidOperationException("事务连接与固定事务执行上下文不一致");
        Options.DatabaseType = context.DataSource?.DatabaseType ?? Options.DatabaseType;
        Options.SetDatabaseContext(context);
        SetConnection(connection);
        _transactionScopeLease = lease;
        SetTransaction(transaction, lease.TransactionId);
    }

    #region SetTransaction(设置数据库事务)

    /// <summary>
    /// 设置数据库事务
    /// </summary>
    /// <param name="transaction">数据库事务</param>
    public void SetTransaction(IDbTransaction transaction)
    {
        SetTransaction(transaction, null);
    }

    /// <summary>
    /// 设置数据库事务并指定诊断事务标识。
    /// </summary>
    /// <param name="transaction">数据库事务。</param>
    /// <param name="transactionId">诊断事务标识。</param>
    private void SetTransaction(IDbTransaction transaction, string transactionId)
    {
        if (transaction == null)
            return;
        _transaction = transaction;
        _transactionId = transactionId ?? Guid.NewGuid().ToString("N");
        _transactionOwnership = SqlResourceOwnership.External;
        if (transaction.Connection != null)
        {
            _connection = transaction.Connection;
            _connectionOwnership = SqlResourceOwnership.External;
        }
    }

    #endregion

    #region GetTransaction(获取数据库事务)

    /// <summary>
    /// 获取数据库事务
    /// </summary>
    public IDbTransaction GetTransaction()
    {
        _transactionScopeLease?.EnsureActive();
        return _transaction ?? _externalTransactionResolver?.Invoke();
    }

    /// <summary>
    /// 获取查询事务。
    /// </summary>
    protected IDbTransaction GetQueryTransaction()
    {
        _transactionScopeLease?.EnsureActive();
        var transaction = GetTransaction();
        if (transaction != null)
            return transaction;
        var context = Options.GetDatabaseContext();
        if (context?.ReadPreference != SqlReadPreference.Primary)
            return null;
        if (context.DataSource?.PrimaryReadStrategy != PrimaryReadStrategy.Transaction)
            return null;
        var primaryReadTransaction = BeginTransaction();
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
            CommitTransaction();
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

    #endregion

    #region BeginTransaction(开始事务)

    /// <summary>
    /// 开始事务
    /// </summary>
    public IDbTransaction BeginTransaction() => BeginTransactionImpl(null);

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别</param>
    public IDbTransaction BeginTransaction(IsolationLevel isolationLevel) => BeginTransactionImpl(isolationLevel);

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">事务隔离级别</param>
    private IDbTransaction BeginTransactionImpl(IsolationLevel? isolationLevel)
    {
        try
        {
            if (_transaction != null)
                return _transaction;
            EnsureTransactionsSupported();
            var connection = GetConnection();
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

    #endregion

    #region CommitTransaction(提交事务)

    /// <summary>
    /// 提交事务
    /// </summary>
    public void CommitTransaction()
    {
        if (_transaction == null)
            return;
        if (_transactionOwnership == SqlResourceOwnership.External)
            return;
        try
        {
            _transaction.Commit();
        }
        catch
        {
            _transaction.Rollback();
            throw;
        }
        finally
        {
            CloseOwnedConnection();
            ReleaseTransaction();
        }
    }

    #endregion

    #region RollbackTransaction(回滚事务)

    /// <summary>
    /// 回滚事务
    /// </summary>
    public void RollbackTransaction()
    {
        if (_transaction == null)
            return;
        if (_transactionOwnership == SqlResourceOwnership.External)
            return;
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
    /// 回滚内部拥有的事务
    /// </summary>
    protected void RollbackOwnedTransaction()
    {
        if (_transactionOwnership != SqlResourceOwnership.Owned)
            return;
        RollbackTransaction();
    }

    #endregion
}
