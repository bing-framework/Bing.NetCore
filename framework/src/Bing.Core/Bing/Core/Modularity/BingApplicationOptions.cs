using System.Runtime.Loader;

namespace Bing.Core.Modularity;

/// <summary>
/// 启动期模块应用的配置。
/// </summary>
/// <remarks>
/// 选项仅在注册应用时读取，后续修改不会重新构建模块图。
/// </remarks>
public sealed class BingApplicationOptions
{
    /// <summary>
    /// 获取当前应用的服务扫描配置。
    /// </summary>
    public BingServiceScanningOptions ServiceScanning { get; } = new();

    /// <summary>
    /// 获取或设置约定服务注册的执行时机。
    /// </summary>
    public BingServiceRegistrationMode ServiceRegistrationMode { get; set; } = BingServiceRegistrationMode.Compatible;
    /// <summary>
    /// 动态运行库为本次注册指定的插件加载上下文。
    /// </summary>
    internal AssemblyLoadContext PluginLoadContext { get; set; }

    /// <summary>
    /// 获取启动期插件来源。
    /// </summary>
    /// <remarks>
    /// 插件来源仅在应用注册期间读取；默认加载上下文中的程序集不会在运行中卸载。
    /// </remarks>
    public BingPluginSourceList PluginSources { get; } = new();

    /// <summary>
    /// 获取除启动模块外需要加载的根模块。
    /// </summary>
    public IList<Type> AdditionalModules { get; } = new List<Type>();

    /// <summary>
    /// 获取禁止加载的模块。
    /// </summary>
    /// <remarks>
    /// 若模块被显式选中或被其他模块依赖，应用注册会立即失败。
    /// </remarks>
    public IList<Type> ExcludedModules { get; } = new List<Type>();

    /// <summary>
    /// 获取或设置是否扫描选中模块所在程序集的约定服务。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>true</c>；设置为 <c>false</c> 后由调用方手工注册服务。
    /// </remarks>
    public bool AutoRegisterServices { get; set; } = true;
}

/// <summary>
/// 约定服务注册的执行时机。
/// </summary>
public enum BingServiceRegistrationMode
{
    /// <summary>
    /// 保留现有服务注册顺序。
    /// </summary>
    Compatible,
    /// <summary>
    /// 在模块主服务配置前注册约定服务。
    /// </summary>
    BeforeConfigureServices
}
