namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// 诊断常量
/// </summary>
public class SqlQueryDiagnosticListenerNames
{
    /// <summary>
    /// 前缀
    /// </summary>
    public const string Prefix = "Bing.SqlQuery.";

    /// <summary>
    /// 监听名称
    /// </summary>
    public const string DiagnosticListenerName = "SqlQueryDiagnosticListener";

    /// <summary>
    /// 执行前
    /// </summary>
    public const string BeforeExecute = Prefix + "ExecuteBefore";

    /// <summary>
    /// 执行后
    /// </summary>
    public const string AfterExecute = Prefix + "ExecuteAfter";

    /// <summary>
    /// 执行异常
    /// </summary>
    public const string ErrorExecute = Prefix + "ExecuteError";

    /// <summary>
    /// 执行数据库连接释放
    /// </summary>
    public const string DisposeExecute = Prefix + "ExecuteDispose";

    /// <summary>
    /// 数据库连接释放异常
    /// </summary>
    public const string DisposeException = Prefix + "DisposeException";
}