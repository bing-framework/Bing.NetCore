namespace Bing.Events;

/// <summary>
/// 事件
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 事件标识
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// 事件时间
    /// </summary>
    DateTime Time { get; }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    string GetEventName();
}
