namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// 定义 SQL 查询 <c>DiagnosticListener</c> 使用的稳定名称。
/// </summary>
public class SqlQueryDiagnosticListenerNames
{
    /// <summary>
    /// SQL 查询诊断事件名称的统一前缀，用于构造和筛选本组件发布的事件。
    /// </summary>
    public const string Prefix = "Bing.SqlQuery.";

    /// <summary>
    /// SQL 查询诊断监听器的固定名称，供诊断基础设施创建和订阅对应的监听器。
    /// </summary>
    public const string DiagnosticListenerName = "SqlQueryDiagnosticListener";

    /// <summary>
    /// SQL 命令执行前发布的诊断事件名称。
    /// </summary>
    public const string BeforeExecute = Prefix + "ExecuteBefore";

    /// <summary>
    /// SQL 命令执行完成后发布的诊断事件名称。
    /// </summary>
    public const string AfterExecute = Prefix + "ExecuteAfter";

    /// <summary>
    /// SQL 命令执行发生异常时发布的诊断事件名称。
    /// </summary>
    public const string ErrorExecute = Prefix + "ExecuteError";

    /// <summary>
    /// SQL 命令执行完成后释放数据库连接时发布的诊断事件名称。
    /// </summary>
    public const string DisposeExecute = Prefix + "ExecuteDispose";

    /// <summary>
    /// 释放数据库连接发生异常时发布的诊断事件名称。
    /// </summary>
    public const string DisposeException = Prefix + "DisposeException";
}
