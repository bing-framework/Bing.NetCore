using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 查询执行所需的资源访问视图。
/// </summary>
/// <remarks>
/// 该类型只保存现有资源管理器的引用，不拥有连接或事务，也不产生额外堆分配。
/// 查询路径通过此视图访问连接、租约、查询事务和取消生命周期；事务清理通过专用查询清理视图访问。
/// </remarks>
internal readonly struct SqlQueryResourceAccess
{
    private readonly ISqlQueryResourceOwner _resources;

    internal SqlQueryResourceAccess(ISqlQueryResourceOwner resources)
    {
        _resources = resources;
    }

    internal SqlConnectionSource ConnectionSource => _resources.ConnectionSource;
    internal SqlResourceOwnership ConnectionOwnership => _resources.ConnectionOwnership;
    internal SqlResourceOwnership TransactionOwnership => _resources.TransactionOwnership;
    internal bool HasTransaction => _resources.HasTransaction;
    internal bool IsPrimaryReadTransaction => _resources.IsPrimaryReadTransaction;
    internal string TransactionId => _resources.TransactionId;
    internal string TransactionExecutionMode => _resources.TransactionExecutionMode;
    internal void EnsureExecutionAvailable() => _resources.EnsureExecutionAvailable();

    internal IDbConnection GetExecutionConnection() => _resources.GetExecutionConnection();

    internal IDbTransaction GetTransactionForDiagnostics() => _resources.GetTransactionForDiagnostics();

    internal IDisposable AcquireExecutionLease() => _resources.AcquireExecutionLease();

    internal IDbTransaction GetQueryTransaction() => _resources.GetQueryTransaction();

    internal Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken cancellationToken = default) =>
        _resources.GetQueryTransactionAsync(cancellationToken);

    internal void MarkPreCancelledUnitOfWorkExecution() => _resources.MarkPreCancelledUnitOfWorkExecution();

    internal ISqlOperationCancellationLease CreateOperationCancellationLease(CancellationToken cancellationToken) =>
        _resources.CreateOperationCancellationLease(cancellationToken);

}
