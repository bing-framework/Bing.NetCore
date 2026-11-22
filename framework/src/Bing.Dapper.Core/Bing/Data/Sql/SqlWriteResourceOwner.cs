using System.Data;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>负责写入门禁、可选事务参与及工作单元失败传播。</summary>
/// <remarks>
/// 写入不使用主库读策略，不提交或回滚借用事务。
/// 批次等显式自有事务仍由原写入操作或事务作用域完成。
/// </remarks>
internal sealed class SqlWriteResourceOwner : SqlExecutionResources, ISqlWriteResourceOwner
{
    private SqlExecutionResourceCore _resourceCore = new(new object());

    internal SqlWriteResourceOwner(IServiceProvider services, Func<SqlOptions> options,
        Func<Bing.Data.Sql.Builders.ISqlProvider> providerResolver, Func<string> connectionStringResolver,
        Func<bool> isRuntimeInitialized, Func<SqlProviderTransactionCapabilities> transactionCapabilities,
        Action<SqlDataSourceDescriptor> ensureWritable)
        : base(services, options, providerResolver, connectionStringResolver, isRuntimeInitialized,
            transactionCapabilities, ensureWritable) { }

    internal override ref SqlExecutionResourceCore ResourceCore => ref _resourceCore;

    void ISqlWriteResourceOwner.EnsureWritableDataSource(SqlDataSourceDescriptor source) => EnsureWritableDataSource(source);
    IDbConnection ISqlWriteResourceOwner.GetExecutionConnection() => GetExecutionConnection();
    IDbTransaction ISqlWriteResourceOwner.GetExecutionTransaction() => GetExecutionTransaction();
    IDisposable ISqlWriteResourceOwner.AcquireExecutionLease() => AcquireExecutionLease();
    bool ISqlWriteResourceOwner.HasUnitOfWorkResource => HasUnitOfWorkResource;
    bool ISqlWriteResourceOwner.IsUnitOfWorkRootTransactional => IsUnitOfWorkRootTransactional;
    ISqlOperationCancellationLease ISqlWriteResourceOwner.CreateOperationCancellationLease(CancellationToken token) =>
        CreateOperationCancellationLease(token);
    void ISqlWriteResourceOwner.MarkPreCancelledUnitOfWorkExecution() => MarkPreCancelledUnitOfWorkExecution();
    void ISqlWriteResourceOwner.MarkUnitOfWorkExecutionFailure() => MarkUnitOfWorkExecutionFailure();

    /// <summary>写入只参与 Uow 或已有事务，不应用查询专用策略。</summary>
    public IDbTransaction GetWriteTransaction()
    {
        EnsureExecutionAvailable();
        return HasUnitOfWorkResource
            ? EnsureUnitOfWorkTransaction()
            : GetExecutionTransaction();
    }

    /// <summary>异步准备写入事务，非事务 Uow 保持无事务语义。</summary>
    public async Task<IDbTransaction> GetWriteTransactionAsync(CancellationToken cancellationToken)
    {
        EnsureExecutionAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        return HasUnitOfWorkResource
            ? await EnsureUnitOfWorkTransactionAsync(cancellationToken).ConfigureAwait(false)
            : GetExecutionTransaction();
    }
}
