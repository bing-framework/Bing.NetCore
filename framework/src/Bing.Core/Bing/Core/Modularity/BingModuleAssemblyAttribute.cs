namespace Bing.Core.Modularity;

/// <summary>
/// 声明模块需要一并扫描的程序集。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class BingModuleAssemblyAttribute : Attribute
{
    /// <summary>
    /// 初始化一个 <see cref="BingModuleAssemblyAttribute" /> 类型的实例。
    /// </summary>
    /// <param name="markerType">附加程序集中的公开标记类型。</param>
    public BingModuleAssemblyAttribute(Type markerType) =>
        MarkerType = markerType ?? throw new ArgumentNullException(nameof(markerType));

    /// <summary>
    /// 获取附加程序集的标记类型。
    /// </summary>
    public Type MarkerType { get; }
}
