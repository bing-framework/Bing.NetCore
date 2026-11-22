using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 查询计划的准备和跟踪支持。
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 准备查询计划执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <returns>可供本次执行复用的准备命令。</returns>
    private SqlPreparedCommand PrepareQueryPlan(SqlQueryPlan plan)
    {
        // 冻结 IR 仍使用同次渲染的参数和调试 SQL；不能因脱离 Builder 而退回原生文本路径。
        var command = plan.IsBuilderPlan || plan.IsGeneratedExecution || SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan)
            ? PrepareCommand(plan)
            : PrepareCommand(plan.CommandText, plan.Parameters);
        if (plan.CommandType == System.Data.CommandType.StoredProcedure)
            ConfigureOutputParameterSupport(command);
        plan.NotifyParametersBound(CreateOutputParameterAccessor(command.DapperParameters));
        return command;
    }

    /// <summary>
    /// 写入查询计划的调试跟踪日志。
    /// </summary>
    /// <param name="command">当前已准备的查询命令。</param>
    private void WritePlanTraceLog(SqlPreparedCommand command)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        WriteTraceLog(command);
    }
}
