namespace Bing.Events;

/// <summary>
/// 定义可发布事件的标识、发生时间和逻辑名称。
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 获取或设置事件的唯一标识。
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// 获取事件发生或创建的时间。
    /// </summary>
    DateTime Time { get; }

    /// <summary>
    /// 获取用于发布、路由或序列化的事件逻辑名称。
    /// </summary>
    /// <returns>当前事件的逻辑名称。</returns>
    string GetEventName();
}
