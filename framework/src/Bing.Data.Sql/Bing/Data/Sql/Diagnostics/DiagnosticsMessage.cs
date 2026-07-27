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
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString();

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
