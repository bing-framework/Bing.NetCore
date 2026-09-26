using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.DynamicApi.WebApiClient;

/// <summary>
/// 在发送请求前更新动态上下文头。
/// </summary>
internal sealed class BingWebApiClientRequestContextHandler : DelegatingHandler
{
    /// <summary>
    /// 在每次请求发送时读取当前上下文。
    /// </summary>
    private readonly IBingWebApiClientRequestContextAccessor _contextAccessor;
    /// <summary>
    /// 保存租户头名称；null 表示不管理该头。
    /// </summary>
    private readonly string? _tenantHeaderName;
    /// <summary>
    /// 保存关联头名称；null 表示不管理该头。
    /// </summary>
    private readonly string? _correlationIdHeaderName;
    /// <summary>
    /// 保存语言头名称；null 表示不管理该头。
    /// </summary>
    private readonly string? _languageHeaderName;

    /// <summary>
    /// 初始化 BingWebApiClientRequestContextHandler 类的新实例。
    /// </summary>
    /// <param name="contextAccessor">当前请求上下文访问器。</param>
    /// <param name="tenantHeaderName">租户头名称；null 表示禁用。</param>
    /// <param name="correlationIdHeaderName">关联头名称；null 表示禁用。</param>
    /// <param name="languageHeaderName">语言头名称；null 表示禁用。</param>
    public BingWebApiClientRequestContextHandler(
        IBingWebApiClientRequestContextAccessor contextAccessor,
        string? tenantHeaderName,
        string? correlationIdHeaderName,
        string? languageHeaderName)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _tenantHeaderName = tenantHeaderName;
        _correlationIdHeaderName = correlationIdHeaderName;
        _languageHeaderName = languageHeaderName;
    }

    /// <inheritdoc />
    /// <remarks>先清理已配置的上下文头，再写入当前上下文中的非空值。</remarks>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RemoveHeader(request, _tenantHeaderName);
        RemoveHeader(request, _correlationIdHeaderName);
        RemoveHeader(request, _languageHeaderName);

        var context = _contextAccessor.GetCurrentContext();
        if (context != null)
        {
            SetHeader(request, _tenantHeaderName, context.TenantId);
            SetHeader(request, _correlationIdHeaderName, context.CorrelationId);
            SetHeader(request, _languageHeaderName, context.Language);
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// 写入非空上下文头。
    /// </summary>
    /// <param name="request">当前请求。</param>
    /// <param name="name">头名称；null 或空白时不写入。</param>
    /// <param name="value">头值；null 或空白时不写入。</param>
    private static void SetHeader(HttpRequestMessage request, string? name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            return;

        request.Headers.Add(name, value);
    }

    /// <summary>
    /// 清理已配置的上下文头。
    /// </summary>
    /// <param name="request">当前请求。</param>
    /// <param name="name">头名称；null 或空白时不清理。</param>
    private static void RemoveHeader(HttpRequestMessage request, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            request.Headers.Remove(name);
    }
}
