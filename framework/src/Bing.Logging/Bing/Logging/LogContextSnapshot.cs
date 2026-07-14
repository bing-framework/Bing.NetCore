namespace Bing.Logging;

/// <summary>
/// 不可变日志上下文快照
/// </summary>
public sealed class LogContextSnapshot
{
    /// <summary>
    /// 初始化一个<see cref="LogContextSnapshot"/>类型的实例
    /// </summary>
    public LogContextSnapshot(
        string traceId,
        LogIdentityContext identity = null,
        LogClientContext client = null,
        BusinessLogContext business = null)
    {
        TraceId = traceId;
        Identity = identity ?? new LogIdentityContext();
        Client = client ?? new LogClientContext();
        Business = business ?? new BusinessLogContext();
    }

    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; }

    /// <summary>
    /// 身份上下文
    /// </summary>
    public LogIdentityContext Identity { get; }

    /// <summary>
    /// 客户端上下文
    /// </summary>
    public LogClientContext Client { get; }

    /// <summary>
    /// 业务上下文
    /// </summary>
    public BusinessLogContext Business { get; }

    /// <summary>
    /// 使用新的跟踪标识创建快照
    /// </summary>
    public LogContextSnapshot WithTraceId(string traceId) => new(traceId, Identity, Client, Business);
}