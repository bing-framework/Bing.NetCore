namespace Bing.Core.Modularity;

/// <summary>
/// 按依赖拓扑顺序加载模块。
/// </summary>
/// <remarks>
/// <see cref="BingModule.Level"/>、<see cref="BingModule.Order"/> 和类型名称仅用于就绪节点之间的排序。
/// </remarks>
public class BingModuleLoader : IBingModuleLoader
{
    /// <inheritdoc />
    /// <remarks>
    /// 模块构造失败时会释放已经创建的模块实例，并保留原始异常。
    /// </remarks>
    public virtual IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules)
    {
        var graph = ModuleDependencyGraph.Build(roots, excludedModules);
        var instances = new Dictionary<Type, BingModule>();
        try
        {
            foreach (var type in graph.Types)
                instances.Add(type, (BingModule)Activator.CreateInstance(type));

            var result = new List<BingModuleDescriptor>();
            var remaining = new HashSet<Type>(graph.Types);
            var completed = new HashSet<Type>();
            while (remaining.Count > 0)
            {
                var next = remaining.Where(t => graph.Dependencies[t].All(completed.Contains))
                    .OrderBy(t => instances[t].Level).ThenBy(t => instances[t].Order)
                    .ThenBy(t => t.FullName, StringComparer.Ordinal).First();
                result.Add(new BingModuleDescriptor(instances[next], graph.Dependencies[next], result.Count));
                remaining.Remove(next);
                completed.Add(next);
            }
            return result.AsReadOnly();
        }
        catch (Exception error)
        {
            // 构造失败发生在同步注册阶段，仍须释放已成功创建的模块。
            // 对仅实现 IAsyncDisposable 的模块同步等待 DisposeAsync，保留原始构造异常。
            var cleanup = new List<Exception>();
            foreach (var instance in instances.Values.Reverse())
            {
                try { ReleaseInstance(instance); }
                catch (Exception ex) { cleanup.Add(ex); }
            }
            if (cleanup.Count > 0)
                error.Data["Bing.ModuleCleanupErrors"] = new AggregateException(cleanup);
            throw;
        }
    }

    /// <summary>
    /// 释放单个模块实例。
    /// </summary>
    /// <param name="instance">需要释放的模块实例。</param>
    private static void ReleaseInstance(BingModule instance)
    {
        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (instance is IAsyncDisposable asyncDisposable)
            BingModuleDisposal.DisposeSynchronously(asyncDisposable);
    }
}
