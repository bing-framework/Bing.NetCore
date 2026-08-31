namespace Bing.Monitoring.Health;

/// <summary>
/// 表示业务健康检查的不可变结果。
/// </summary>
public readonly struct BusHealthResult
{
    /// <summary>
    /// 用于未提供附加数据时的共享空只读字典。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly IReadOnlyDictionary<string, object> _emptyReadOnlyDictionary = new Dictionary<string, object>();

    /// <summary>
    /// 使用业务健康状态和可选诊断信息初始化 <see cref="BusHealthResult"/> 的实例。
    /// </summary>
    /// <param name="status">业务健康状态。</param>
    /// <param name="description">可选的状态说明。</param>
    /// <param name="exception">导致降级或不健康状态的可选异常。</param>
    /// <param name="data">可选的附加诊断数据。</param>
    private BusHealthResult(BusHealthStatus status, string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null)
    {
        Status = status;
        Description = description;
        Exception = exception;
        Data = data ?? _emptyReadOnlyDictionary;
    }

    /// <summary>
    /// 获取附加诊断数据；未提供时为空只读字典。
    /// </summary>
    public readonly IReadOnlyDictionary<string, object> Data;

    /// <summary>
    /// 获取可选的状态说明。
    /// </summary>
    public readonly string Description;

    /// <summary>
    /// 获取导致当前状态的可选异常。
    /// </summary>
    public readonly Exception Exception;

    /// <summary>
    /// 获取业务健康状态。
    /// </summary>
    public readonly BusHealthStatus Status;

    /// <summary>
    /// 创建健康状态结果。
    /// </summary>
    /// <param name="description">可选的状态说明。</param>
    /// <param name="data">可选的附加诊断数据。</param>
    /// <returns>状态为 <see cref="BusHealthStatus.Healthy"/> 的健康结果。</returns>
    public static BusHealthResult Healthy(string description = null, IReadOnlyDictionary<string, object> data = null) => new(BusHealthStatus.Healthy, description, null, data);

    /// <summary>
    /// 创建降级状态结果。
    /// </summary>
    /// <param name="description">可选的状态说明。</param>
    /// <param name="exception">导致降级状态的可选异常。</param>
    /// <param name="data">可选的附加诊断数据。</param>
    /// <returns>状态为 <see cref="BusHealthStatus.Degraded"/> 的健康结果。</returns>
    public static BusHealthResult Degraded(string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null) =>
        new(BusHealthStatus.Degraded, description, exception, data);

    /// <summary>
    /// 创建不健康状态结果。
    /// </summary>
    /// <param name="description">可选的状态说明。</param>
    /// <param name="exception">导致不健康状态的可选异常。</param>
    /// <param name="data">可选的附加诊断数据。</param>
    /// <returns>状态为 <see cref="BusHealthStatus.Unhealthy"/> 的健康结果。</returns>
    public static BusHealthResult Unhealthy(string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null) =>
        new(BusHealthStatus.Unhealthy, description, exception, data);
}
