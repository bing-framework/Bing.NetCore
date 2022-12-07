namespace Bing.Events;

/// <summary>
/// 事件名称提供程序
/// </summary>
public interface IEventNameProvider
{
    /// <summary>
    /// 获取名称
    /// </summary>
    /// <param name="eventType">事件类型</param>
    string GetName(Type eventType);
}
