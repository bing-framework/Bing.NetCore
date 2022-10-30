using System;
using System.Collections.Generic;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应日志
/// </summary>
public class RequestResponseLog
{
    /// <summary>
    /// 初始化一个<see cref="RequestResponseLog"/>类型的实例
    /// </summary>
    public RequestResponseLog() => LogId = Guid.NewGuid().ToString();

    /// <summary>
    /// 日志标识
    /// </summary>
    /// <remarks>默认：<code>Guid.NewGuid().ToString()</code></remarks>
    public string LogId { get; set; }

    /// <summary>
    /// 节点（项目名称）
    /// </summary>
    public string Node { get; set; }

    /// <summary>
    /// 客户端IP
    /// </summary>
    public string ClientIp { get; set; }

    /// <summary>
    /// 跟踪标识
    /// </summary>
    /// <remarks>默认：<code>HttpContext.TraceIdentifier</code></remarks>
    public string TraceId { get; set; }

    /// <summary>
    /// 请求时间（UTC）
    /// </summary>
    public DateTime? RequestDateTimeUtc { get; set; }

    /// <summary>
    /// 请求时间（UTC）操作层面
    /// </summary>
    public DateTime? RequestDateTimeUtcActionLevel { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string RequestPath { get; set; }

    /// <summary>
    /// 请求查询
    /// </summary>
    public string RequestQuery { get; set; }

    /// <summary>
    /// 请求查询列表
    /// </summary>
    public List<KeyValuePair<string, string>> RequestQueries { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string RequestMethod { get; set; }

    /// <summary>
    /// 请求格式
    /// </summary>
    public string RequestScheme { get; set; }

    /// <summary>
    /// 请求主机
    /// </summary>
    public string RequestHost { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> RequestHeaders { get; set; }

    /// <summary>
    /// 请求Cookie
    /// </summary>
    public Dictionary<string, string> RequestCookies { get; set; }

    /// <summary>
    /// 请求正文
    /// </summary>
    public string RequestBody { get; set; }

    /// <summary>
    /// 请求内容类型
    /// </summary>
    public string RequestContentType { get; set; }

    /// <summary>
    /// 响应时间（UTC）
    /// </summary>
    public DateTime? ResponseDateTimeUtc { get; set; }

    /// <summary>
    /// 响应时间（UTC）操作层面
    /// </summary>
    public DateTime? ResponseDateTimeUtcActionLevel { get; set; }

    /// <summary>
    /// 响应状态
    /// </summary>
    public string ResponseStatus { get; set; }

    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string> ResponseHeaders { get; set; }

    /// <summary>
    /// 响应正文
    /// </summary>
    public string ResponseBody { get; set; }

    /// <summary>
    /// 响应内容类型
    /// </summary>
    public string ResponseContentType { get; set; }

    /// <summary>
    /// 是否异常操作层面
    /// </summary>
    public bool? IsExceptionActionLevel { get; set; }

    /// <summary>
    /// 异常消息
    /// </summary>
    public string ExceptionMessage { get; set; }

    /// <summary>
    /// 异常堆栈跟踪
    /// </summary>
    public string ExceptionStackTrace { get; set; }
}