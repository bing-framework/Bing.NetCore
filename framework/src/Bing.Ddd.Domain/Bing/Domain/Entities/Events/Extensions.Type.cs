namespace Bing.Domain.Entities.Events;

/// <summary>
/// 提供领域事件处理器类型判断扩展。
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// 判断处理器类型是否声明处理指定事件。
    /// </summary>
    /// <param name="handlerType">处理器类型。</param>
    /// <param name="eventType">领域事件类型。</param>
    /// <returns>处理器的泛型事件参数与指定事件类型相同时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool CanHandle(this Type handlerType, Type eventType) => handlerType.GetGenericArguments().FirstOrDefault() == eventType;

    /// <summary>
    /// 判断指定类型是否为领域事件类型。
    /// </summary>
    /// <param name="eventType">要检查的类型。</param>
    /// <returns>继承 <see cref="DomainEvent"/> 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsEvent(this Type eventType) => typeof(DomainEvent).IsAssignableFrom(eventType);
}