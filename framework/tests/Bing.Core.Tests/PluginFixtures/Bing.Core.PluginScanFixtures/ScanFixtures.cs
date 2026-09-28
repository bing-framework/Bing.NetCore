using Bing.DependencyInjection;
using Bing.Core.PluginScanDependency;

namespace Bing.Core.PluginScanFixtures;

/// <summary>
/// 标识插件的附加服务程序集。
/// </summary>
public sealed class PluginScanMarker { }

/// <summary>
/// 插件附加程序集中的约定服务。
/// </summary>
public interface IPluginScanService
{
    /// <summary>
    /// 获取服务版本。
    /// </summary>
    string Version { get; }
}

/// <summary>
/// 提供插件附加程序集中的约定服务。
/// </summary>
public sealed class PluginScanService : IPluginScanService, ITransientDependency
{
    /// <inheritdoc />
    public string Version => "v1";
}

/// <summary>
/// 要求附加依赖存在才能完成类型发现。
/// </summary>
public sealed class PluginBrokenType : PluginScanBase { }
