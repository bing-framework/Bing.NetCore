namespace Bing.EventBus;

/// <summary>
/// 创建事件处理器及其生命周期释放包装器的工厂。
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// 获取事件处理器及其资源释放包装器。
    /// </summary>
    /// <returns>包含事件处理器的包装器；调用方完成处理后应释放该包装器。</returns>
    IEventHandlerDisposeWrapper GetHandler();

    /// <summary>
    /// 判断指定工厂集合中是否已存在与当前工厂等价的注册。
    /// </summary>
    /// <param name="handlerFactories">待检查的事件处理器工厂集合。</param>
    /// <returns>存在与当前工厂等价的工厂时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    bool IsInFactories(List<IEventHandlerFactory> handlerFactories);
}