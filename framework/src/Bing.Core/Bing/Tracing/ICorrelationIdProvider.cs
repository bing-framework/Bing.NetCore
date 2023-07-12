namespace Bing.Tracing;

/// <summary>
/// 跟踪标识提供程序
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// 获取跟踪标识
    /// </summary>
    string Get();
}
