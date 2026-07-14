using AspectCore.DynamicProxy;
using Bing.Aspects;

namespace Bing.Events.Cap;

/// <summary>
/// CAP跟踪 属性
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CapTraceAttribute : InterceptorBase
{
    public override async Task Invoke(AspectContext context, AspectDelegate next)
    {
        await next(context);
    }
}
