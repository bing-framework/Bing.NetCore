namespace Bing.Domain.Entities.Events;

/// <summary>
/// 定义创建领域事件处理器实例的工厂契约。
/// </summary>
public interface IDomainHandlerFactory
{
    /// <summary>
    /// 根据处理器类型创建领域事件处理器实例。
    /// </summary>
    /// <param name="handlerType">要创建的领域事件处理器类型。</param>
    /// <returns>创建的处理器实例；无法解析时返回 <c>null</c>。</returns>
    object Create(Type handlerType);
}