using AspectCore.DynamicProxy.Parameters;

namespace Bing.Aspects;

/// <summary>
/// 标记参数不得为 <c>null</c>，并在方法执行前进行校验。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class NotNullAttribute : ParameterInterceptorBase
{
    /// <summary>
    /// 在目标方法执行前检查参数值；为空时抛出参数异常，否则继续执行拦截链。
    /// </summary>
    /// <param name="context">参数拦截上下文。</param>
    /// <param name="next">后续拦截器或目标方法委托。</param>
    public override Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        if (context.Parameter.Value == null)
            throw new ArgumentNullException(context.Parameter.Name);
        return next(context);
    }
}
