namespace Bing.Tracing;

/// <summary>
/// 跟踪标识 选项配置
/// </summary>
public class CorrelationIdOptions
{
    /// <summary>
    /// Http请求头名称
    /// </summary>
    public string HttpHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// 是否将跟踪标识设置在响应头
    /// </summary>
    public bool SetResponseHeader { get; set; } = true;
}
