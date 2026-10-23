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
    /// <inheritdoc />
    public override Task Invoke(AspectContext context, AspectDelegate next)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = InvokeCoreAsync(context, next, completion);
        return completion.Task;
    }

    private static async Task InvokeCoreAsync(AspectContext context, AspectDelegate next,
        TaskCompletionSource completion)
    {
        var parent = TraceIdContext.Current;
        Exception invocationException = null;
        try
        {
            var parameters = context.GetParameters();
            var param = parameters.FirstOrDefault(x =>
                x.ParameterInfo.GetCustomAttribute<FromCapAttribute>() != null);
            if (param?.Value is CapHeader capHeader &&
                capHeader.TryGetValue(Headers.TraceId, out var traceId) &&
                !string.IsNullOrEmpty(traceId))
                TraceIdContext.Current = new TraceIdContext(traceId);

            await next(context);
        }
        catch (Exception exception)
        {
            invocationException = exception;
        }
        finally
        {
            TraceIdContext.Current = parent;
        }

        if (invocationException == null)
            completion.TrySetResult();
        else
            completion.TrySetException(invocationException);
    }
}
