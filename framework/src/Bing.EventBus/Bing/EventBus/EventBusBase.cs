using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bing.Collections;
using Bing.EventBus.Local;
using Bing.Exceptions;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus
{
    /// <summary>
    /// 事件总线基类
    /// </summary>
    public abstract class EventBusBase : IEventBus
    {
        /// <summary>
        /// 服务作用域工厂
        /// </summary>
        protected IServiceScopeFactory ServiceScopeFactory { get; }

        /// <summary>
        /// 初始化一个<see cref="EventBusBase"/>类型的实例
        /// </summary>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        protected EventBusBase(IServiceScopeFactory serviceScopeFactory)
        {
            ServiceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
        {
            return PublishAsync(typeof(TEvent), eventData);
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        public virtual async Task PublishAsync(Type eventType, object eventData)
        {
            await PublishToEventBusAsync(eventType, eventData);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="action">事件处理委托</param>
        public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
        {
            return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <typeparam name="THandler">事件处理器类型</typeparam>
        public virtual IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
        {
            return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件处理器</param>
        public virtual IDisposable Subscribe(Type eventType, IEventHandler handler)
        {
            return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="factory">事件处理器工厂</param>
        public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
        {
            return Subscribe(typeof(TEvent), factory);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="factory">事件处理器工厂</param>
        public abstract IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="action">事件处理委托</param>
        public abstract void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class;

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">本地事件处理器</param>
        public virtual void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
        {
            Unsubscribe(typeof(TEvent), handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件处理器</param>
        public abstract void Unsubscribe(Type eventType, IEventHandler handler);

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="factory">事件处理器工厂</param>
        public virtual void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
        {
            Unsubscribe(typeof(TEvent), factory);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="factory">事件处理器工厂</param>
        public abstract void Unsubscribe(Type eventType, IEventHandlerFactory factory);

        /// <summary>
        /// 取消全部订阅
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        public void UnsubscribeAll<TEvent>() where TEvent : class
        {
            UnsubscribeAll(typeof(TEvent));
        }

        /// <summary>
        /// 取消全部订阅
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public abstract void UnsubscribeAll(Type eventType);

        /// <summary>
        /// 发布到事件总线
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        protected abstract Task PublishToEventBusAsync(Type eventType, object eventData);

        /// <summary>
        /// 触发多个事件处理器
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        protected virtual async Task TriggerHandlersAsync(Type eventType, object eventData)
        {
            var exceptions = new List<Exception>();
            await TriggerHandlersAsync(eventType, eventData, exceptions);
            if (exceptions.Any()) 
                ThrowOriginalExceptions(eventType, exceptions);
        }

        /// <summary>
        /// 抛出原始异常
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="exceptions">异常列表</param>
        protected void ThrowOriginalExceptions(Type eventType, List<Exception> exceptions)
        {
            if (exceptions.Count == 1)
                exceptions[0].ReThrow();
            throw new AggregateException($"More than one error has occurred while triggering the event: {eventType}",
                exceptions);
        }

        /// <summary>
        /// 订阅事件处理器列表
        /// </summary>
        /// <param name="handlers">事件处理器类型列表</param>
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
        /// 触发多个事件处理器
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        /// <param name="exceptions">异常列表</param>
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
        /// 获取事件处理器工厂列表
        /// </summary>
        /// <param name="eventType">事件类型</param>
        protected abstract IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType);

        /// <summary>
        /// 触发事件处理器
        /// </summary>
        /// <param name="asyncEventHandlerFactory">事件处理器工厂</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="eventData">事件数据</param>
        /// <param name="exceptions">异常列表</param>
        protected virtual async Task TriggerHandlerAsync(IEventHandlerFactory asyncEventHandlerFactory, Type eventType, object eventData, List<Exception> exceptions)
        {
            using (var eventHandlerWrapper = asyncEventHandlerFactory.GetHandler())
            {
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
                    exceptions.Add(ex.InnerException);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        /// <summary>
        /// 事件类型 - 事件处理器工厂列表
        /// </summary>
        protected class EventTypeWithEventHandlerFactories
        {
            /// <summary>
            /// 事件类型
            /// </summary>
            public Type EventType { get; }

            /// <summary>
            /// 事件处理器工厂列表
            /// </summary>
            public List<IEventHandlerFactory> EventHandlerFactories { get; }

            /// <summary>
            /// 初始化一个<see cref="EventTypeWithEventHandlerFactories"/>类型的实例
            /// </summary>
            /// <param name="eventType">事件类型</param>
            /// <param name="eventHandlerFactories">事件处理器工厂列表</param>
            public EventTypeWithEventHandlerFactories(Type eventType, List<IEventHandlerFactory> eventHandlerFactories)
            {
                EventType = eventType;
                EventHandlerFactories = eventHandlerFactories;
            }
        }

        /// <summary>
        /// 同步上下文移除器
        /// </summary>
        /// <remarks>参考：https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/</remarks>
        protected struct SynchronizationContextRemover : INotifyCompletion
        {
            /// <summary>
            /// 是否已完成
            /// </summary>
            public bool IsCompleted
            {
                get => SynchronizationContext.Current == null;
            }

            /// <summary>
            /// 完成操作
            /// </summary>
            /// <param name="continuation">继续操作</param>
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
            /// 获取等待器
            /// </summary>
            public SynchronizationContextRemover GetAwaiter() => this;

            /// <summary>
            /// 获取结果
            /// </summary>
            public void GetResult() { }
        }
    }
}
