namespace Bing.Monitoring.Health;

/// <summary>
/// 业务健康状态
/// </summary>
public enum BusHealthStatus
{
    /// <summary>
    /// 不良
    /// </summary>
    Unhealthy = 0,
    /// <summary>
    /// 降级
    /// </summary>
    Degraded = 1,
    /// <summary>
    /// 健康
    /// </summary>
    Healthy = 2
}
