namespace Bing.Core.Modularity;

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
