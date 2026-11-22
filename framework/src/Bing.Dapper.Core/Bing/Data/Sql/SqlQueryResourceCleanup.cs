namespace Bing.Data.Sql;

/// <summary>
/// 查询路径的事务清理访问视图。
/// </summary>
/// <remarks>
/// 该类型只保存窄清理契约引用，不拥有连接、事务或工作单元资源，也不产生额外堆分配。
/// </remarks>
internal readonly struct SqlQueryResourceCleanup
{
    private readonly ISqlQueryResourceCleanupOwner _owner;

    internal SqlQueryResourceCleanup(ISqlQueryResourceCleanupOwner owner)
    {
        _owner = owner;
    }

    internal void CompleteQueryTransaction() => _owner.CompleteQueryTransaction();

    internal Task CompleteQueryTransactionAsync(CancellationToken cancellationToken = default) =>
        _owner.CompleteQueryTransactionAsync(cancellationToken);

    internal void RollbackQueryTransaction() => _owner.RollbackQueryTransaction();

    internal Task RollbackQueryTransactionAsync() => _owner.RollbackQueryTransactionAsync();
}
