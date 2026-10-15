using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bing.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.RateLimiting;

/// <summary>
/// 按请求身份和路由实施立即拒绝的令牌桶限流。
/// </summary>
public sealed class BingRateLimitMiddleware
{
    /// <summary>
    /// 限流键允许配置的最小字符长度。
    /// </summary>
    private const int MinimumMaxKeyLength = 32;

    /// <summary>
    /// 超长限流键摘要结果使用的固定前缀。
    /// </summary>
    private const string HashKeyPrefix = "sha256:";

    /// <summary>
    /// 后续请求处理器。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 分桶规则。
    /// </summary>
    private readonly BingRateLimitOptions _options;

    /// <summary>
    /// 限流故障日志。
    /// </summary>
    private readonly ILogger<BingRateLimitMiddleware> _logger;

    /// <summary>
    /// 初始化一个 <see cref="BingRateLimitMiddleware" /> 类型的实例。
    /// </summary>
    /// <param name="next">后续请求处理器。</param>
    /// <param name="options">限流分桶配置。</param>
    /// <param name="logger">可选的限流故障日志。</param>
    /// <exception cref="ArgumentNullException">后续请求处理器或限流分桶配置为空。</exception>
    /// <exception cref="ArgumentException">限流策略名称为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">限流键最大长度小于 32 个字符。</exception>
    public BingRateLimitMiddleware(RequestDelegate next, IOptions<BingRateLimitOptions> options,
        ILogger<BingRateLimitMiddleware> logger = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.PolicyName))
            throw new ArgumentException("限流策略名称不能为空。", nameof(options));
        if (_options.MaxKeyLength < MinimumMaxKeyLength)
            throw new ArgumentOutOfRangeException(nameof(options),
                $"限流键长度必须至少为 {MinimumMaxKeyLength} 个字符。");
        _logger = logger;
    }

    /// <summary>
    /// 处理一个 HTTP 请求。
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="limiter">执行令牌桶判定的限流器。</param>
    public async Task InvokeAsync(HttpContext context, ITokenBucketRateLimiter limiter)
    {
        var subject = _options.KeySelector?.Invoke(context);
        if (string.IsNullOrWhiteSpace(subject))
        {
            var authenticatedIdentity = GetFirstAuthenticatedIdentity(context.User);
            var userId = authenticatedIdentity != null && !string.IsNullOrWhiteSpace(_options.UserIdClaimType)
                ? authenticatedIdentity.FindFirst(_options.UserIdClaimType)?.Value
                : null;
            var tenantId = authenticatedIdentity != null && _options.IncludeTenantInKey && !string.IsNullOrWhiteSpace(_options.TenantClaimType)
                ? authenticatedIdentity.FindFirst(_options.TenantClaimType)?.Value
                : null;
            var identity = string.IsNullOrWhiteSpace(userId)
                ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
                : $"user:{userId}";
            subject = string.IsNullOrWhiteSpace(tenantId)
                ? identity
                : $"tenant:{EncodeIdentityPart(tenantId)}|{EncodeIdentityPart(identity)}";
        }

        var route = _options.RouteKeySelector?.Invoke(context);
        if (string.IsNullOrWhiteSpace(route))
        {
            var endpoint = context.GetEndpoint();
            route = (endpoint as RouteEndpoint)?.RoutePattern.RawText
                ?? endpoint?.DisplayName
                ?? "unmatched";
        }
        RateLimitResult result;
        try
        {
            result = await limiter.TryAcquireAsync(CreateBucketKey(_options.PolicyName, route, subject),
                cancellationToken: context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "限流存储不可用，策略 {PolicyName} 无法完成请求判定。", _options.PolicyName);
            if (_options.FailOpenOnInfrastructureError)
            {
                await _next(context);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        if (result.IsAllowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var seconds = Math.Ceiling(result.RetryAfter.TotalSeconds);
        context.Response.Headers["Retry-After"] = Math.Max(1, Math.Min(seconds, int.MaxValue)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 按声明主体顺序获取第一个已认证身份。
    /// </summary>
    /// <param name="principal">当前请求的声明主体。</param>
    /// <returns>第一个已认证身份；不存在时返回 null。</returns>
    private static ClaimsIdentity GetFirstAuthenticatedIdentity(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
        {
            if (identity.IsAuthenticated)
                return identity;
        }

        return null;
    }

    /// <summary>
    /// 为身份片段添加长度前缀，避免内部字符造成边界歧义。
    /// </summary>
    /// <param name="value">需要编码的身份片段。</param>
    /// <returns>包含字符长度和原值的身份片段。</returns>
    private static string EncodeIdentityPart(string value) =>
        string.Concat(value.Length.ToString(CultureInfo.InvariantCulture), ":", value);

    /// <summary>
    /// 生成无分隔符歧义的限流键。
    /// </summary>
    /// <param name="policy">限流策略名称。</param>
    /// <param name="route">稳定路由键。</param>
    /// <param name="subject">请求主体键。</param>
    /// <returns>未超过配置长度的结构化键或固定长度摘要键。</returns>
    private string CreateBucketKey(string policy, string route, string subject)
    {
        var key = string.Concat(policy.Length.ToString(CultureInfo.InvariantCulture), ":", policy,
            route.Length.ToString(CultureInfo.InvariantCulture), ":", route,
            subject.Length.ToString(CultureInfo.InvariantCulture), ":", subject);
        if (key.Length <= _options.MaxKeyLength)
            return key;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var hashText = Convert.ToHexString(hash).ToLowerInvariant();
        var hashLength = Math.Min(hashText.Length, _options.MaxKeyLength - HashKeyPrefix.Length);
        return HashKeyPrefix + hashText[..hashLength];
    }
}
