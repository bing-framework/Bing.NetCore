using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Helpers;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bing.EventBus.Local;

/// <summary>
/// 基于内存的本地事件总线
/// </summary>
public class LocalEventBus : EventBusBase, ILocalEventBus
{
    /// <summary>
    /// 日志组件
    /// </summary>
    public ILogger<LocalEventBus> Logger { get; set; }

    /// <summary>
    /// 本地事件总线 选项配置
    /// </summary>
    protected LocalEventBusOptions Options { get; }

    /// <summary>
    /// 事件处理器工厂列表
    /// </summary>
    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; }

    /// <summary>
    /// 初始化一个<see cref="EventBusBase"/>类型的实例
    /// </summary>
    /// <param name="options">本地事件总线 选项配置</param>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    public LocalEventBus(
        IOptions<LocalEventBusOptions> options,
        IServiceScopeFactory serviceScopeFactory)
        : base(serviceScopeFactory)
    {
        Options = options.Value;
        Logger = NullLogger<LocalEventBus>.Instance;
        HandlerFactories = new ConcurrentDictionary<Type, List<IEventHandlerFactory>>();
        SubscribeHandlers(Options.Handlers);
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).LockAndRun(factories =>
        {
            if (!factory.IsInFactories(factories))
                factories.Add(factory);
        });
        return new EventHandlerFactoryUnregistrar(this, eventType, factory);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理委托</param>
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        Check.NotNull(action, nameof(action));

        GetOrCreateHandlerFactories(typeof(TEvent))
            .LockAndRun(factories =>
            {
                factories.RemoveAll(factory =>
                {
                    var singleInstanceFactory = factory as SingleInstanceHandlerFactory;
                    if (singleInstanceFactory == null)
                        return false;
                    var actionHandler = singleInstanceFactory.HandlerInstance as ActionEventHandler<TEvent>;
                    if (actionHandler == null)
                        return false;
                    return actionHandler.Action == action;
                });
            });
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        GetOrCreateHandlerFactories(eventType)
            .LockAndRun(factories =>
            {
                factories.RemoveAll(factory =>
                    factory is SingleInstanceHandlerFactory &&
                    (factory as SingleInstanceHandlerFactory).HandlerInstance == handler);
            });
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).LockAndRun(factories => factories.Remove(factory));
    }

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType).LockAndRun(factories => factories.Clear());
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    public virtual IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }

    /// <summary>
    /// 发布到事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await PublishAsync(new LocalEventMessage(Guid.NewGuid().ToString(), eventData, eventType));
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="localEventMessage">本地事件消息</param>
    public virtual async Task PublishAsync(LocalEventMessage localEventMessage)
    {
        await TriggerHandlersAsync(localEventMessage.EventType, localEventMessage.EventData);
    }

    /// <summary>
    /// 获取事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<EventTypeWithEventHandlerFactories>();
        foreach (var handlerFactory in HandlerFactories.Where(hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
            handlerFactoryList.Add(new EventTypeWithEventHandlerFactories(handlerFactory.Key, handlerFactory.Value));
        return handlerFactoryList.ToArray();
    }

    /// <summary>
    /// 获取或创建事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    private List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(eventType, (type) => new List<IEventHandlerFactory>());
    }

    /// <summary>
    /// 触发事件类型是否为处理器事件类型
    /// </summary>
    /// <param name="targetEventType">目标事件类型</param>
    /// <param name="handlerEventType">处理器事件类型</param>
    private static bool ShouldTriggerEventForHandler(Type targetEventType, Type handlerEventType)
    {
        if (handlerEventType == targetEventType)
            return true;
        if (handlerEventType.IsAssignableFrom(targetEventType))
            return true;
        return false;
    }
}