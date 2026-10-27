using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Bing.Resilience;

/// <summary>跨 Provider 稳定诊断源；不使用请求地址或操作名称作为指标标签。</summary>
public static class ResilienceDiagnostics
{
    public const string SourceName = "Bing.Resilience";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Executions = Meter.CreateCounter<long>("bing.resilience.executions");
    private static readonly Counter<long> Events = Meter.CreateCounter<long>("bing.resilience.events");

    /// <summary>记录稳定策略事件；诊断记录失败不会改变业务结果。</summary>
    public static void RecordEvent(ResiliencePipelineIdentity id, string provider, string eventName) =>
        Event(id, provider, eventName);

    /// <summary>记录扩展生命周期；调用者仅传入固定事件/结果类别，不传入地址、操作 ID 或载荷。</summary>
    public static void RecordLifecycle(string eventName, string outcome)
    {
        var tags = new TagList { { "resilience.event", eventName }, { "resilience.outcome", outcome } };
        try { Events.Add(1, tags); } catch { }
        // 每类事件都有独立短 span，既可关联当前执行，也可观察后台刷新与排空。
        Activity? activity = null;
        try
        {
            activity = ActivitySource.StartActivity(eventName);
            activity?.SetTag("resilience.outcome", outcome);
        }
        catch { }
        finally { try { activity?.Dispose(); } catch { } }
    }

    internal static void Event(ResiliencePipelineIdentity id, string provider, string eventName)
    {
        // 指标监听者的异常不能改变业务结果。
        try { Events.Add(1, Tags(id, provider, eventName)); } catch { }
    }
    internal static void Complete(ResiliencePipelineIdentity id, string provider, string outcome)
    {
        try { Executions.Add(1, Tags(id, provider, outcome)); } catch { }
    }
    private static TagList Tags(ResiliencePipelineIdentity id, string provider, string outcome) => new()
    {
        { "resilience.pipeline", id.Name }, { "resilience.purpose", id.Purpose },
        { "resilience.partition", id.Partition.StartsWith("dynamic:", StringComparison.Ordinal) ? "dynamic" : id.Partition }, { "resilience.provider", provider }, { "resilience.event", outcome }
    };
}
internal sealed class ObservedPipeline<T>(IResiliencePipeline<T> inner, ResiliencePipelineIdentity identity, string provider) : IResiliencePipeline<T>
{
    public ValueTask<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync((_, ct) => action(ct), new(), cancellationToken);
    }
    public async ValueTask<T> ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> action,
        ResilienceExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action); ArgumentNullException.ThrowIfNull(context);
        // HTTP 组件管线通过同一上下文嵌套，不重复报告逻辑执行。
        var root = context.ExecutionDepth == 0;
        Activity? activity = null;
        if (root)
            try { activity = ResilienceDiagnostics.ActivitySource.StartActivity("resilience.execute"); } catch { }
        var outcome = "success";
        try
        {
            return await inner.ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { outcome = "cancelled"; throw; }
        catch { outcome = "failure"; throw; }
        finally
        {
            if (root) ResilienceDiagnostics.Complete(identity, provider, outcome);
            try { activity?.SetTag("resilience.outcome", outcome); activity?.Dispose(); } catch { }
        }
    }
}
