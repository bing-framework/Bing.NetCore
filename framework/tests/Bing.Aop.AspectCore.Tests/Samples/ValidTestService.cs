namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// <see cref="IValidTestService"/> 的默认实现，
/// 方法本体只原样返回字符串，验证逻辑完全由 AOP 拦截器处理。
/// </summary>
public class ValidTestService : IValidTestService
{
    /// <inheritdoc />
    public string ProcessObject(object input) => "processed";
}
