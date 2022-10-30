using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus;

/// <summary>
/// 基于依赖注入的事件处理器工厂
/// </summary>
public class IocEventHandlerFactory : IEventHandlerFactory, IDisposable
{
    /// <summary>
    /// 事件处理器类型
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// 服务作用域工厂
    /// </summary>
    protected IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    /// 初始化一个<see cref="IocEventHandlerFactory"/>类型的实例
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="handlerType">事件处理器类型</param>
    public IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    {
        ScopeFactory = scopeFactory;
        HandlerType = handlerType;
    }

    /// <summary>
    /// 获取事件处理器
    /// </summary>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        return new EventHandlerDisposeWrapper(
            (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType),
            () => scope.Dispose());
    }

    /// <summary>
    /// 是否在当前工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(t => t.HandlerType == HandlerType);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
    }
}