namespace Bing.Auditing.Persistence;

/// <summary>
/// 定义租户范围内的审计日志查询条件。
/// </summary>
public sealed class AuditLogQuery
{
    /// <summary>
    /// 获取或设置租户标识。
    /// </summary>
    /// <remarks>空字符串表示非租户审计。</remarks>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置应用程序名称过滤条件。
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 获取或设置用户标识过滤条件。
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置关联标识过滤条件。
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// 获取或设置最早执行时间。
    /// </summary>
    /// <remarks>包含该时间点。</remarks>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// 获取或设置最晚执行时间。
    /// </summary>
    /// <remarks>不包含该时间点。</remarks>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// 获取或设置关键审计过滤条件。
    /// </summary>
    public bool? IsCritical { get; set; }

    /// <summary>
    /// 获取或设置要跳过的记录数。
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// 获取或设置最多返回的记录数。
    /// </summary>
    public int Limit { get; set; } = 100;
}
