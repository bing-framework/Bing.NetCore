using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.EventBus;

/// <summary>
/// 瞬时事件处理器工厂
/// </summary>
/// <typeparam name="THandler">事件处理器</typeparam>
public class TransientEventHandlerFactory<THandler> : TransientEventHandlerFactory, IEventHandlerFactory where THandler : IEventHandler, new()
{
    /// <summary>
    /// 初始化一个<see cref="TransientEventHandlerFactory"/>类型的实例
    /// </summary>
    public TransientEventHandlerFactory() : base(typeof(THandler))
    {
    }

    /// <summary>
    /// 创建事件处理器
    /// </summary>
    protected override IEventHandler CreateHandler() => new THandler();
}

/// <summary>
/// 瞬时事件处理器工厂
/// </summary>
public class TransientEventHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 事件处理器类型
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// 初始化一个<see cref="TransientEventHandlerFactory"/>类型的实例
    /// </summary>
    /// <param name="handlerType">事件处理器类型</param>
    public TransientEventHandlerFactory(Type handlerType)
    {
        HandlerType = handlerType;
    }

    /// <summary>
    /// 获取事件处理器
    /// </summary>
    public virtual IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = CreateHandler();
        return new EventHandlerDisposeWrapper(
            handler,
            () => (handler as IDisposable)?.Dispose());
    }

    /// <summary>
    /// 是否在当前工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<TransientEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    /// <summary>
    /// 创建事件处理器
    /// </summary>
    protected virtual IEventHandler CreateHandler()
    {
        return (IEventHandler)Activator.CreateInstance(HandlerType);
    }
}