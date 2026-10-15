namespace Bing.Aspects;

/// <summary>
/// 标记参数不得为 <c>null</c>。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class NotNullAttribute : Attribute
{
}
