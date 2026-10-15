namespace Bing.Domain.Entities.Events;

/// <summary>
/// 提供待分发领域事件的读取与移除能力。
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// 获取当前记录的领域事件。
    /// </summary>
    /// <returns>领域事件的只读集合；尚未添加事件时返回 <c>null</c>。</returns>
    IReadOnlyCollection<DomainEvent> GetDomainEvents();

    /// <summary>
    /// 移除指定的领域事件。
    /// </summary>
    /// <param name="event">要移除的领域事件。</param>
    void RemoveDomainEvent(DomainEvent @event);

    /// <summary>
    /// 清空当前记录的领域事件。
    /// </summary>
    void ClearDomainEvents();
}
