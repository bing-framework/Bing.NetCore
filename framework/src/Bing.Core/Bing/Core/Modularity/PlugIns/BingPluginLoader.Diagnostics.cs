using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

internal static partial class BingPluginLoader
{
    /// <summary>
    /// 附加插件上下文并保留原始异常类型。
    /// </summary>
    /// <param name="error">原始异常。</param>
    /// <param name="record">发生错误的插件记录。</param>
    /// <param name="phase">失败阶段。</param>
    /// <param name="assembly">相关程序集路径。</param>
    /// <param name="dependencyPath">相关插件依赖链。</param>
    /// <returns>附加插件诊断信息的原始异常。</returns>
    private static Exception AttachPluginContext(Exception error, PluginRecord record, string phase,
        string assembly = null, IEnumerable<string> dependencyPath = null)
    {
        error.Data[PluginIdDataKey] = record.Id;
        error.Data[PluginManifestDataKey] = record.ManifestPath;
        error.Data[PluginPhaseDataKey] = phase;
        if (!error.Data.Contains(PluginAssemblyDataKey))
            error.Data[PluginAssemblyDataKey] = assembly ?? record.EntryAssemblyPath;
        if (dependencyPath != null)
            error.Data[PluginDependencyPathDataKey] = dependencyPath.ToArray();
        return error;
    }

    /// <summary>
    /// 获取程序集文件位置。
    /// </summary>
    /// <param name="assembly">目标程序集。</param>
    /// <returns>程序集文件位置；位置不可用时返回空字符串。</returns>
    private static string GetAssemblyLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch { return string.Empty; }
    }
}
