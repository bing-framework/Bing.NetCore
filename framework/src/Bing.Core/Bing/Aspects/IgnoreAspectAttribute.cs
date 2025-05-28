namespace Bing.Aspects;

/// <summary>
/// 忽略拦截 特性
/// </summary>
/// <remarks>主要针对于使用第三方AOP的时候，内置接口需要忽略拦截器处理。</remarks>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class IgnoreAspectAttribute : Attribute
{
}
