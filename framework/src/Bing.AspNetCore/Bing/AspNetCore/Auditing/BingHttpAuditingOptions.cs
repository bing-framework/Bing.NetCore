using System.Security.Claims;

namespace Bing.AspNetCore.Auditing;

/// <summary>
/// 配置 HTTP 请求审计。
/// </summary>
public sealed class BingHttpAuditingOptions
{
    /// <summary>
    /// 获取或设置是否启用 HTTP 请求审计。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 获取或设置应用程序名称。
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 获取或设置请求 URL 的最大记录长度。
    /// </summary>
    public int MaxUrlLength { get; set; } = 2048;

    /// <summary>
    /// 获取允许记录的查询参数名称。
    /// </summary>
    /// <remarks>默认不记录查询参数。只应加入已确认不包含敏感值的字段，按 OrdinalIgnoreCase 比较。</remarks>
    public ISet<string> AllowedQueryParameters { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取或设置用户标识声明类型。
    /// </summary>
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>
    /// 获取或设置用户名称声明类型。
    /// </summary>
    public string UserNameClaimType { get; set; } = ClaimTypes.Name;

    /// <summary>
    /// 获取或设置租户标识声明类型。
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// 获取或设置客户端标识声明类型。
    /// </summary>
    public string ClientIdClaimType { get; set; } = "client_id";

    /// <summary>
    /// 获取或设置是否传播审计存储失败。
    /// </summary>
    /// <remarks>默认仅记录错误；开启后仅在没有原始业务异常时传播。保存发生在业务执行后，不能撤回已发送响应或已提交事务。</remarks>
    public bool ThrowOnStoreFailure { get; set; }

    /// <summary>
    /// 获取或设置审计保存等待预算。
    /// </summary>
    /// <remarks>
    /// 超过预算只记录超时诊断；底层保存任务仍会完成后才释放审计作用域。默认值为无限等待。
    /// </remarks>
    public TimeSpan SaveTimeout { get; set; } = Timeout.InfiniteTimeSpan;
}
