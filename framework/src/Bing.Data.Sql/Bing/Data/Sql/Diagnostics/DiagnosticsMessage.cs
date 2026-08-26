namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// 诊断消息
/// </summary>
public sealed class DiagnosticsMessage
{
    /// <summary>
    /// 当前时间戳
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 操作
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// 查询上下文标识，同一个查询实例在多次执行中保持不变。
    /// </summary>
    public string QueryContextId { get; set; }

    /// <summary>
    /// 父查询上下文标识，用于派生查询或重试链路。
    /// </summary>
    public string ParentQueryContextId { get; set; }

    /// <summary>
    /// 执行标识，同一次执行的 Before、After、Error 消息保持一致。
    /// </summary>
    public string ExecutionId { get; set; }

    /// <summary>
    /// 当前执行阶段，例如 Data 或 Count。
    /// </summary>
    public string Phase { get; set; }

    /// <summary>
    /// Activity 跟踪标识。
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Activity Span 标识。
    /// </summary>
    public string SpanId { get; set; }

    /// <summary>
    /// Core 关联标识回退值。
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 映射配置名称。
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 租户标识，仅在调用方显式启用诊断租户输出时提供。
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// SQL 参数诊断快照
    /// </summary>
    public SqlParameterDiagnosticSnapshot Parameters { get; set; }

    /// <summary>
    /// SQL 连接诊断信息
    /// </summary>
    public SqlConnectionDiagnosticInfo Connection { get; set; }

    /// <summary>
    /// SQL 事务诊断信息
    /// </summary>
    public SqlTransactionDiagnosticInfo Transaction { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }
}
