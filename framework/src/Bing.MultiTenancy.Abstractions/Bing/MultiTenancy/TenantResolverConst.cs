namespace Bing.MultiTenancy;

/// <summary>
/// 定义跨租户解析贡献者共享的稳定常量。
/// </summary>
public class TenantResolverConst
{
    /// <summary>
    /// 默认 HTTP 或上下文租户键，值为 <c>x-tenant-id</c>，并由 <see cref="MultiTenancyOptions"/> 默认采用。
    /// </summary>
    public const string DefaultTenantKey = "x-tenant-id";
}
