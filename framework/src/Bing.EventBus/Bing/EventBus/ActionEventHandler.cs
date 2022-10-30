using System;
using System.Threading.Tasks;
using Bing.DependencyInjection;
using Bing.EventBus.Local;

namespace Bing.EventBus;

/// <summary>
/// 函数操作事件处理器
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class ActionEventHandler<TEvent> : ILocalEventHandler<TEvent>, ITransientDependency where TEvent : class
{
    /// <summary>
    /// 函数操作
    /// </summary>
    public Func<TEvent, Task> Action { get; }

    /// <summary>
    /// 初始化一个<see cref="ActionEventHandler{TEvent}"/>类型的实例
    /// </summary>
    /// <param name="handler">事件处理</param>
    public ActionEventHandler(Func<TEvent, Task> handler) => Action = handler;

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public async Task HandleAsync(TEvent eventData)
    {
        await Action(eventData);
    }
}