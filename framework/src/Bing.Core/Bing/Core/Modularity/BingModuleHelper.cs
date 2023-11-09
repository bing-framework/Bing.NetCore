using Microsoft.Extensions.Logging;

namespace Bing.Core.Modularity;

/// <summary>
/// Bing模块帮助类
/// </summary>
internal static class BingModuleHelper
{
    /// <summary>
    /// 查找全部模块类型
    /// </summary>
    /// <param name="startupModuleType">启动模块类型</param>
    /// <param name="logger">日志</param>
    public static List<Type> FindAllModuleTypes(Type startupModuleType, ILogger logger)
    {
        var moduleTypes = new List<Type>();
        logger.Log(LogLevel.Information, "Loaded Bing modules:");
        AddModuleAndDependenciesRecursively(moduleTypes, startupModuleType, logger);
        return moduleTypes;
    }

    /// <summary>
    /// 查找依赖的模块类型
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    public static List<Type> FindDependedModuleTypes(Type moduleType)
    {
        BingModule.CheckBingModuleType(moduleType);

        var dependencies = new List<Type>();

        var dependencyDescriptors = moduleType
            .GetCustomAttributes()
            .OfType<IDependedTypesProvider>();

        foreach (var descriptor in dependencyDescriptors)
        {
            foreach (var dependedModuleType in descriptor.GetDependedTypes())
            {
                dependencies.AddIfNotContains(dependedModuleType);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// 递归添加模块以及依赖项
    /// </summary>
    /// <param name="moduleTypes">模块类型列表</param>
    /// <param name="moduleType">模块类型</param>
    /// <param name="logger">日志</param>
    /// <param name="depth">深度</param>
    private static void AddModuleAndDependenciesRecursively(List<Type> moduleTypes, Type moduleType, ILogger logger, int depth = 0)
    {
        BingModule.CheckBingModuleType(moduleType);
        if (moduleTypes.Contains(moduleType))
            return;

        moduleTypes.Add(moduleType);
        logger.Log(LogLevel.Information, $"{new string(' ', depth * 2)}- {moduleType.FullName}");

        foreach (var dependedModuleType in FindDependedModuleTypes(moduleType))
            AddModuleAndDependenciesRecursively(moduleTypes, dependedModuleType, logger, depth + 1);
    }
}
