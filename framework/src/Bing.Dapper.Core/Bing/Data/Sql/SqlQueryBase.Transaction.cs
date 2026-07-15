using Bing.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象基类 - 事务
/// </summary>
public abstract partial class SqlQueryBase
{
    #region SetTransaction(设置数据库事务)

    /// <summary>
    /// 设置数据库事务
    /// </summary>
    /// <param name="transaction">数据库事务</param>
    public void SetTransaction(IDbTransaction transaction)
    {
        if (transaction == null)
            return;
        _transaction = transaction;
        _transactionId = Guid.NewGuid().ToString("N");
        _transactionOwnership = SqlResourceOwnership.External;
        _connection = transaction.Connection;
        _connectionOwnership = SqlResourceOwnership.External;
    }

    #endregion

    #region GetTransaction(获取数据库事务)

    /// <summary>
    /// 获取数据库事务
    /// </summary>
    public IDbTransaction GetTransaction() => _transaction ?? _externalTransactionResolver?.Invoke();

    /// <summary>
    /// 获取查询事务。
    /// </summary>
    protected IDbTransaction GetQueryTransaction()
    {
        var transaction = GetTransaction();
        if (transaction != null)
            return transaction;
        var context = Options.GetDatabaseContext();
        if (context?.ReadPreference != SqlReadPreference.Primary)
            return null;
        if (context.DataSource?.PrimaryReadStrategy != PrimaryReadStrategy.Transaction)
            return null;
        _primaryReadTransactionStarted = true;
        return BeginTransaction();
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
