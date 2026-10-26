using System.Reflection;
using System.Runtime.CompilerServices;
using Bing.Collections;
using Bing.EventBus.Local;
using Bing.Exceptions;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus;

/// <summary>
/// 事件总线的抽象基类。
/// </summary>
/// <remarks>
/// 提供事件发布、订阅登记、处理器分发和异常聚合的共用实现；具体存储与分发策略由派生总线决定。
/// </remarks>
public abstract class EventBusBase : IEventBus
{
    /// <summary>
    /// 获取用于为事件处理器创建独立依赖注入作用域的工厂。
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 初始化一个 <see cref="EventBusBase" /> 类的新实例。
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    protected EventBusBase(IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData);
    }

    /// <inheritdoc />
    public virtual async Task PublishAsync(Type eventType, object eventData)
    {
        await PublishToEventBusAsync(eventType, eventData);
    }

    /// <inheritdoc />
    public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
    }

    /// <inheritdoc />
    public virtual IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
    {
        return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
    }

    /// <inheritdoc />
    public virtual IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
    }

    /// <inheritdoc />
    public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return Subscribe(typeof(TEvent), factory);
    }

    /// <inheritdoc />
    public abstract IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);

    /// <inheritdoc />
    public abstract void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class;

    /// <inheritdoc />
    public virtual void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), handler);
    }

    /// <inheritdoc />
    public abstract void Unsubscribe(Type eventType, IEventHandler handler);

    /// <inheritdoc />
    public virtual void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        Unsubscribe(typeof(TEvent), factory);
    }

    /// <inheritdoc />
    public abstract void Unsubscribe(Type eventType, IEventHandlerFactory factory);

    /// <inheritdoc />
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
        UnsubscribeAll(typeof(TEvent));
    }

    /// <inheritdoc />
    public abstract void UnsubscribeAll(Type eventType);

    /// <summary>
    /// 将事件发布到具体事件总线。
    /// </summary>
    /// <param name="eventType">发布事件的运行时类型。</param>
    /// <param name="eventData">传递给事件处理器的事件数据。</param>
    protected abstract Task PublishToEventBusAsync(Type eventType, object eventData);

    /// <summary>
    /// 触发指定事件类型的全部处理器。
    /// </summary>
    /// <param name="eventType">发布事件的运行时类型。</param>
    /// <param name="eventData">传递给事件处理器的事件数据。</param>
    /// <exception cref="Exception">处理器抛出异常时重新抛出原异常；多个处理器失败时抛出聚合异常。</exception>
    protected virtual async Task TriggerHandlersAsync(Type eventType, object eventData)
    {
        var exceptions = new List<Exception>();
        await TriggerHandlersAsync(eventType, eventData, exceptions);
        if (exceptions.Any()) 
            ThrowOriginalExceptions(eventType, exceptions);
    }

    /// <summary>
    /// 重新抛出处理器收集的原始异常。
    /// </summary>
    /// <param name="eventType">分发失败的事件类型。</param>
    /// <param name="exceptions">处理器失败异常列表。</param>
    /// <exception cref="Exception">仅一个异常时重新抛出该原异常；多个异常时抛出聚合异常。</exception>
    protected void ThrowOriginalExceptions(Type eventType, List<Exception> exceptions)
    {
        if (exceptions.Count == 1)
            exceptions[0].ReThrow();
        throw new AggregateException($"More than one error has occurred while triggering the event: {eventType}",
            exceptions);
    }

    /// <summary>
    /// 为处理器列表中实现本地事件处理器契约的类型登记订阅。
    /// </summary>
    /// <param name="handlers">待登记的事件处理器类型列表。</param>
    protected virtual void SubscribeHandlers(ITypeList<IEventHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            var interfaces = handler.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if(!typeof(IEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                    continue;
                var genericArgs = @interface.GetGenericArguments();
                if (genericArgs.Length == 1)
                    Subscribe(genericArgs[0], new IocEventHandlerFactory(ServiceScopeFactory, handler));
            }
        }
    }

    /// <summary>
    /// 触发指定事件类型的全部处理器。
    /// </summary>
    /// <param name="eventType">发布事件的运行时类型。</param>
    /// <param name="eventData">传递给事件处理器的事件数据。</param>
    /// <param name="exceptions">收集处理器失败异常的列表。</param>
    /// <remarks>
    /// 失败异常收集到 <paramref name="exceptions" />；可继承泛型事件会同时向基类事件类型重新发布；
    /// 分发前会移除当前同步上下文。
    /// </remarks>
    protected virtual async Task TriggerHandlersAsync(Type eventType, object eventData, List<Exception> exceptions)
    {
        await new SynchronizationContextRemover();

        foreach (var handlerFactories in GetHandlerFactories(eventType))
        {
            foreach (var handlerFactory in handlerFactories.EventHandlerFactories)
            {
                await TriggerHandlerAsync(handlerFactory, eventType, eventData, exceptions);
            }
        }

        // 泛型参数事件数据处理
        if (eventType.GetTypeInfo().IsGenericType &&
            eventType.GetGenericArguments().Length == 1 &&
            typeof(IEventDataWithInheritableGenericArgument).IsAssignableFrom(eventType))
        {
            var genericArg = eventType.GetGenericArguments()[0];
            var baseArg = genericArg.GetTypeInfo().BaseType;
            if (baseArg != null)
            {
                var baseEventType = eventType.GetGenericTypeDefinition().MakeGenericType(baseArg);
                var constructorArgs = ((IEventDataWithInheritableGenericArgument)eventData).GetConstructorArgs();
                var baseEventData = Activator.CreateInstance(baseEventType, constructorArgs);
                await PublishToEventBusAsync(baseEventType, baseEventData);
            }
        }
    }

    /// <summary>
    /// 获取指定事件类型已登记的处理器工厂集合。
    /// </summary>
    /// <param name="eventType">事件类型。</param>
    /// <returns>与指定事件类型匹配的处理器工厂集合。</returns>
    protected abstract IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType);

    /// <summary>
    /// 执行指定事件处理器工厂创建的本地事件处理器。
    /// </summary>
    /// <param name="asyncEventHandlerFactory">创建事件处理器及其资源释放包装器的工厂。</param>
    /// <param name="eventType">当前发布事件的运行时类型。</param>
    /// <param name="eventData">传递给事件处理器的事件数据。</param>
    /// <param name="exceptions">收集处理器失败异常的列表。</param>
    /// <remarks>处理器包装器始终在执行后释放；反射调用的 <see cref="TargetInvocationException"/> 会记录其内部异常，其他异常直接追加到异常列表，由上层统一处理。</remarks>
    protected virtual async Task TriggerHandlerAsync(IEventHandlerFactory asyncEventHandlerFactory, Type eventType, object eventData, List<Exception> exceptions)
    {
        var asyncFactory = asyncEventHandlerFactory as IAsyncEventHandlerFactory;
        var eventHandlerWrapper = asyncFactory != null
            ? await asyncFactory.GetHandlerAsync()
            : asyncEventHandlerFactory.GetHandler();
        Exception handlerError = null;
        try
        {
            var handlerType = eventHandlerWrapper.EventHandler.GetType();
            // 本地事件处理器
            if (Types.IsGenericImplementation(handlerType, typeof(ILocalEventHandler<>)))
            {
                var method = typeof(ILocalEventHandler<>)
                    .MakeGenericType(eventType)
                    .GetMethod(nameof(ILocalEventHandler<object>.HandleAsync), new[] { eventType });
                if (method == null)
                    return;
                var result = method.Invoke(eventHandlerWrapper.EventHandler, new object[] { eventData });
                if (result == null)
                    return;
                await (Task)result;
            }
            else
            {
                throw new Warning(
                    $"The object instance is not an event handler. Object type: {handlerType.AssemblyQualifiedName}");
            }
        }
        catch (TargetInvocationException ex)
        {
            handlerError = ex.InnerException;
            exceptions.Add(handlerError);
        }
        catch (Exception ex)
        {
            handlerError = ex;
            exceptions.Add(ex);
        }
        finally
        {
            try
            {
                // 异步工厂优先等待异步释放；旧同步工厂保持原有同步释放入口。
                if (asyncFactory != null && eventHandlerWrapper is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else
                    eventHandlerWrapper.Dispose();
            }
            catch (Exception cleanupError)
            {
                if (asyncFactory == null)
                {
                    // 旧同步工厂清理失败必须立即终止分发；同一处理器已失败时保留两个异常。
                    if (handlerError != null)
                        throw new AggregateException(handlerError, cleanupError);
                    throw;
                }
                exceptions.Add(cleanupError);
            }
        }
    }

    /// <summary>
    /// 事件类型及其已登记的处理器工厂集合。
    /// </summary>
    protected class EventTypeWithEventHandlerFactories
    {
        /// <summary>
        /// 获取当前处理器工厂集合对应的事件类型。
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// 获取该事件类型已登记的处理器工厂列表。
        /// </summary>
        public List<IEventHandlerFactory> EventHandlerFactories { get; }

        /// <summary>
        /// 初始化一个 <see cref="EventTypeWithEventHandlerFactories" /> 类的新实例。
        /// </summary>
        /// <param name="eventType">事件类型。</param>
        /// <param name="eventHandlerFactories">该事件类型已登记的处理器工厂列表。</param>
        public EventTypeWithEventHandlerFactories(Type eventType, List<IEventHandlerFactory> eventHandlerFactories)
        {
            EventType = eventType;
            EventHandlerFactories = eventHandlerFactories;
        }
    }

    /// <summary>
    /// 在异步续体执行前移除当前同步上下文的等待器。
    /// </summary>
    /// <remarks>
    /// 参考：https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/
    /// </remarks>
    protected struct SynchronizationContextRemover : INotifyCompletion
    {
        /// <summary>
        /// 获取当前是否已无同步上下文，可同步继续执行。
        /// </summary>
        public bool IsCompleted
        {
            get => SynchronizationContext.Current == null;
        }

        /// <inheritdoc />
        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>
        /// 获取当前等待器实例。
        /// </summary>
        /// <returns>当前同步上下文移除器实例。</returns>
        public SynchronizationContextRemover GetAwaiter() => this;

        /// <summary>
        /// 完成等待且不产生返回值。
        /// </summary>
        public void GetResult() { }
    }
}