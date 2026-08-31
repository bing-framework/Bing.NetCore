namespace Bing.Events;

/// <summary>
/// 定义根据事件 CLR 类型解析逻辑名称的提供程序。
/// </summary>
public interface IEventNameProvider
{
    /// <summary>
    /// 获取指定事件类型的逻辑名称。
    /// </summary>
    /// <param name="eventType">要解析名称的事件 CLR 类型。</param>
    /// <returns>用于事件发布或路由的逻辑名称。</returns>
    string GetName(Type eventType);
}
