namespace Bing.Events;

/// <summary>
/// 定义可附加到事件上的调用链会话标识。
/// </summary>
public interface IEventSession
{
    /// <summary>
    /// 获取或设置用于关联同一调用链事件的会话标识。
    /// </summary>
    string SessionId { get; set; }
}
