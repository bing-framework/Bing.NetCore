using Bing.Utils.Json;

namespace Bing.Events.Messages;

/// <summary>
/// 消息事件
/// </summary>
public class MessageEvent : Event, IMessageEvent
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public object Data { get; set; }

    /// <inheritdoc />
    public string Callback { get; set; }

    /// <inheritdoc />
    public bool Send { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 输出包含事件标识、时间、消息信息和序列化数据的日志文本。
    /// </remarks>
    public override string ToString()
    {
        var result = new StringBuilder();
        result.AppendLine($"事件标识: {Id}");
        result.AppendLine($"事件时间: {Time:yyyy-MM-dd HH:mm:ss.fff}");
        if (string.IsNullOrWhiteSpace(Name) == false)
            result.AppendLine($"消息名称: {Name}");
        if (string.IsNullOrWhiteSpace(Callback) == false)
            result.AppendLine($"回调名称: {Callback}");
        result.Append($"事件数据: {(Data).ToJson()}");
        return result.ToString();
    }
}

/// <summary>
/// 携带强类型消息数据的消息事件。
/// </summary>
/// <typeparam name="T">消息数据类型。</typeparam>
public class MessageEvent<T> : MessageEvent
{
    /// <summary>
    /// 初始化一个 <see cref="MessageEvent{T}"/> 类型的实例。
    /// </summary>
    /// <param name="data">消息负载数据。</param>
    public MessageEvent(T data)
    {
        Data = data;
    }
}
