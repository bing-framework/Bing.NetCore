using System.Data;
using Bing.Extensions;

namespace Bing.Data.Sql.Queries;

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
        _connection = transaction.Connection;
    }

    #endregion

    #region GetTransaction(获取数据库事务)

    /// <summary>
    /// 获取数据库事务
    /// </summary>
    public IDbTransaction GetTransaction() => _transaction;

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
            return _transaction;
        }
        catch
        {
            if (_connection?.State == ConnectionState.Open)
                _connection?.Close();
            _transaction?.Dispose();
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
        try
        {
            _transaction?.Commit();
        }
        catch
        {
            _transaction?.Rollback();
            throw;
        }
        finally
        {
            if (_connection?.State == ConnectionState.Open)
                _connection.Close();
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    #endregion

    #region RollbackTransaction(回滚事务)

    /// <summary>
    /// 回滚事务
    /// </summary>
    public void RollbackTransaction()
    {
        try
        {
            if (_connection.State != ConnectionState.Closed)
                _transaction?.Rollback();
        }
        finally
        {
            if (_connection?.State == ConnectionState.Open)
                _connection?.Close();
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    #endregion
}
