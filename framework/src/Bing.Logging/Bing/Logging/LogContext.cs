namespace Bing.Logging;

/// <summary>
/// 日志上下文
/// </summary>
public class LogContext
{
    /// <summary>
    /// 初始化一个 <see cref="LogContext"/> 类型的实例。
    /// </summary>
    public LogContext() => Data = new Dictionary<string, object>();

    /// <summary>
    /// 获取或设置跟踪标识
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置用户标识
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置租户标识
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置应用程序
    /// </summary>
    public string Application { get; set; }

    /// <summary>
    /// 获取或设置执行环境
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// 获取或设置IP
    /// </summary>
    public string Ip { get; set; }

    /// <summary>
    /// 获取或设置主机
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 获取或设置浏览器
    /// </summary>
    public string Browser { get; set; }

    /// <summary>
    /// 获取或设置请求地址
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// 获取或设置会话标识
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// 获取或设置是否Web环境
    /// </summary>
    public bool IsWebEnv { get; set; }

    /// <summary>
    /// 获取或设置标签列表
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 获取扩展数据
    /// </summary>
    public IDictionary<string, object> Data { get; }

    /// <summary>
    /// 当前日志上下文
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly AsyncLocal<LogContext> _current = new();

    /// <summary>
    /// 获取或设置当前日志上下文
    /// </summary>
    public static LogContext Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
