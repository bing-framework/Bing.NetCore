using Bing.Extensions;
using Bing.Utils.Json;

namespace Bing.Events;

/// <summary>
/// 提供默认标识、发生时间和名称解析行为的事件基类。
/// </summary>
public class Event : IEvent
{
    /// <summary>
    /// 保存构造时指定的显式事件名称。
    /// </summary>
    private readonly string _eventName;

    /// <summary>
    /// 使用可选的显式事件名称初始化 <see cref="Event"/> 的实例。
    /// </summary>
    /// <param name="eventName">优先于事件类型特性解析结果使用的可选事件名称。</param>
    /// <remarks>构造时生成新的 GUID 标识，并将发生时间设置为当前本地时间。</remarks>
    public Event(string eventName = default)
    {
        Id =  Guid.NewGuid().ToString();
        Time = DateTime.Now;
        _eventName = eventName;
    }

    /// <inheritdoc />
    public string Id { get; set; }

    /// <inheritdoc />
    public DateTime Time { get; }

    /// <inheritdoc />
    /// <remarks>优先返回构造时指定的名称；否则普通类型使用 <see cref="EventNameAttribute"/>，泛型类型使用 <see cref="GenericEventNameAttribute"/> 解析。</remarks>
    public virtual string GetEventName()
    {
        var eventName = _eventName;
        if (eventName.IsEmpty())
        {
            var eventType = base.GetType();
            if (!eventType.IsGenericType)
                eventName = EventNameAttribute.GetNameOrDefault(eventType);
            else
            {
                var eventNameAttribute = GetType().GetAttribute<GenericEventNameAttribute>();
                eventName = eventNameAttribute.GetName(eventType);
            }
        }
        return eventName;
    }

    /// <summary>
    /// 返回包含事件标识、发生时间和 JSON 数据的多行文本。
    /// </summary>
    /// <returns>当前事件的诊断文本表示。</returns>
    public override string ToString()
    {
        var result = new StringBuilder();
        result.AppendLine($"事件标识: {Id}");
        result.AppendLine($"事件时间: {Time:yyyy-MM-dd HH:mm:ss.fff}");
        result.AppendLine($"事件数据: {(this).ToJson()}");
        return result.ToString();
    }
}
