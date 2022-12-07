using System.Collections.Generic;

namespace Bing.EventBus;

/// <summary>
/// 事件处理器工厂
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// 获取事件处理器
    /// </summary>
    IEventHandlerDisposeWrapper GetHandler();

    /// <summary>
    /// 是否在当前工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    bool IsInFactories(List<IEventHandlerFactory> handlerFactories);
}