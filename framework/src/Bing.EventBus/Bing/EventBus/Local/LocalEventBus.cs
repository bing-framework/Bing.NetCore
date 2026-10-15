using System.Collections.Concurrent;
using Bing.Helpers;
using Bing.Threading;
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
    /// 保存当前异步事件发布链的深度。
    /// </summary>
    private readonly AsyncLocal<int> _publishDepth = new AsyncLocal<int>();

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
    /// 保护事件类型字典与列表的增删操作。
    /// </summary>
    private readonly object _handlerFactoriesGate = new object();

    /// <summary>
    /// 初始化一个 <see cref="LocalEventBus" /> 类型的实例。
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
    /// <returns>用于取消本次订阅的释放句柄。</returns>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        lock (_handlerFactoriesGate)
        {
            var factories = GetOrCreateHandlerFactories(eventType);
            factories.LockAndRun(items =>
            {
                if (!factory.IsInFactories(items))
                    items.Add(factory);
            });
        }
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

        lock (_handlerFactoriesGate)
        {
            var eventType = typeof(TEvent);
            var factories = GetOrCreateHandlerFactories(eventType);
            factories.LockAndRun(items =>
            {
                items.RemoveAll(factory =>
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
            RemoveEmptyHandlerFactories(eventType, factories);
        }
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        lock (_handlerFactoriesGate)
        {
            var factories = GetOrCreateHandlerFactories(eventType);
            factories.LockAndRun(items =>
            {
                items.RemoveAll(factory =>
                    factory is SingleInstanceHandlerFactory &&
                    (factory as SingleInstanceHandlerFactory).HandlerInstance == handler);
            });
            RemoveEmptyHandlerFactories(eventType, factories);
        }
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        lock (_handlerFactoriesGate)
        {
            var factories = GetOrCreateHandlerFactories(eventType);
            factories.LockAndRun(items => items.Remove(factory));
            RemoveEmptyHandlerFactories(eventType, factories);
        }
    }

    /// <summary>
    /// 取消全部订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType)
    {
        lock (_handlerFactoriesGate)
        {
            var factories = GetOrCreateHandlerFactories(eventType);
            factories.LockAndRun(items => items.Clear());
            RemoveEmptyHandlerFactories(eventType, factories);
        }
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">本地事件处理器</param>
    /// <returns>用于取消本次订阅的释放句柄。</returns>
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
        if (localEventMessage == null)
            throw new ArgumentNullException(nameof(localEventMessage));
        if (Options.MaxPublishDepth <= 0)
            throw new InvalidOperationException("本地事件总线的最大发布深度必须大于零。");

        var previousDepth = _publishDepth.Value;
        var currentDepth = checked(previousDepth + 1);
        if (currentDepth > Options.MaxPublishDepth)
        {
            throw new InvalidOperationException(
                $"本地事件发布深度超过上限 {Options.MaxPublishDepth}：{localEventMessage.EventType.FullName}。");
        }

        _publishDepth.Value = currentDepth;
        try
        {
            await TriggerHandlersAsync(localEventMessage.EventType, localEventMessage.EventData);
        }
        finally
        {
            _publishDepth.Value = previousDepth;
        }
    }

    /// <summary>
    /// 获取可处理指定事件类型的事件处理器工厂列表快照。
    /// </summary>
    /// <param name="eventType">要发布的事件类型。</param>
    /// <returns>匹配目标事件类型或可赋值基类型/接口订阅的工厂列表快照。</returns>
    /// <remarks>订阅列表在同一同步区域复制，当前分发不会受后续订阅变更影响。</remarks>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<EventTypeWithEventHandlerFactories>();
        lock (_handlerFactoriesGate)
        {
            foreach (var handlerFactory in HandlerFactories.Where(hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
            {
                List<IEventHandlerFactory> snapshot = null;
                handlerFactory.Value.LockAndRun(factories => snapshot = factories.ToList());
                handlerFactoryList.Add(new EventTypeWithEventHandlerFactories(handlerFactory.Key, snapshot));
            }
        }
        return handlerFactoryList.ToArray();
    }

    /// <summary>
    /// 获取或创建事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>指定事件类型对应的事件处理器工厂列表。</returns>
    private List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(eventType, (type) => new List<IEventHandlerFactory>());
    }

    /// <summary>
    /// 删除不再包含订阅的事件类型列表。
    /// </summary>
    /// <param name="eventType">事件类型。</param>
    /// <param name="factories">待检查的工厂列表。</param>
    private void RemoveEmptyHandlerFactories(Type eventType, List<IEventHandlerFactory> factories)
    {
        if (factories.Count != 0 || !HandlerFactories.TryGetValue(eventType, out var current) ||
            !ReferenceEquals(current, factories))
            return;
        HandlerFactories.TryRemove(eventType, out _);
    }

    /// <summary>
    /// 触发事件类型是否为处理器事件类型
    /// </summary>
    /// <param name="targetEventType">目标事件类型</param>
    /// <param name="handlerEventType">处理器事件类型</param>
    /// <returns>处理器事件类型可处理目标事件类型时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private static bool ShouldTriggerEventForHandler(Type targetEventType, Type handlerEventType)
    {
        if (handlerEventType == targetEventType)
            return true;
        if (handlerEventType.IsAssignableFrom(targetEventType))
            return true;
        return false;
    }
}
