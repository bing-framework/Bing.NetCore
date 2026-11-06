using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 配置 HTTP 流量闸门和安全排空行为。
/// </summary>
public sealed class BingTrafficGateOptions
{
    /// <summary>
    /// 获取或设置是否在服务提供程序创建时直接进入接收状态。
    /// </summary>
    /// <remarks>关闭后应在依赖预热完成时调用 <c>MarkBingTrafficReady</c>。</remarks>
    /// <remarks>默认值为 true。</remarks>
    public bool StartReady { get; set; } = true;

    /// <summary>
    /// 获取或设置就绪探针路径。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;/health/ready&quot;</c>。</remarks>
    public string ReadinessPath { get; set; } = "/health/ready";

    /// <summary>
    /// 获取或设置停止时等待在途请求完成的最长时间。
    /// </summary>
    /// <remarks>默认值为 30 秒。</remarks>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 获取或设置排空或未就绪时返回的 HTTP 状态码。
    /// </summary>
    public int UnavailableStatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;
}
