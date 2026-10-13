using Bing.DependencyInjection;
using Bing.Events.Handlers;

namespace Bing.Events.Default;

/// <summary>
/// 事件处理器服务
/// </summary>
public class EventHandlerManager : IEventHandlerManager
{
    /// <inheritdoc />
    public List<IEventHandler<TEvent>> GetHandlers<TEvent>() where TEvent : IEvent => ServiceLocator.Instance.GetServices<IEventHandler<TEvent>>().ToList();
}
