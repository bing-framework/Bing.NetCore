using System.Collections.Generic;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应记录器选项
/// </summary>
public class RequestResponseLoggerOptions
{
    /// <summary>
    /// 是否开启收集数据
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 是否记录接口的入参
    /// </summary>
    public bool WithRequest { get; set; } = false;

    /// <summary>
    /// 是否记录接口的出参
    /// </summary>
    public bool WithResponse { get; set; } = false;

    /// <summary>
    /// 是否记录Cookie信息
    /// </summary>
    public bool WithCookie { get; set; } = false;

    /// <summary>
    /// 是否记录请求头信息
    /// </summary>
    public bool WithHeader { get; set; } = false;

    /// <summary>
    /// 请求数据过滤，用 * 来模糊匹配
    /// </summary>
    public List<string> RequestFilter { get; set; } = new();

    /// <summary>
    /// 日期时间格式
    /// </summary>
    public string DateTimeFormat { get; set; }
}