namespace Bing.Auditing.Persistence;

/// <summary>
/// 表示审计日志的安全元数据投影。
/// </summary>
/// <remarks>该类型不包含审计明细 JSON；需要展示明细时应由宿主依据授权另行设计。</remarks>
public sealed class AuditLogEntry
{
    /// <summary>
    /// 初始化一个 <see cref="AuditLogEntry" /> 类型的实例。
    /// </summary>
    internal AuditLogEntry(string tenantId, string auditId, string applicationName, string userId,
        DateTime executionTimeUtc, int executionDuration, string httpMethod, int? httpStatusCode,
        string correlationId, bool isCritical)
    {
        TenantId = tenantId;
        AuditId = auditId;
        ApplicationName = applicationName;
        UserId = userId;
        ExecutionTimeUtc = executionTimeUtc;
        ExecutionDuration = executionDuration;
        HttpMethod = httpMethod;
        HttpStatusCode = httpStatusCode;
        CorrelationId = correlationId;
        IsCritical = isCritical;
    }

    /// <summary>
    /// 获取租户标识。
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 获取审计标识。
    /// </summary>
    public string AuditId { get; }

    /// <summary>
    /// 获取应用程序名称。
    /// </summary>
    public string ApplicationName { get; }

    /// <summary>
    /// 获取用户标识。
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// 获取 UTC 执行时间。
    /// </summary>
    public DateTime ExecutionTimeUtc { get; }

    /// <summary>
    /// 获取执行时长。
    /// </summary>
    public int ExecutionDuration { get; }

    /// <summary>
    /// 获取 HTTP 方法。
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// 获取 HTTP 状态码。
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// 获取关联标识。
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// 获取是否为关键审计。
    /// </summary>
    public bool IsCritical { get; }
}
