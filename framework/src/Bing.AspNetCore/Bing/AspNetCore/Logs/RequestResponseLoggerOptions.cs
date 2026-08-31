namespace Bing.AspNetCore.Logs;

/// <summary>
/// 配置 HTTP 请求和响应日志的采集范围与格式。
/// </summary>
public class RequestResponseLoggerOptions
{
    /// <summary>
    /// 获取或设置是否启用请求响应数据采集。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置日志配置的名称，用于区分多个请求响应记录器配置。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置是否记录接口请求参数，默认值为 <c>false</c>。
    /// </summary>
    public bool WithRequest { get; set; } = false;

    /// <summary>
    /// 获取或设置是否记录接口响应参数，默认值为 <c>false</c>。
    /// </summary>
    public bool WithResponse { get; set; } = false;

    /// <summary>
    /// 获取或设置是否记录 Cookie 信息，默认值为 <c>false</c>；Cookie 可能包含敏感凭据。
    /// </summary>
    public bool WithCookie { get; set; } = false;

    /// <summary>
    /// 获取或设置是否记录请求头信息，默认值为 <c>false</c>；请求头可能包含认证令牌。
    /// </summary>
    public bool WithHeader { get; set; } = false;

    /// <summary>
    /// 获取或设置请求数据过滤规则列表；规则支持使用 <c>*</c> 进行模糊匹配。
    /// </summary>
    public List<string> RequestFilter { get; set; } = new();

    /// <summary>
    /// 获取或设置日志中日期时间值使用的格式字符串。
    /// </summary>
    public string DateTimeFormat { get; set; }
}