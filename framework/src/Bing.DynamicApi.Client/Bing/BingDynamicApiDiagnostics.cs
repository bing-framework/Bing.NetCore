using System.Diagnostics;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 记录动态 API 逻辑调用与描述预算的诊断源。
/// </summary>
/// <remarks>
/// 记录 HTTP 管线之外的逻辑调用和共享描述预算，不增加请求指标维度。
/// </remarks>
internal static class BingDynamicApiDiagnostics
{
    /// <summary>
    /// 发布动态 API 客户端活动跟踪。
    /// </summary>
    private static readonly ActivitySource Source = new("Bing.DynamicApi.Client");

    /// <summary>
    /// 为动态 API 定义或调用创建诊断活动。
    /// </summary>
    /// <param name="options">当前远程服务的韧性和超时配置。</param>
    /// <param name="remoteName">目标远程服务名称。</param>
    /// <param name="kind">定义读取或 API 调用类型。</param>
    /// <returns>创建的内部 Activity；未启用相应超时配置或未被诊断监听器采样时返回 null。</returns>
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

    /// <summary>
    /// 记录动态 API 请求取消诊断事件。
    /// </summary>
    /// <param name="activity">要附加事件和错误状态的诊断活动；可以为 null。</param>
    /// <param name="reason">取消或超时原因。</param>
    internal static void RecordCancellation(Activity activity, string reason)
    {
        activity?.AddEvent(new ActivityEvent(reason));
        activity?.SetStatus(ActivityStatusCode.Error, reason);
    }
}
