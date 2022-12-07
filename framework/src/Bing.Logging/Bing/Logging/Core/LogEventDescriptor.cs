namespace Bing.Logging.Core;

/// <summary>
/// 日志事件描述符
/// </summary>
public class LogEventDescriptor
{
    /// <summary>
    /// 初始化一个<see cref="LogEventDescriptor"/>类型的实例
    /// </summary>
    public LogEventDescriptor()
    {
        Context = new LogEventContext();
    }

    /// <summary>
    /// 日志事件上下文
    /// </summary>
    public LogEventContext Context { get; }

    /// <summary>
    /// 日志跟踪标识
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 日志跟踪名称
    /// </summary>
    public string TraceName { get; set; }

    /// <summary>
    /// 业务跟踪标识
    /// </summary>
    public string BusinessTraceId { get; set; }
}
