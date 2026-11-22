using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 执行取消租约的最小契约，保留执行器需要的令牌和释放语义。
/// </summary>
internal interface ISqlOperationCancellationLease : IDisposable
{
    CancellationToken Token { get; }
}

/// <summary>
/// 查询执行路径的短事务清理最小契约。
/// </summary>
/// <remarks>
/// 清理视图只负责结束当前操作拥有的查询事务或标记工作单元回滚，
/// 不暴露连接、数据源门禁和工作单元执行状态，避免执行路径重新依赖宽资源类型。
/// </remarks>
internal interface ISqlQueryResourceCleanupOwner
{
    void CompleteQueryTransaction();
    Task CompleteQueryTransactionAsync(CancellationToken cancellationToken = default);
    void RollbackQueryTransaction();
    Task RollbackQueryTransactionAsync();
}

/// <summary>
/// 查询路径所需的资源所有者最小契约。
/// </summary>
/// <remarks>
/// 该接口只描述连接、查询事务、租约和查询取消；事务清理由专用清理契约承载，写入数据源校验及工作单元失败标记不属于查询视图。
/// </remarks>
internal interface ISqlQueryResourceOwner
{
    SqlConnectionSource ConnectionSource { get; }
    SqlResourceOwnership ConnectionOwnership { get; }
    SqlResourceOwnership TransactionOwnership { get; }
    bool HasTransaction { get; }
    bool IsPrimaryReadTransaction { get; }
    string TransactionId { get; }
    string TransactionExecutionMode { get; }

    void EnsureExecutionAvailable();
    IDbConnection GetExecutionConnection();
    IDbTransaction GetTransactionForDiagnostics();
    IDisposable AcquireExecutionLease();
    IDbTransaction GetQueryTransaction();
    Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken cancellationToken = default);
    void MarkPreCancelledUnitOfWorkExecution();
    ISqlOperationCancellationLease CreateOperationCancellationLease(CancellationToken cancellationToken);
}

/// <summary>
/// 写入路径所需的资源所有者最小契约。
/// </summary>
/// <remarks>
/// 该接口保留写入数据源门禁、事务参与和工作单元失败标记；写入不接管查询短事务的创建或清理。
/// </remarks>
internal interface ISqlWriteResourceOwner
{
    void EnsureWritableDataSource(SqlDataSourceDescriptor source = null);
    IDbConnection GetExecutionConnection();
    IDbTransaction GetExecutionTransaction();
    IDbTransaction GetWriteTransaction();
    Task<IDbTransaction> GetWriteTransactionAsync(CancellationToken cancellationToken = default);
    IDisposable AcquireExecutionLease();
    bool HasUnitOfWorkResource { get; }
    bool IsUnitOfWorkRootTransactional { get; }
    ISqlOperationCancellationLease CreateOperationCancellationLease(CancellationToken cancellationToken);
    void MarkPreCancelledUnitOfWorkExecution();
    void MarkUnitOfWorkExecutionFailure();
}
