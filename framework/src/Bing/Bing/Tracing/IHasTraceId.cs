namespace Bing.Tracing;

/// <summary>
/// 定义跟踪标识
/// </summary>
public interface IHasTraceId
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    string TraceId { get; set; }
}
