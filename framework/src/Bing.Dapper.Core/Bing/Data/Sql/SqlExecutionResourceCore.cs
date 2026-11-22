namespace Bing.Data.Sql;

/// <summary>
/// 嵌入单个查询或写入所有者的连接、事务及执行清理状态。
/// </summary>
/// <remarks>
/// 该值类型不单独分配对象，由各具象 owner 直接持有。通用生命周期算法通过 ref 访问原状态，
/// 不复制状态；Scope/Uow 共享的是借用连接和事务，不是子对象的租约及释放标志。
/// </remarks>
internal struct SqlExecutionResourceCore
{
    /// <summary>连接生命周期状态。</summary>
    internal SqlConnectionResourceState ConnectionState;

    /// <summary>事务生命周期状态。</summary>
    internal SqlTransactionResourceState TransactionState;

    /// <summary>执行租约和释放生命周期状态。</summary>
    internal SqlExecutionLifecycleState LifecycleState;

    /// <summary>
    /// 初始化本地资源状态并绑定该所有者的执行同步锁。
    /// </summary>
    /// <param name="executionStateLock">该资源所有者独立的同步锁。</param>
    internal SqlExecutionResourceCore(object executionStateLock)
    {
        ConnectionState = default;
        TransactionState = default;
        LifecycleState = new SqlExecutionLifecycleState
        {
            ExecutionStateLock = executionStateLock
        };
    }
}
