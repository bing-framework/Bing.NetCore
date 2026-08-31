using Bing.Exceptions;
using Bing.Extensions;

namespace Bing.Events;

/// <summary>
/// 根据单个泛型参数生成事件逻辑名称的特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class GenericEventNameAttribute : Attribute, IEventNameProvider
{
    /// <summary>
    /// 获取或设置添加到泛型参数事件名称前的可选前缀。
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// 获取或设置添加到泛型参数事件名称后的可选后缀。
    /// </summary>
    public string Postfix { get; set; }

    /// <inheritdoc />
    /// <exception cref="Warning">事件类型不是泛型类型或泛型参数数量不为一个时抛出。</exception>
    /// <remarks>先解析唯一泛型参数的事件名称，再依次拼接 <see cref="Prefix"/> 和 <see cref="Postfix"/>。</remarks>
    public virtual string GetName(Type eventType)
    {
        if (!eventType.IsGenericType)
            throw new Warning($"给定类型不是泛型: {eventType.AssemblyQualifiedName}");
        var genericArguments = eventType.GetGenericArguments();
        if (genericArguments.Length > 1)
            throw new Warning($"给定类型具有多个泛型参数: {eventType.AssemblyQualifiedName}");
        var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);
        if (!Prefix.IsEmpty())
            eventName = $"{Prefix}{eventName}";
        if (!Postfix.IsEmpty())
            eventName = $"{eventName}{Postfix}";
        return eventName;
    }
}
