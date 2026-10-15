using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Bing.AspNetCore.RateLimiting;

/// <summary>
/// 配置 HTTP 请求使用的令牌桶分组。
/// </summary>
public sealed class BingRateLimitOptions
{
    /// <summary>
    /// 获取或设置策略名称。
    /// </summary>
    public string PolicyName { get; set; } = "default";

    /// <summary>
    /// 获取或设置自定义可信限流键选择器。
    /// </summary>
    public Func<HttpContext, string> KeySelector { get; set; }

    /// <summary>
    /// 获取或设置已认证用户的标识声明类型。
    /// </summary>
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// 获取或设置租户声明类型。
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// 获取或设置是否将租户声明加入默认分桶键。
    /// </summary>
    public bool IncludeTenantInKey { get; set; } = true;

    /// <summary>
    /// 获取或设置自定义稳定路由键选择器。
    /// </summary>
    /// <remarks>应返回不包含用户输入参数的路由模板；未设置时使用路由模板或端点名称，未匹配端点的请求共享固定分桶。</remarks>
    public Func<HttpContext, string> RouteKeySelector { get; set; }

    /// <summary>
    /// 获取或设置限流键允许的最大字符长度。
    /// </summary>
    /// <remarks>
    /// 中间件要求该值至少为 32。
    /// </remarks>
    public int MaxKeyLength { get; set; } = 256;

    /// <summary>
    /// 获取或设置限流存储故障时是否继续处理请求。
    /// </summary>
    public bool FailOpenOnInfrastructureError { get; set; }
}
