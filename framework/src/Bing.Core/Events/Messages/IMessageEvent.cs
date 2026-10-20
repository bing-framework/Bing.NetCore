namespace Bing.Events.Messages;

/// <summary>
/// 消息事件
/// </summary>
public interface IMessageEvent : IEvent
{
    /// <summary>
    /// 获取或设置消息名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 获取或设置事件数据
    /// </summary>
    object Data { get; set; }

    /// <summary>
    /// 获取或设置回调名称
    /// </summary>
    string Callback { get; set; }

    /// <summary>
    /// 获取或设置是否立即发送消息
    /// </summary>
    bool Send { get; set; }
}
