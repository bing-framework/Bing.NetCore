namespace Bing.EventBus.Distributed;

/// <summary>
/// 分布式事件
/// </summary>
public interface IDistributedEvent : IEvent
{
    /// <summary>
    /// 获取事件标识
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// 获取事件发生时间
    /// </summary>
    DateTime EventTime { get; }
}
