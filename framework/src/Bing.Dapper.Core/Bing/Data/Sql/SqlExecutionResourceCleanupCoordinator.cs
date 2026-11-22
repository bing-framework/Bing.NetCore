using System.Runtime.ExceptionServices;

namespace Bing.Data.Sql;

/// <summary>
/// 负责 Root 执行资源的同步和异步清理编排。
/// </summary>
/// <remarks>
/// 该类型使用值类型嵌入资源所有者，只移动清理顺序和异常聚合，不复制连接或事务状态，
/// 因此查询与写入仍然可以共享同一个连接和事务核心。
/// </remarks>
internal readonly struct SqlExecutionResourceCleanupCoordinator
{
    private readonly SqlExecutionResources _owner;

    /// <summary>
    /// 初始化清理协调器。
    /// </summary>
    /// <param name="owner">当前查询或写入资源所有者。</param>
    internal SqlExecutionResourceCleanupCoordinator(SqlExecutionResources owner) =>
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>
    /// 按事务后连接的顺序同步释放 Root 资源。
    /// </summary>
    internal void Dispose()
    {
        if (_owner.TryBeginDispose() == false)
            return;

        Exception transactionException = null;
        try
        {
            _owner.ReleaseTransaction();
        }
        catch (Exception exception)
        {
            transactionException = exception;
        }

        try
        {
            _owner.ReleaseConnection();
        }
        catch (Exception connectionException)
        {
            if (transactionException != null)
                throw new AggregateException(transactionException, connectionException);
            throw;
        }
        finally
        {
            _owner.MarkTransactionScopeChildDisposed();
        }

        if (transactionException != null)
            ExceptionDispatchInfo.Capture(transactionException).Throw();
    }

    /// <summary>
    /// 按事务后连接的顺序异步释放 Root 资源。
    /// </summary>
    /// <returns>表示清理操作的异步值任务。</returns>
    internal async ValueTask DisposeAsync()
    {
        if (_owner.TryBeginDispose() == false)
            return;

        Exception transactionException = null;
        try
        {
            await _owner.ReleaseTransactionAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            transactionException = exception;
        }

        try
        {
            await _owner.ReleaseConnectionAsync().ConfigureAwait(false);
        }
        catch (Exception connectionException)
        {
            if (transactionException != null)
                throw new AggregateException(transactionException, connectionException);
            throw;
        }
        finally
        {
            _owner.MarkTransactionScopeChildDisposed();
        }

        if (transactionException != null)
            ExceptionDispatchInfo.Capture(transactionException).Throw();
    }
}
