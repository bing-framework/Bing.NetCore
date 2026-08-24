using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划准备与跟踪
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 准备查询计划执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <returns>可供本次执行复用的准备结果。</returns>
    private PreparedQueryPlan PrepareQueryPlan(SqlQueryPlan plan)
    {
        var command = plan.IsBuilderPlan
            ? PrepareCommand(plan)
            : PrepareCommand(plan.CommandText, plan.Parameters);
        if (plan.CommandType == System.Data.CommandType.StoredProcedure)
            ConfigureOutputParameterSupport(command);
        plan.NotifyParametersBound(CreateOutputParameterAccessor(command.DapperParameters));
        return new PreparedQueryPlan(plan, command);
    }

    /// <summary>
    /// 写入查询计划的调试跟踪日志。
    /// </summary>
    /// <param name="preparedPlan">当前已准备的查询计划。</param>
    private void WritePlanTraceLog(PreparedQueryPlan preparedPlan)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        WriteTraceLog(preparedPlan.Command);
    }

    /// <summary>
    /// 查询计划单次执行的不可变准备结果。
    /// </summary>
    private sealed class PreparedQueryPlan
    {
        /// <summary>
        /// 初始化查询计划准备结果。
        /// </summary>
        /// <param name="plan">原始查询计划。</param>
        /// <param name="command">本次执行的准备命令。</param>
        public PreparedQueryPlan(SqlQueryPlan plan, SqlPreparedCommand command)
        {
            Plan = plan;
            Command = command;
        }

        /// <summary>
        /// 原始查询计划。
        /// </summary>
        public SqlQueryPlan Plan { get; }

        /// <summary>
        /// 本次执行的准备命令。
        /// </summary>
        public SqlPreparedCommand Command { get; }

        /// <summary>
        /// 最终执行 SQL。
        /// </summary>
        public string Sql => Command.Sql;

        /// <summary>
        /// Dapper 使用的绑定参数。
        /// </summary>
        public object DapperParameters => Command.DapperParameters;

        /// <summary>
        /// Trace 使用的原始参数源。
        /// </summary>
        public object ParameterSource => Command.ParameterSource;

    }
}