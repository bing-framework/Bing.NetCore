namespace Bing.Core.Modularity;

/// <summary>
/// 声明模块需要一并扫描的程序集。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class BingModuleAssemblyAttribute : Attribute
{
    /// <summary>
    /// 初始化模块附加程序集声明。
    /// </summary>
    /// <param name="markerType">附加程序集中的公开标记类型。</param>
    public BingModuleAssemblyAttribute(Type markerType) =>
        MarkerType = markerType ?? throw new ArgumentNullException(nameof(markerType));

    /// <summary>
    /// 获取附加程序集的标记类型。
    /// </summary>
    public Type MarkerType { get; }
}

/// <summary>
/// 配置当前应用的服务扫描范围。
/// </summary>
public sealed class BingServiceScanningOptions
{
    /// <summary>
    /// 获取应用级附加程序集。
    /// </summary>
    public IList<Assembly> AdditionalAssemblies { get; } = new List<Assembly>();

    /// <summary>
    /// 获取或设置约定 DI 候选类型筛选器。
    /// </summary>
    /// <remarks>不影响模块发现、选项绑定、手工注册或公开类型事件。</remarks>
    public Func<Type, bool> ConventionalTypeFilter { get; set; }
}
