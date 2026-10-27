using System.Diagnostics;

namespace Bing.DynamicApi.Client;

/// <summary>记录 HTTP 管线之外的逻辑调用和共享描述预算，不增加请求指标维度。</summary>
internal static class BingDynamicApiDiagnostics
{
    private static readonly ActivitySource Source = new("Bing.DynamicApi.Client");

    /// <summary>仅为显式启用新模式或预算的调用创建活动；无人订阅时不创建活动。</summary>
    internal static Activity Start(BingDynamicApiRemoteOptions options, string remoteName, BingDynamicApiRequestKind kind)
    {
        if (options.RetryMode == BingDynamicApiRetryMode.Legacy &&
            options.OperationTimeout == null && options.DefinitionTimeout == null)
            return null;
        var activity = Source.StartActivity(kind == BingDynamicApiRequestKind.Definition
            ? "dynamic_api.definition" : "dynamic_api.invocation", ActivityKind.Internal);
        activity?.SetTag("bing.remote.name", remoteName);
        activity?.SetTag("bing.request.kind", kind.ToString());
        return activity;
    }

    /// <summary>记录固定原因，不将异常正文、URL、请求头或业务数据写入活动。</summary>
    internal static void RecordCancellation(Activity activity, string reason)
    {
        activity?.AddEvent(new ActivityEvent(reason));
        activity?.SetStatus(ActivityStatusCode.Error, reason);
    }
}
