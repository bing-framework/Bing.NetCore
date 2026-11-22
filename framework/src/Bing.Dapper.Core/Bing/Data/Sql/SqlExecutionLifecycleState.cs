namespace Bing.Data.Sql;

/// <summary>
/// 共享执行资源的租约、释放标志和同步状态。
/// </summary>
/// <remarks>
/// 使用值类型嵌入共享核心，避免为查询和写入各自增加新的生命周期对象分配。
/// </remarks>
internal struct SqlExecutionLifecycleState
{
    /// <summary>当前实例的执行租约状态。</summary>
    internal int ExecutionLease;

    /// <summary>保护执行租约和释放状态原子转换的同步锁。</summary>
    internal object ExecutionStateLock;

    /// <summary>当前 Root Query 是否已经释放。</summary>
    internal int IsDisposed;
}
