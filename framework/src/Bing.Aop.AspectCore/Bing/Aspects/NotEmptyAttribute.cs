using AspectCore.DynamicProxy.Parameters;
using Bing.Extensions;

namespace Bing.Aspects;

/// <summary>
/// 标记参数不得为空字符串、空白字符串或可转换为空文本的值。
/// </summary>
public class NotEmptyAttribute : ParameterInterceptorBase
{
    /// <inheritdoc />
    /// <remarks>
    /// 在目标方法执行前检查参数文本；为空时抛出参数异常，否则继续执行拦截链。
    /// </remarks>
    public override Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        if (string.IsNullOrWhiteSpace(context.Parameter.Value.SafeString()))
            throw new ArgumentNullException(context.Parameter.Name);
        return next(context);
    }
}
