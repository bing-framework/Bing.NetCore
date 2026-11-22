using System.Data;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>拥有查询专用的事务选择策略及短事务完成状态。</summary>
/// <remarks>
/// 普通查询不创建事务；共享事务仍由 Scope、Uow 或外部调用方拥有。
/// 只有已配置的主库读策略可以为本次查询创建短事务。
/// 连接、事务及执行租约由本 owner 独立持有，复用通用生命周期算法。
/// </remarks>
internal sealed class SqlQueryResourceOwner : SqlExecutionResources, ISqlQueryResourceOwner, ISqlQueryResourceCleanupOwner
{
    private SqlExecutionResourceCore _resourceCore = new(new object());
    private bool _primaryReadTransactionStarted;

    internal SqlQueryResourceOwner(IServiceProvider services, Func<SqlOptions> options,
        Func<Bing.Data.Sql.Builders.ISqlProvider> providerResolver, Func<string> connectionStringResolver,
        Func<bool> isRuntimeInitialized, Func<SqlProviderTransactionCapabilities> transactionCapabilities,
        Action<SqlDataSourceDescriptor> ensureWritable)
        : base(services, options, providerResolver, connectionStringResolver, isRuntimeInitialized,
            transactionCapabilities, ensureWritable) { }

    internal override ref SqlExecutionResourceCore ResourceCore => ref _resourceCore;

    SqlConnectionSource ISqlQueryResourceOwner.ConnectionSource => ConnectionSource;
    SqlResourceOwnership ISqlQueryResourceOwner.ConnectionOwnership => ConnectionOwnership;
    SqlResourceOwnership ISqlQueryResourceOwner.TransactionOwnership => TransactionOwnership;
    bool ISqlQueryResourceOwner.HasTransaction => HasTransaction;
    public bool IsPrimaryReadTransaction => _primaryReadTransactionStarted;
    string ISqlQueryResourceOwner.TransactionId => TransactionId;
    string ISqlQueryResourceOwner.TransactionExecutionMode => TransactionExecutionMode;
    void ISqlQueryResourceOwner.EnsureExecutionAvailable() => EnsureExecutionAvailable();
    IDbConnection ISqlQueryResourceOwner.GetExecutionConnection() => GetExecutionConnection();
    public IDbTransaction GetTransactionForDiagnostics() => GetExecutionTransaction();
    IDisposable ISqlQueryResourceOwner.AcquireExecutionLease() => AcquireExecutionLease();
    void ISqlQueryResourceOwner.MarkPreCancelledUnitOfWorkExecution() => MarkPreCancelledUnitOfWorkExecution();
    ISqlOperationCancellationLease ISqlQueryResourceOwner.CreateOperationCancellationLease(CancellationToken token) =>
        CreateOperationCancellationLease(token);

    /// <summary>优先参与已有事务，仅在显式主库读策略要求时开启查询短事务。</summary>
    public IDbTransaction GetQueryTransaction()
    {
        EnsureExecutionAvailable();
        if (HasUnitOfWorkResource)
            return EnsureUnitOfWorkTransaction();
        var transaction = GetExecutionTransaction();
        if (transaction != null)
            return transaction;
        if (RequiresPrimaryReadTransaction() == false)
            return null;
        var primaryReadTransaction = BeginOwnedTransaction();
        _primaryReadTransactionStarted = true;
        return primaryReadTransaction;
    }

    /// <summary>异步获取可选查询事务，取消不得开启新事务。</summary>
    public async Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken cancellationToken)
    {
        EnsureExecutionAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        if (HasUnitOfWorkResource)
            return await EnsureUnitOfWorkTransactionAsync(cancellationToken).ConfigureAwait(false);
        var transaction = GetExecutionTransaction();
        if (transaction != null)
            return transaction;
        if (RequiresPrimaryReadTransaction() == false)
            return null;
        var primaryReadTransaction = await BeginOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        _primaryReadTransactionStarted = true;
        return primaryReadTransaction;
    }

    private bool RequiresPrimaryReadTransaction()
    {
        var context = Options.GetDatabaseContext();
        return context?.ReadPreference == SqlReadPreference.Primary &&
               context.DataSource?.PrimaryReadStrategy == PrimaryReadStrategy.Transaction;
    }

    /// <summary>只提交本次查询创建的短事务，不完成借用事务。</summary>
    public void CompleteQueryTransaction()
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            CommitOwnedTransaction();
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>异步完成查询短事务，失败后仍清除查询策略状态。</summary>
    public async Task CompleteQueryTransactionAsync(CancellationToken cancellationToken)
    {
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            await CommitOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }

    /// <summary>失败时通知 Uow，或回滚本次查询拥有的短事务。</summary>
    public void RollbackQueryTransaction()
    {
        if (HasUnitOfWorkResource)
        {
            MarkUnitOfWorkRollbackOnly();
            return;
        }
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

    /// <summary>异步回滚查询短事务，清理阶段不继承业务取消令牌。</summary>
    public async Task RollbackQueryTransactionAsync()
    {
        if (HasUnitOfWorkResource)
        {
            MarkUnitOfWorkRollbackOnly();
            return;
        }
        if (_primaryReadTransactionStarted == false)
            return;
        try
        {
            await RollbackOwnedTransactionAsync().ConfigureAwait(false);
        }
        finally
        {
            _primaryReadTransactionStarted = false;
        }
    }
}
