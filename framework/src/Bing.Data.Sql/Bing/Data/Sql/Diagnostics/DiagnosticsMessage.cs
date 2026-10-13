namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// 诊断消息
/// </summary>
public sealed class DiagnosticsMessage
{
    /// <summary>
    /// 获取或设置当前时间戳
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 获取或设置操作
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// 获取或设置查询上下文标识，同一个查询实例在多次执行中保持不变。
    /// </summary>
    public string QueryContextId { get; set; }

    /// <summary>
    /// 获取或设置父查询上下文标识。
    /// </summary>
    /// <remarks>
    /// 用于派生查询或重试链路。
    /// </remarks>
    public string ParentQueryContextId { get; set; }

    /// <summary>
    /// 获取或设置执行标识，同一次执行的 Before、After、Error 消息保持一致。
    /// </summary>
    public string ExecutionId { get; set; }

    /// <summary>
    /// 获取或设置当前执行阶段。
    /// </summary>
    /// <remarks>
    /// 例如 Data 或 Count。
    /// </remarks>
    public string Phase { get; set; }

    /// <summary>
    /// 获取或设置Activity 跟踪标识。
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置Activity Span 标识。
    /// </summary>
    public string SpanId { get; set; }

    /// <summary>
    /// 获取或设置Core 关联标识回退值。
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// 获取或设置Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 获取或设置映射配置名称。
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 获取或设置租户标识。
    /// </summary>
    /// <remarks>
    /// 仅在调用方显式启用诊断租户输出时提供。
    /// </remarks>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置SQL 参数诊断快照
    /// </summary>
    public SqlParameterDiagnosticSnapshot Parameters { get; set; }

    /// <summary>
    /// 获取或设置SQL 连接诊断信息
    /// </summary>
    public SqlConnectionDiagnosticInfo Connection { get; set; }

    /// <summary>
    /// 获取或设置SQL 事务诊断信息
    /// </summary>
    public SqlTransactionDiagnosticInfo Transaction { get; set; }

    /// <summary>
    /// 获取或设置耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 获取或设置执行期间捕获的异常。
    /// </summary>
    /// <remarks>
    /// 执行成功时为空。
    /// </remarks>
    public Exception Exception { get; set; }
}
