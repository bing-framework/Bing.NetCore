using System;
using System.Linq;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 类型(<see cref="Type"/>) 扩展
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// 能否处理指定事件
    /// </summary>
    /// <param name="handlerType">处理器类型</param>
    /// <param name="eventType">事件类型</param>
    public static bool CanHandle(this Type handlerType, Type eventType) => handlerType.GetGenericArguments().FirstOrDefault() == eventType;

    /// <summary>
    /// 是否领域事件
    /// </summary>
    /// <param name="eventType">领域事件类型</param>
    public static bool IsEvent(this Type eventType) => typeof(DomainEvent).IsAssignableFrom(eventType);
}