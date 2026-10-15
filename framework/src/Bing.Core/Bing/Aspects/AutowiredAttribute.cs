namespace Bing.Aspects;

/// <summary>
/// 标记由依赖注入容器设置的属性或字段。
/// </summary>
/// <remarks>
/// 字段标记保留用于兼容，当前 AspectCore 适配器只处理公开可写实例属性。
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    Inherited = true,
    AllowMultiple = false)]
public class AutowiredAttribute : Attribute
{
}
