using System;
using System.Threading.Tasks;

namespace Bing.EventBus.Local;

/// <summary>
/// 空本地事件总线
/// </summary>
public sealed class NullLocalEventBus : ILocalEventBus
{
    /// <summary>
    /// 空本地事件总线实例
    /// </summary>
    public static NullLocalEventBus Instance { get; } = new NullLocalEventBus();

    /// <summary>
    /// 初始化一个<see cref="NullLocalEventBus"/>类型的实例
    /// </summary>
    private NullLocalEventBus() { }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class => Task.CompletedTask;

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    public Task PublishAsync(Type eventType, object eventData) => Task.CompletedTask;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理委托</param>
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class => NullDisposable.Instance;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class => NullDisposable.Instance;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型</typeparam>
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new() => NullDisposable.Instance;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public IDisposable Subscribe(Type eventType, IEventHandler handler) => NullDisposable.Instance;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class => NullDisposable.Instance;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => NullDisposable.Instance;

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理委托</param>
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
    }

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
    }

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public void UnsubscribeAll(Type eventType)
    {
    }
}