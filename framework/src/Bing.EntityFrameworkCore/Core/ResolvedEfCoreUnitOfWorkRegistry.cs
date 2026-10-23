using System.Runtime.CompilerServices;
using Bing.Uow;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 按解析顺序记录当前 DI scope 中实际创建的 EF Core 工作单元。
/// </summary>
internal sealed class ResolvedEfCoreUnitOfWorkRegistry : IUnitOfWorkRootStartingObserver
{
    private readonly object _sync = new();
    private readonly HashSet<UnitOfWorkBase> _seen = new(ReferenceComparer.Instance);
    private readonly List<UnitOfWorkBase> _contexts = new();

    public void Register(UnitOfWorkBase context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        lock (_sync)
        {
            if (_seen.Add(context))
                _contexts.Add(context);
        }
    }

    public void Unregister(UnitOfWorkBase context)
    {
        if (context == null)
            return;
        lock (_sync)
        {
            _seen.Remove(context);
            _contexts.Remove(context);
        }
    }

    public UnitOfWorkBase[] Snapshot()
    {
        lock (_sync)
            return _contexts.ToArray();
    }

    public void OnRootStarting(IUnitOfWorkScope scope, IUnitOfWorkScope parentScope)
    {
        if (scope == null || parentScope?.IsActive != true || scope.Id == parentScope.Id)
            return;

        foreach (var context in Snapshot())
            context.CaptureParentStateForOperationRoot(scope, parentScope);
    }

    public void OnRootExited(IUnitOfWorkScope scope) => ReleaseRootProtection(scope);

    public void OnRootStartFailed(IUnitOfWorkScope scope) => ReleaseRootProtection(scope);

    private void ReleaseRootProtection(IUnitOfWorkScope scope)
    {
        if (scope == null)
            return;

        foreach (var context in Snapshot())
            context.ReleaseOperationRootProtection(scope.Id);
    }

    private sealed class ReferenceComparer : IEqualityComparer<UnitOfWorkBase>
    {
        public static ReferenceComparer Instance { get; } = new();
        public bool Equals(UnitOfWorkBase left, UnitOfWorkBase right) => ReferenceEquals(left, right);
        public int GetHashCode(UnitOfWorkBase value) => RuntimeHelpers.GetHashCode(value);
    }
}
