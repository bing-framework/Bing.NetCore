namespace Bing.Validation;

/// <summary>
/// 标记参数需要执行模型验证。
/// </summary>
/// <remarks>
/// 该特性只表达验证意图，具体执行由对应的验证适配器提供。
/// </remarks>
public class ValidAttribute : Attribute
{
}
