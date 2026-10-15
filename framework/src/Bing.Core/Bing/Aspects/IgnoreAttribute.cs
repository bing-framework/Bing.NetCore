namespace Bing.Aspects;

/// <summary>
/// 标记目标不参与 AOP 拦截。
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class IgnoreAttribute : Attribute
{
}
