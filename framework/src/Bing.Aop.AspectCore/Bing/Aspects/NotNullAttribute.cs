using AspectCore.DynamicProxy.Parameters;

namespace Bing.Aspects;

/// <summary>
/// 标记参数不得为 <c>null</c>，并在方法执行前进行校验。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class NotNullAttribute : ParameterInterceptorBase
{
    /// <inheritdoc />
    /// <remarks>
    /// 在目标方法执行前检查参数值；为空时抛出参数异常，否则继续执行拦截链。
    /// </remarks>
    public override Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        if (context.Parameter.Value == null)
            throw new ArgumentNullException(context.Parameter.Name);
        return next(context);
    }
}
