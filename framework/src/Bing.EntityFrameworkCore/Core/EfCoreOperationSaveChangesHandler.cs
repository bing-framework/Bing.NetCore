using Bing.Uow;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 在 operation root 完成前保存当前 scope 中已解析的 EF Core 工作单元。
/// </summary>
internal sealed class EfCoreOperationSaveChangesHandler : IUnitOfWorkSaveChangesHandler
{
    private readonly ResolvedEfCoreUnitOfWorkRegistry _registry;

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

    private void RejectRequiresNewContextReuse(IUnitOfWorkScope scope,
        IEnumerable<UnitOfWorkBase> resolvedContexts)
    {
        foreach (var context in resolvedContexts)
            context.EnsureOperationRootMayUseContext(scope);
    }

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
