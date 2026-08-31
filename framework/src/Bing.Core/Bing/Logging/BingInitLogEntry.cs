using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 表示框架初始化阶段采集的一条日志信息。
/// </summary>
public class BingInitLogEntry
{
    /// <summary>
    /// 获取或设置日志级别。
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 获取或设置日志事件标识。
    /// </summary>
    public EventId EventId { get; set; }

    /// <summary>
    /// 获取或设置日志状态对象，由格式化函数解释其内容。
    /// </summary>
    public object State { get; set; } = default!;

    /// <summary>
    /// 获取或设置关联异常；没有异常时为空。
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// 获取或设置根据状态对象和异常生成日志消息的函数。
    /// </summary>
    public Func<object, Exception, string> Formatter { get; set; } = default!;

    /// <summary>
    /// 根据 <see cref="State"/>、<see cref="Exception"/> 和 <see cref="Formatter"/> 生成日志消息。
    /// </summary>
    public string Message => Formatter(State, Exception);

}
