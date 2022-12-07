using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;
using Bing.Tracing;
using DotNetCore.CAP;

namespace Bing.Events.Cap;

/// <summary>
/// CAP跟踪 属性
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CapTraceAttribute : InterceptorBase
{
    public override async Task Invoke(AspectContext context, AspectDelegate next)
    {
        var parameters = context.GetParameters();
        var param = parameters.FirstOrDefault(x => x.ParameterInfo.GetCustomAttribute<FromCapAttribute>() != null);
        if (param == null)
        {
            await next(context);
            return;
        }

        if (param.Value is CapHeader capHeader)
        {
            InitTraceIdContext(capHeader);
            await next(context);
        }
    }

    /// <summary>
    /// 初始化跟踪标识上下文
    /// </summary>
    private static void InitTraceIdContext(CapHeader capHeader)
    {
        if (!capHeader.TryGetValue(Headers.TraceId, out var traceId))
            return;

        if (TraceIdContext.Current == null)
            TraceIdContext.Current = new TraceIdContext(traceId);
        else
            TraceIdContext.Current.TraceId = traceId;
    }
}