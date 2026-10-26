namespace Bing.EventBus;

/// <summary>
/// 可选异步事件处理器工厂契约，用于异步分发时等待作用域资源释放。
/// </summary>
/// <remarks>
/// 旧同步工厂无需实现该接口；异步分发发现该契约时会优先使用异步获取和释放入口。
/// </remarks>
public interface IAsyncEventHandlerFactory
{
    /// <summary>
    /// 异步获取事件处理器及其资源释放包装器。
    /// </summary>
    /// <returns>等待处理器构造完成的异步获取结果。</returns>
    ValueTask<IEventHandlerDisposeWrapper> GetHandlerAsync();
}
