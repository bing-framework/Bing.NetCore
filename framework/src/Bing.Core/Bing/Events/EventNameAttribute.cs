using Bing.Helpers;

namespace Bing.Events;

/// <summary>
/// 为非泛型事件类型指定固定逻辑名称的特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class EventNameAttribute : Attribute, IEventNameProvider
{
    /// <summary>
    /// 获取指定的固定事件名称。
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// 使用固定事件名称初始化 <see cref="EventNameAttribute"/> 的实例。
    /// </summary>
    /// <param name="name">不能为空的固定事件名称。</param>
    public EventNameAttribute(string name)
    {
        Check.NotNullOrEmpty(name, nameof(name));
        Name = name;
    }

    /// <inheritdoc />
    public string GetName(Type eventType) => Name;

    /// <summary>
    /// 获取指定泛型事件类型的逻辑名称。
    /// </summary>
    /// <typeparam name="TEvent">要解析名称的事件类型。</typeparam>
    /// <returns>特性声明的名称或事件类型的 CLR 全名。</returns>
    public static string GetNameOrDefault<TEvent>() => GetNameOrDefault(typeof(TEvent));

    /// <summary>
    /// 获取指定事件类型的逻辑名称。
    /// </summary>
    /// <param name="eventType">要解析名称的事件类型。</param>
    /// <returns>首个 <see cref="IEventNameProvider"/> 特性提供的名称；未声明时返回类型的 CLR 全名。</returns>
    public static string GetNameOrDefault(Type eventType)
    {
        Check.NotNull(eventType, nameof(eventType));
        return eventType.GetCustomAttributes(true)
                   .OfType<IEventNameProvider>()
                   .FirstOrDefault()
                   ?.GetName(eventType)
               ?? eventType.FullName;
    }
}
