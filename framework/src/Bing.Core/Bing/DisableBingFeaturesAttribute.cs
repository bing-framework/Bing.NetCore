namespace Bing;

/// <summary>
/// 禁用功能 特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DisableBingFeaturesAttribute : Attribute
{
    /// <summary>
    /// 获取或设置是否禁用拦截器。
    /// </summary>
    /// <remarks>默认值为true。</remarks>
    public bool DisableInterceptors { get; set; } = true;

    /// <summary>
    /// 获取或设置是否禁用中间件。
    /// </summary>
    /// <remarks>默认值为true。</remarks>
    public bool DisableMiddleware { get; set; } = true;

    /// <summary>
    /// 获取或设置是否禁用MVC过滤器。
    /// </summary>
    /// <remarks>默认值为true。</remarks>
    public bool DisableMvcFilters { get; set; } = true;
}
