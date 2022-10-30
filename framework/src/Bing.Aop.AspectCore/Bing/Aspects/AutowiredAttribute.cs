using AspectCore.DependencyInjection;

namespace Bing.Aspects;

/// <summary>
/// 属性注入 属性
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class AutowiredAttribute : FromServiceContextAttribute
{
}
