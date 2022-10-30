using System;
using System.Text;
using Bing.Utils.Json;

namespace Bing.EventBus;

/// <summary>
/// 消息事件
/// </summary>
public class MessageEvent : IMessageEvent
{
    /// <summary>
    /// 初始化一个<see cref="MessageEvent"/>类型的实例
    /// </summary>
    public MessageEvent()
    {
        Id = Helpers.Id.Guid();
        Time = DateTime.Now;
    }

    /// <inheritdoc />
    public string Id { get; set; }

    /// <inheritdoc />
    public DateTime Time { get; set; }

    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public object Data { get; set; }

    /// <inheritdoc />
    public string Callback { get; set; }

    /// <inheritdoc />
    public bool Send { get; set; }

    /// <summary>
    /// 输出日志
    /// </summary>
    public override string ToString()
    {
        var result = new StringBuilder();
        result.AppendLine($"事件标识:{Id},");
        result.AppendLine($"事件时间:{Time:yyyy-MM-dd HH:mm:ss.fff},");
        if (string.IsNullOrWhiteSpace(Name) == false)
            result.Append($"消息名称:{Name},");
        if (string.IsNullOrWhiteSpace(Callback) == false)
            result.AppendLine($"回调名称:{Callback}");
        result.Append($"事件数据: {(Data).ToJson()}");
        return result.ToString();
    }
}