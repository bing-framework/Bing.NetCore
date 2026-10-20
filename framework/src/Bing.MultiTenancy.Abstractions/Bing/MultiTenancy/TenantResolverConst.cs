namespace Bing.MultiTenancy;

/// <summary>
/// 定义跨租户解析贡献者共享的稳定常量。
/// </summary>
public class TenantResolverConst
{
    /// <summary>
    /// 默认 HTTP 请求或上下文使用的租户键名，值为 <c>x-tenant-id</c>。
    /// </summary>
    public const string DefaultTenantKey = "x-tenant-id";
}
