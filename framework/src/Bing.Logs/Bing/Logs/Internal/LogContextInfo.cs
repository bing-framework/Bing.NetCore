using System.Diagnostics;

namespace Bing.Logs.Internal;

/// <summary>
/// 日志上下文信息
/// </summary>
public class LogContextInfo
{
    #region 属性

    /// <summary>
    /// 序号
    /// </summary>
    private int _orderId;

    /// <summary>
    /// 跟踪号
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 计时器
    /// </summary>
    public Stopwatch Stopwatch { get; set; }

    /// <summary>
    /// IP
    /// </summary>
    public string Ip { get; set; }

    /// <summary>
    /// 主机
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 浏览器
    /// </summary>
    public string Browser { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// 是否Web环境
    /// </summary>
    public bool IsWebEnv { get; set; }

    /// <summary>
    /// 当前日志上下文信息
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly AsyncLocal<LogContextInfo> _current = new AsyncLocal<LogContextInfo>();

    /// <summary>
    /// 当前日志上下文信息
    /// </summary>
    public static LogContextInfo Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    #endregion

    /// <summary>
    /// 初始化一个<see cref="LogContextInfo"/>类型的实例
    /// </summary>
    public LogContextInfo() => _orderId = 0;

    /// <summary>
    /// 获取序号
    /// </summary>
    public int GetOrderId() => _orderId++;

    /// <summary>
    /// 获取日志标识
    /// </summary>
    public string GetLogId()
    {
        var logId= $"{TraceId}-{GetOrderId()}";
        Debug.WriteLine($"【LogContextInfo】LogId: {logId}");
        return logId;
    }
}
