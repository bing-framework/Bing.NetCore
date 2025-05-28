namespace Bing;

/// <summary>
/// 禁用功能 特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DisableBingFeaturesAttribute : Attribute
{
    /// <summary>
    /// 是否禁用拦截器。
    /// 默认值为true。
    /// </summary>
    public bool DisableInterceptors { get; set; } = true;

    /// <summary>
    /// 是否禁用中间件。
    /// 默认值为true。
    /// </summary>
    public bool DisableMiddleware { get; set; } = true;

    /// <summary>
    /// 是否禁用MVC过滤器。
    /// 默认值为true。
    /// </summary>
    public bool DisableMvcFilters { get; set; } = true;
}
