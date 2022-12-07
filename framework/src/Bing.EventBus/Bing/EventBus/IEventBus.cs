using System;
using System.Threading.Tasks;
using Bing.EventBus.Local;

namespace Bing.EventBus;

/// <summary>
/// 事件总线
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class;

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    Task PublishAsync(Type eventType, object eventData);

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理委托</param>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型</typeparam>
    IDisposable Subscribe<TEvent, THandler>() 
        where TEvent : class
        where THandler : IEventHandler, new();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    IDisposable Subscribe(Type eventType, IEventHandler handler);

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理委托</param>
    void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class;

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class;

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    void Unsubscribe(Type eventType, IEventHandler handler);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class;

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    void Unsubscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    void UnsubscribeAll<TEvent>() where TEvent : class;

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    void UnsubscribeAll(Type eventType);
}