namespace Bing.Events.Messages;

/// <summary>
/// 提供集群消费所需的可选事件元数据。
/// </summary>
public interface IClusterEventMetadata : IMessageEvent
{
    /// <summary>
    /// 租户标识。
    /// </summary>
    string TenantId { get; set; }

    /// <summary>
    /// 业务流名称。
    /// </summary>
    string Stream { get; set; }

    /// <summary>
    /// 实体标识。
    /// </summary>
    string EntityKey { get; set; }

    /// <summary>
    /// 业务流中的连续序号。
    /// </summary>
    long? Sequence { get; set; }

    /// <summary>
    /// 消息契约版本。
    /// </summary>
    string ContractVersion { get; set; }

    /// <summary>
    /// 跟踪标识。
    /// </summary>
    string TraceId { get; set; }
}
