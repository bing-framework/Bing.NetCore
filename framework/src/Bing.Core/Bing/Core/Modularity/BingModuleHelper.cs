using Microsoft.Extensions.Logging;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供模块发现和依赖查询辅助方法。
/// </summary>
internal static class BingModuleHelper
{
    /// <summary>
    /// 查找启动模块及其依赖类型。
    /// </summary>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <param name="logger">用于记录加载结果的日志对象。</param>
    /// <returns>启动模块及其依赖模块的类型列表。</returns>
    public static List<Type> FindAllModuleTypes(Type startupModuleType, ILogger logger)
    {
        var graph = ModuleDependencyGraph.Build(new[] { startupModuleType });
        logger.Log(LogLevel.Information, "Loaded Bing modules:");
        foreach (var moduleType in graph.Types)
            logger.Log(LogLevel.Information, $"- {moduleType.FullName}");
        return graph.Types.ToList();
    }

    /// <summary>
    /// 查找指定模块的直接依赖类型。
    /// </summary>
    /// <param name="moduleType">模块类型。</param>
    /// <returns>指定模块直接依赖的模块类型列表。</returns>
    public static List<Type> FindDependedModuleTypes(Type moduleType)
    {
        var graph = ModuleDependencyGraph.Build(new[] { moduleType });
        return graph.Dependencies[moduleType].ToList();
    }
}
