using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 写入执行所需的资源访问视图。
/// </summary>
/// <remarks>
/// 该类型只保存现有资源管理器的引用，不拥有连接或事务，也不产生额外堆分配。
/// 写入路径通过此视图访问数据源校验、执行连接、事务、租约和取消生命周期；事务清理通过专用写入清理视图访问。
/// </remarks>
internal readonly struct SqlWriteResourceAccess
{
    private readonly ISqlWriteResourceOwner _resources;

    internal SqlWriteResourceAccess(ISqlWriteResourceOwner resources)
    {
        _resources = resources;
    }

    internal void EnsureWritableDataSource(SqlDataSourceDescriptor source = null) => _resources.EnsureWritableDataSource(source);

    internal IDbConnection GetExecutionConnection() => _resources.GetExecutionConnection();

    internal IDbTransaction GetExecutionTransaction() => _resources.GetExecutionTransaction();

    internal IDbTransaction GetWriteTransaction() => _resources.GetWriteTransaction();

    internal Task<IDbTransaction> GetWriteTransactionAsync(CancellationToken cancellationToken = default) =>
        _resources.GetWriteTransactionAsync(cancellationToken);

    internal IDisposable AcquireExecutionLease() => _resources.AcquireExecutionLease();

    internal bool HasUnitOfWorkResource => _resources.HasUnitOfWorkResource;

    internal bool IsUnitOfWorkRootTransactional => _resources.IsUnitOfWorkRootTransactional;

    internal ISqlOperationCancellationLease CreateOperationCancellationLease(CancellationToken cancellationToken) =>
        _resources.CreateOperationCancellationLease(cancellationToken);

    internal void MarkPreCancelledUnitOfWorkExecution() => _resources.MarkPreCancelledUnitOfWorkExecution();

    internal void MarkUnitOfWorkExecutionFailure() => _resources.MarkUnitOfWorkExecutionFailure();
}
