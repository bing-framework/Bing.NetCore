using System;

namespace Bing.DynamicApi.WebApiClient;

/// <summary>
/// 配置声明式动态 API 客户端的基础地址和上下文请求头。
/// </summary>
public sealed class BingWebApiClientOptions
{
    /// <summary>
    /// 获取或设置服务端地址回退值。
    /// </summary>
    /// <remarks>仅在宿主未配置 HttpHost 时使用；必须是不含查询和片段的绝对 HTTP(S) URI。</remarks>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// 获取或设置租户标识请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string? TenantHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// 获取或设置关联标识请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string? CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// 获取或设置语言请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string? LanguageHeaderName { get; set; } = "Accept-Language";

    /// <summary>
    /// 校验并规范化服务端地址。
    /// </summary>
    /// <returns>以斜杠结尾的有效 HTTP(S) 基础地址。</returns>
    /// <exception cref="InvalidOperationException">地址为空、非绝对地址或包含不支持的协议、查询、片段。</exception>
    internal Uri GetValidatedBaseAddress()
    {
        if (BaseAddress == null || !BaseAddress.IsAbsoluteUri)
            throw new InvalidOperationException("Bing WebApiClient 的 BaseAddress 必须是绝对 URI。");

        if (BaseAddress.Scheme != Uri.UriSchemeHttp && BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Bing WebApiClient 的 BaseAddress 只支持 HTTP 或 HTTPS。");

        if (!string.IsNullOrEmpty(BaseAddress.Query) || !string.IsNullOrEmpty(BaseAddress.Fragment))
            throw new InvalidOperationException("Bing WebApiClient 的 BaseAddress 不能包含查询字符串或 URI 片段。");

        var address = BaseAddress.AbsoluteUri;
        return address.EndsWith("/", StringComparison.Ordinal) ? BaseAddress : new Uri(address + "/", UriKind.Absolute);
    }

    /// <summary>
    /// 校验上下文请求头名称。
    /// </summary>
    /// <exception cref="ArgumentException">头名称为空白或包含非法字符。</exception>
    internal void ValidateHeaderNames()
    {
        ValidateHeaderName(TenantHeaderName, nameof(TenantHeaderName));
        ValidateHeaderName(CorrelationIdHeaderName, nameof(CorrelationIdHeaderName));
        ValidateHeaderName(LanguageHeaderName, nameof(LanguageHeaderName));
    }

    /// <summary>
    /// 校验单项请求头名称。
    /// </summary>
    /// <param name="headerName">请求头名称；null 表示禁用。</param>
    /// <param name="propertyName">用于错误提示的配置属性名。</param>
    private static void ValidateHeaderName(string? headerName, string propertyName)
    {
        if (headerName == null)
            return;

        if (string.IsNullOrWhiteSpace(headerName))
            throw new ArgumentException($"Bing WebApiClient 的 {propertyName} 不能为空白字符串。", propertyName);

        foreach (var character in headerName)
        {
            if (!IsHeaderNameTokenCharacter(character))
                throw new ArgumentException($"Bing WebApiClient 的 {propertyName} 不是有效的 HTTP 请求头名称。", propertyName);
        }
    }

    /// <summary>
    /// 判断字符是否符合 HTTP 头名称语法。
    /// </summary>
    /// <param name="character">待检查的字符。</param>
    /// <returns>为合法 token 字符时返回 true，否则返回 false。</returns>
    private static bool IsHeaderNameTokenCharacter(char character) =>
        character is >= '0' and <= '9'
            or >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or '!'
            or '#'
            or '$'
            or '%'
            or '&'
            or '\''
            or '*'
            or '+'
            or '-'
            or '.'
            or '^'
            or '_'
            or '`'
            or '|'
            or '~';
}
