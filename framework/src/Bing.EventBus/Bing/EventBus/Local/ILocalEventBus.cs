using System;

namespace Bing.EventBus.Local;

/// <summary>
/// 基于内存的本地事件总线
/// </summary>
public interface ILocalEventBus : IEventBus
{
    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class;
}