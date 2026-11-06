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
    /// <param name="tenantId">租户标识。</param>
    /// <param name="auditId">当前审计记录的标识。</param>
    /// <param name="applicationName">写入审计记录的应用程序名称。</param>
    /// <param name="userId">用户标识。</param>
    /// <param name="executionTimeUtc">本次操作的执行时间（UTC）。</param>
    /// <param name="executionDuration">本次操作的执行时长，单位为毫秒。</param>
    /// <param name="httpMethod">本次 HTTP 请求使用的方法。</param>
    /// <param name="httpStatusCode">本次 HTTP 响应的状态码。</param>
    /// <param name="correlationId">用于关联同一操作链路的标识。</param>
    /// <param name="isCritical">指示本次审计操作是否为关键操作。</param>
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
