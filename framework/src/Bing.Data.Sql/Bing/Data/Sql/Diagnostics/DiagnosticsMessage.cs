using Bing.Data.Enums;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// 诊断消息
/// </summary>
public class DiagnosticsMessage
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
    /// Sql参数
    /// </summary>
    public object Parameters { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 执行前消息
/// </summary>
public class SqlQueryDiagnosticBeforeMessage
{
    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// Json参数
    /// </summary>
    public string ParameterJson { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据源
    /// </summary>
    public string DataSource { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteBefore { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 执行后消息
/// </summary>
public class SqlQueryDiagnosticAfterMessage
{
    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// Json参数
    /// </summary>
    public string ParameterJson { get; set; }

    /// <summary>
    /// 数据源
    /// </summary>
    public string DataSource { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteAfter { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 异常消息
/// </summary>
public class SqlQueryDiagnosticErrorMessage
{
    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteError { get; set; }
}
