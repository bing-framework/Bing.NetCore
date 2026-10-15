namespace Bing.Events.Cap;

/// <summary>
/// 表示从 CAP 消息头恢复的业务事件元数据。
/// </summary>
public sealed class ClusterEventMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterEventMetadata" /> 类型的实例。
    /// </summary>
    public ClusterEventMetadata(string eventId, string tenantId, string stream, string entityKey,
        long? sequence, string contractVersion, string traceId)
    {
        EventId = eventId;
        TenantId = tenantId;
        Stream = stream;
        EntityKey = entityKey;
        Sequence = sequence;
        ContractVersion = contractVersion;
        TraceId = traceId;
    }

    /// <summary>
    /// 事件标识。
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// 租户标识。
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 业务流名称。
    /// </summary>
    public string Stream { get; }

    /// <summary>
    /// 实体标识。
    /// </summary>
    public string EntityKey { get; }

    /// <summary>
    /// 连续序号。
    /// </summary>
    public long? Sequence { get; }

    /// <summary>
    /// 消息契约版本。
    /// </summary>
    public string ContractVersion { get; }

    /// <summary>
    /// 跟踪标识。
    /// </summary>
    public string TraceId { get; }
}
