namespace Bing.AspNetCore.Logs;

/// <summary>
/// 记录 HTTP 请求和响应的日志信息。
/// </summary>
public class RequestResponseLog
{
    /// <summary>
    /// 初始化一个 <see cref="RequestResponseLog"/> 类型的实例。
    /// </summary>
    public RequestResponseLog() => LogId = Guid.NewGuid().ToString();

    /// <summary>
    /// 获取或设置日志标识。
    /// </summary>
    /// <remarks>未显式指定时，构造函数使用新生成的 GUID 字符串。</remarks>
    public string LogId { get; set; }

    /// <summary>
    /// 获取或设置记录日志的应用节点名称。
    /// </summary>
    public string Node { get; set; }

    /// <summary>
    /// 获取或设置客户端 IP 地址。
    /// </summary>
    public string ClientIp { get; set; }

    /// <summary>
    /// 获取或设置请求跟踪标识。
    /// </summary>
    /// <remarks>通常使用 HTTP 上下文提供的跟踪标识。</remarks>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置请求到达时间（UTC）。
    /// </summary>
    public DateTime? RequestDateTimeUtc { get; set; }

    /// <summary>
    /// 获取或设置操作层面的请求时间（UTC）。
    /// </summary>
    public DateTime? RequestDateTimeUtcActionLevel { get; set; }

    /// <summary>
    /// 获取或设置请求路径。
    /// </summary>
    public string RequestPath { get; set; }

    /// <summary>
    /// 获取或设置请求查询字符串。
    /// </summary>
    public string RequestQuery { get; set; }

    /// <summary>
    /// 获取或设置请求查询参数列表。
    /// </summary>
    public List<KeyValuePair<string, string>> RequestQueries { get; set; }

    /// <summary>
    /// 获取或设置 HTTP 请求方法。
    /// </summary>
    public string RequestMethod { get; set; }

    /// <summary>
    /// 获取或设置请求使用的 URI 方案。
    /// </summary>
    public string RequestScheme { get; set; }

    /// <summary>
    /// 获取或设置请求主机。
    /// </summary>
    public string RequestHost { get; set; }

    /// <summary>
    /// 获取或设置请求头集合。
    /// </summary>
    public Dictionary<string, string> RequestHeaders { get; set; }

    /// <summary>
    /// 获取或设置请求 Cookie 集合。
    /// </summary>
    public Dictionary<string, string> RequestCookies { get; set; }

    /// <summary>
    /// 获取或设置请求正文。
    /// </summary>
    public string RequestBody { get; set; }

    /// <summary>
    /// 获取或设置请求内容类型。
    /// </summary>
    public string RequestContentType { get; set; }

    /// <summary>
    /// 获取或设置响应完成时间（UTC）。
    /// </summary>
    public DateTime? ResponseDateTimeUtc { get; set; }

    /// <summary>
    /// 获取或设置操作层面的响应时间（UTC）。
    /// </summary>
    public DateTime? ResponseDateTimeUtcActionLevel { get; set; }

    /// <summary>
    /// 获取或设置响应状态。
    /// </summary>
    public string ResponseStatus { get; set; }

    /// <summary>
    /// 获取或设置响应头集合。
    /// </summary>
    public Dictionary<string, string> ResponseHeaders { get; set; }

    /// <summary>
    /// 获取或设置响应正文。
    /// </summary>
    public string ResponseBody { get; set; }

    /// <summary>
    /// 获取或设置响应内容类型。
    /// </summary>
    public string ResponseContentType { get; set; }

    /// <summary>
    /// 获取或设置操作层面是否发生异常。
    /// </summary>
    public bool? IsExceptionActionLevel { get; set; }

    /// <summary>
    /// 获取或设置异常消息。
    /// </summary>
    public string ExceptionMessage { get; set; }

    /// <summary>
    /// 获取或设置异常堆栈跟踪信息。
    /// </summary>
    public string ExceptionStackTrace { get; set; }
}
