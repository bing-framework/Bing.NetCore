namespace Bing.Logging.Core;

/// <summary>
/// 描述单条日志事件的结构化上下文和跟踪关联信息。
/// </summary>
public class LogEventDescriptor
{
    /// <summary>
    /// 初始化 <see cref="LogEventDescriptor"/> 的实例及其空日志上下文。
    /// </summary>
    public LogEventDescriptor()
    {
        Context = new LogEventContext();
    }

    /// <summary>
    /// 获取日志事件上下文，构造后始终非 <c>null</c>。
    /// </summary>
    public LogEventContext Context { get; }

    /// <summary>
    /// 获取或设置技术调用链的跟踪标识；未关联跟踪时可以为 <c>null</c>。
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置便于诊断展示的可选跟踪名称，不能替代稳定跟踪标识。
    /// </summary>
    public string TraceName { get; set; }

    /// <summary>
    /// 获取或设置跨技术调用链关联业务操作的业务跟踪标识；未关联业务操作时可以为 <c>null</c>。
    /// </summary>
    public string BusinessTraceId { get; set; }
}
