using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 配置 HTTP 流量闸门和安全排空行为。
/// </summary>
public sealed class BingTrafficGateOptions
{
    /// <summary>
    /// 是否在服务提供程序创建时直接进入接收状态。
    /// </summary>
    /// <remarks>关闭后应在依赖预热完成时调用 <c>MarkBingTrafficReady</c>。</remarks>
    public bool StartReady { get; set; } = true;

    /// <summary>
    /// 就绪探针路径。
    /// </summary>
    public string ReadinessPath { get; set; } = "/health/ready";

    /// <summary>
    /// 停止时等待在途请求完成的最长时间。
    /// </summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 排空或未就绪时返回的 HTTP 状态码。
    /// </summary>
    public int UnavailableStatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;
}
