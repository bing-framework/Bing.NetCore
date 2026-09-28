using Bing.Core.PluginScanDependency;
using Bing.DependencyInjection;

namespace Bing.Core.PluginScanFixtures;

/// <summary>
/// 标识第二版本插件的附加服务程序集。
/// </summary>
public sealed class PluginScanMarker { }

/// <summary>
/// 第二版本附加服务契约。
/// </summary>
public interface IPluginScanService
{
    /// <summary>
    /// 获取服务版本。
    /// </summary>
    string Version { get; }
}

/// <summary>
/// 第二版本附加服务。
/// </summary>
public sealed class PluginScanService : IPluginScanService, ITransientDependency
{
    /// <inheritdoc />
    public string Version => "v2";
}

/// <summary>
/// 要求附加依赖存在才能完成类型发现。
/// </summary>
public sealed class PluginBrokenType : PluginScanBase { }
