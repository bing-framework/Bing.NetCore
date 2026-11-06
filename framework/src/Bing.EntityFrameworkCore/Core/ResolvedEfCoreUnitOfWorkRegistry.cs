using System.Runtime.CompilerServices;
using Bing.Uow;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 按解析顺序记录当前 DI scope 中实际创建的 EF Core 工作单元。
/// </summary>
internal sealed class ResolvedEfCoreUnitOfWorkRegistry : IUnitOfWorkRootStartingObserver
{
    /// <summary>
    /// 同步已解析工作单元集合的并发访问。
    /// </summary>
    private readonly object _sync = new();
    /// <summary>
    /// 按引用去重已解析的工作单元上下文。
    /// </summary>
    private readonly HashSet<UnitOfWorkBase> _seen = new(ReferenceComparer.Instance);
    /// <summary>
    /// 保存按解析顺序登记的工作单元上下文。
    /// </summary>
    private readonly List<UnitOfWorkBase> _contexts = new();

    /// <summary>
    /// 登记已解析的 EF Core 工作单元上下文。
    /// </summary>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
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

    /// <summary>
    /// 移除已登记的 EF Core 工作单元上下文。
    /// </summary>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
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

    /// <summary>
    /// 获取已登记上下文的快照。
    /// </summary>
    /// <returns>按登记顺序排列的上下文数组。</returns>
    public UnitOfWorkBase[] Snapshot()
    {
        lock (_sync)
            return _contexts.ToArray();
    }

    /// <inheritdoc />
    public void OnRootStarting(IUnitOfWorkScope scope, IUnitOfWorkScope parentScope)
    {
        if (scope == null || parentScope?.IsActive != true || scope.Id == parentScope.Id)
            return;

        foreach (var context in Snapshot())
            context.CaptureParentStateForOperationRoot(scope, parentScope);
    }

    /// <inheritdoc />
    public void OnRootExited(IUnitOfWorkScope scope) => ReleaseRootProtection(scope);

    /// <inheritdoc />
    public void OnRootStartFailed(IUnitOfWorkScope scope) => ReleaseRootProtection(scope);

    /// <summary>
    /// 释放指定操作根对已登记上下文的保护。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    private void ReleaseRootProtection(IUnitOfWorkScope scope)
    {
        if (scope == null)
            return;

        foreach (var context in Snapshot())
            context.ReleaseOperationRootProtection(scope.Id);
    }

    /// <summary>
    /// 按工作单元实例引用比较相等性。
    /// </summary>
    private sealed class ReferenceComparer : IEqualityComparer<UnitOfWorkBase>
    {
        /// <summary>
        /// 获取用于引用相等比较的共享比较器。
        /// </summary>
        public static ReferenceComparer Instance { get; } = new();
        /// <inheritdoc />
        public bool Equals(UnitOfWorkBase left, UnitOfWorkBase right) => ReferenceEquals(left, right);
        /// <inheritdoc />
        public int GetHashCode(UnitOfWorkBase value) => RuntimeHelpers.GetHashCode(value);
    }
}
