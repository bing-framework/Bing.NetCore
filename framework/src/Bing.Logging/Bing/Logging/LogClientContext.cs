namespace Bing.Logging;

/// <summary>
/// 日志客户端上下文
/// </summary>
public sealed class LogClientContext
{
    /// <summary>
    /// 初始化一个<see cref="LogClientContext"/>类型的实例
    /// </summary>
    public LogClientContext(
        string application = null,
        string environment = null,
        string ip = null,
        string host = null,
        string browser = null,
        string url = null,
        bool isWebEnvironment = false)
    {
        Application = application;
        Environment = environment;
        Ip = ip;
        Host = host;
        Browser = browser;
        Url = url;
        IsWebEnvironment = isWebEnvironment;
    }

    /// <summary>
    /// 应用程序
    /// </summary>
    public string Application { get; }

    /// <summary>
    /// 执行环境
    /// </summary>
    public string Environment { get; }

    /// <summary>
    /// IP地址
    /// </summary>
    public string Ip { get; }

    /// <summary>
    /// 主机
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// 浏览器
    /// </summary>
    public string Browser { get; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// 是否Web环境
    /// </summary>
    public bool IsWebEnvironment { get; }
}