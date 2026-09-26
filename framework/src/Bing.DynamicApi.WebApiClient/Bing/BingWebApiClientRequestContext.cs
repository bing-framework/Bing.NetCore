namespace Bing.DynamicApi.WebApiClient;

/// <summary>
/// 表示发送动态 API 请求时透传的上下文值。
/// </summary>
public sealed class BingWebApiClientRequestContext
{
    /// <summary>
    /// 初始化 BingWebApiClientRequestContext 类的新实例。
    /// </summary>
    /// <param name="tenantId">租户标识；null 表示未提供。</param>
    /// <param name="correlationId">关联标识；null 表示未提供。</param>
    /// <param name="language">语言；null 表示未提供。</param>
    public BingWebApiClientRequestContext(string? tenantId = null, string? correlationId = null, string? language = null)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        Language = language;
    }

    /// <summary>
    /// 获取租户标识。
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// 获取关联标识。
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// 获取语言。
    /// </summary>
    public string? Language { get; }
}

/// <summary>
/// 在每次 HTTP 请求发送时读取当前动态 API 上下文。
/// </summary>
public interface IBingWebApiClientRequestContextAccessor
{
    /// <summary>
    /// 获取当前请求上下文。
    /// </summary>
    /// <returns>当前上下文；未提供上下文时返回 null。</returns>
    BingWebApiClientRequestContext? GetCurrentContext();
}

/// <summary>
/// 提供未配置宿主访问器时的空请求上下文。
/// </summary>
internal sealed class EmptyBingWebApiClientRequestContextAccessor : IBingWebApiClientRequestContextAccessor
{
    /// <inheritdoc />
    public BingWebApiClientRequestContext? GetCurrentContext() => null;
}
