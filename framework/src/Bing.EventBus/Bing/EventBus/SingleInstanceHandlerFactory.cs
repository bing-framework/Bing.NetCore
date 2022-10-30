using System.Collections.Generic;
using System.Linq;

namespace Bing.EventBus;

/// <summary>
/// 单例事件处理器工厂
/// </summary>
public class SingleInstanceHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 事件处理器实例
    /// </summary>
    public IEventHandler HandlerInstance { get; }

    /// <summary>
    /// 初始化一个<see cref="SingleInstanceHandlerFactory"/>类型的实例
    /// </summary>
    /// <param name="handler">事件处理器实例</param>
    public SingleInstanceHandlerFactory(IEventHandler handler)
    {
        HandlerInstance = handler;
    }

    /// <summary>
    /// 获取事件处理器
    /// </summary>
    public IEventHandlerDisposeWrapper GetHandler() => new EventHandlerDisposeWrapper(HandlerInstance);

    /// <summary>
    /// 是否在当前工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<SingleInstanceHandlerFactory>()
            .Any(t => t.HandlerInstance == HandlerInstance);
    }
}