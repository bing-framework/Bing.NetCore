using AspectCore.DependencyInjection;

namespace Bing.Aspects;

/// <summary>
/// 标记属性或字段通过 AspectCore 服务上下文进行依赖注入。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class AutowiredAttribute : FromServiceContextAttribute
{
}
