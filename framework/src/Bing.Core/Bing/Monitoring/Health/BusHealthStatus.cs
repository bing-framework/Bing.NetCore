namespace Bing.Monitoring.Health;

/// <summary>
/// 标识消息总线健康检查的总体状态。
/// </summary>
public enum BusHealthStatus
{
    /// <summary>
    /// 消息总线不可用或健康检查失败。
    /// </summary>
    Unhealthy = 0,
    /// <summary>
    /// 消息总线仍可用，但健康状况已下降。
    /// </summary>
    Degraded = 1,
    /// <summary>
    /// 消息总线正常提供服务。
    /// </summary>
    Healthy = 2
}
