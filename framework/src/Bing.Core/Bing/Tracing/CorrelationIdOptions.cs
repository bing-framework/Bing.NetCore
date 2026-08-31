namespace Bing.Tracing;

/// <summary>
/// 配置 HTTP 请求关联标识的读取和响应写入行为。
/// </summary>
public class CorrelationIdOptions
{
    /// <summary>
    /// 获取或设置承载关联标识的 HTTP 请求头名称。
    /// </summary>
    public string HttpHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// 获取或设置是否将关联标识写入 HTTP 响应头。
    /// </summary>
    public bool SetResponseHeader { get; set; } = true;
}
