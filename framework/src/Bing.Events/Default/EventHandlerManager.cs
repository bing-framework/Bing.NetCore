using Bing.DependencyInjection;
using Bing.Events.Handlers;

namespace Bing.Events.Default;

/// <summary>
/// 事件处理器服务
/// </summary>
public class EventHandlerManager : IEventHandlerManager
{
    /// <summary>
    /// 获取事件处理器列表
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <returns>指定事件类型对应的事件处理器列表。</returns>
    public List<IEventHandler<TEvent>> GetHandlers<TEvent>() where TEvent : IEvent => ServiceLocator.Instance.GetServices<IEventHandler<TEvent>>().ToList();
}
