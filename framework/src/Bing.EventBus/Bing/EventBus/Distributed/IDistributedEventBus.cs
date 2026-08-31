namespace Bing.EventBus.Distributed;

/// <summary>
/// 分布式事件总线
/// </summary>
public interface IDistributedEventBus : IEventBus
{
    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">分布式事件处理器</param>
    /// <returns>用于取消本次订阅的释放句柄。</returns>
    IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class;

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在工作单元内提交</param>
    Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class;

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在工作单元内提交</param>
    Task PublishAsync(Type eventType,object eventData, bool onUnitOfWorkComplete = true);
}