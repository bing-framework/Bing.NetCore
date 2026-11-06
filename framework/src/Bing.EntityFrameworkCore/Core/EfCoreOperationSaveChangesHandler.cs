using Bing.Uow;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 在 operation root 完成前保存当前 scope 中已解析的 EF Core 工作单元。
/// </summary>
internal sealed class EfCoreOperationSaveChangesHandler : IUnitOfWorkSaveChangesHandler
{
    /// <summary>
    /// 保存已解析的 EF Core 工作单元注册表。
    /// </summary>
    private readonly ResolvedEfCoreUnitOfWorkRegistry _registry;

    /// <summary>
    /// 初始化一个 <see cref="EfCoreOperationSaveChangesHandler" /> 类型的实例。
    /// </summary>
    /// <param name="registry">用于注册或查找对应任务、服务或策略的注册表。</param>
    public EfCoreOperationSaveChangesHandler(ResolvedEfCoreUnitOfWorkRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public void SaveChanges(IUnitOfWorkScope scope)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        var snapshot = _registry.Snapshot();
        RejectRequiresNewContextReuse(scope, snapshot);
        foreach (var context in snapshot)
        {
            if (context.HasPendingOperationChanges(out _))
                context.SaveOperationChanges(scope);
        }

        RejectLatePendingChanges(scope, _registry.Snapshot());
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(IUnitOfWorkScope scope, CancellationToken cancellationToken = default)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        var snapshot = _registry.Snapshot();
        RejectRequiresNewContextReuse(scope, snapshot);
        foreach (var context in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.HasPendingOperationChanges(out _))
                await context.SaveOperationChangesAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RejectLatePendingChanges(scope, _registry.Snapshot());
    }

    /// <summary>
    /// 检查操作根是否复用了不允许共享的上下文。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <param name="resolvedContexts">已解析的 EF Core 工作单元上下文集合。</param>
    private void RejectRequiresNewContextReuse(IUnitOfWorkScope scope,
        IEnumerable<UnitOfWorkBase> resolvedContexts)
    {
        foreach (var context in resolvedContexts)
            context.EnsureOperationRootMayUseContext(scope);
    }

    /// <summary>
    /// 拒绝最终保存后仍有待处理变更的上下文。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <param name="resolvedContexts">已解析的 EF Core 工作单元上下文集合。</param>
    /// <remarks>发现待处理变更时将作用域标记为仅回滚，并拒绝继续完成操作。</remarks>
    private static void RejectLatePendingChanges(IUnitOfWorkScope scope,
        IEnumerable<UnitOfWorkBase> resolvedContexts)
    {
        foreach (var context in resolvedContexts)
        {
            if (!context.HasPendingOperationChanges(out var reason))
                continue;

            scope.SetRollbackOnly();
            throw new InvalidOperationException(
                $"EF Core 工作单元 {context.GetType().FullName} 在最终保存快照后仍有待处理状态（{reason}）；请显式组织跨 Context 保存。");
        }
    }
}
