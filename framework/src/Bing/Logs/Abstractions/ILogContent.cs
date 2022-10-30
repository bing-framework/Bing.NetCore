namespace Bing.Logs.Abstractions;

/// <summary>
/// 日志内容
/// </summary>
public interface ILogContent
{
    /// <summary>
    /// 日志名称
    /// </summary>
    string LogName { get; set; }

    /// <summary>
    /// 日志级别
    /// </summary>
    string Level { get; set; }

    /// <summary>
    /// 日志标识
    /// </summary>
    string LogId { get; set; }

    /// <summary>
    /// 跟踪号
    /// </summary>
    string TraceId { get; set; }

    /// <summary>
    /// 操作时间
    /// </summary>
    string OperationTime { get; set; }

    /// <summary>
    /// 持续时间
    /// </summary>
    string Duration { get; set; }

    /// <summary>
    /// IP
    /// </summary>
    string Ip { get; set; }

    /// <summary>
    /// 主机
    /// </summary>
    string Host { get; set; }

    /// <summary>
    /// 线程号
    /// </summary>
    string ThreadId { get; set; }

    /// <summary>
    /// 浏览器
    /// </summary>
    string Browser { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    string Url { get; set; }

    /// <summary>
    /// 操作人编号
    /// </summary>
    string UserId { get; set; }

    /// <summary>
    /// 内容
    /// </summary>
    StringBuilder Content { get; set; }

    /// <summary>
    /// 异常消息
    /// </summary>
    Exception Exception { get; set; }

    /// <summary>
    /// 标签列表
    /// </summary>
    List<string> Tags { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    IDictionary<string, object> ExtraProperties { get; set; }
}
