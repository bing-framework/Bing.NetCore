using Bing.Data.Sql.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划准备与跟踪
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 获取查询计划的 SQL 文本。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <returns>Builder 渲染或原生提供的 SQL 文本。</returns>
    private static string GetPlanSql(SqlQueryPlan plan) => plan.IsBuilderPlan ? plan.Builder.ToSql() : plan.CommandText;

    /// <summary>
    /// 准备查询计划执行所需的 SQL、参数和诊断快照。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <returns>可供本次执行复用的准备结果。</returns>
    private PreparedQueryPlan PrepareQueryPlan(SqlQueryPlan plan)
    {
        var sql = GetPlanSql(plan);
        var parameterSource = GetPlanParameterSource(plan);
        var dapperParameters = plan.IsBuilderPlan
            ? GetDbParameters(plan.Builder, sql)
            : GetDbParameters(plan.Parameters, sql);
        return new PreparedQueryPlan(plan, sql, dapperParameters, parameterSource,
            GetPreparedParameterDiagnostics(plan, dapperParameters, sql));
    }

    /// <summary>
    /// 获取诊断使用的原始参数源。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <returns>Builder 参数字典或原生参数源。</returns>
    private static object GetPlanParameterSource(SqlQueryPlan plan) => plan.IsBuilderPlan
        ? plan.Builder.GetParams()
        : plan.Parameters;

    /// <summary>
    /// 获取查询计划的增强参数诊断信息。
    /// </summary>
    /// <param name="plan">查询计划。</param>
    /// <param name="sql">当前执行的 SQL 文本。</param>
    /// <returns>参数诊断信息集合。</returns>
    private IReadOnlyCollection<SqlParameterDiagnosticInfo> GetPlanParameterDiagnostics(SqlQueryPlan plan, string sql) =>
        plan.IsBuilderPlan ? GetSqlParameterDiagnostics(plan.Builder, sql) : GetSqlParameterDiagnostics(plan.Parameters, sql);

    /// <summary>
    /// 从当前执行已绑定的参数集中获取诊断信息，避免再次解析参数源。
    /// </summary>
    /// <param name="plan">当前查询计划。</param>
    /// <param name="dapperParameters">已绑定的 Dapper 参数。</param>
    /// <param name="sql">当前执行 SQL。</param>
    /// <returns>参数诊断信息集合。</returns>
    private IReadOnlyCollection<SqlParameterDiagnosticInfo> GetPreparedParameterDiagnostics(SqlQueryPlan plan,
        object dapperParameters, string sql)
    {
        if (dapperParameters is IDapperParameterSet parameterSet)
            return parameterSet.Parameters.Select(CreateSqlParameterDiagnosticInfo).ToList();
        return GetPlanParameterDiagnostics(plan, sql);
    }

    /// <summary>
    /// 写入查询计划的调试跟踪日志。
    /// </summary>
    /// <param name="preparedPlan">当前已准备的查询计划。</param>
    private void WritePlanTraceLog(PreparedQueryPlan preparedPlan)
    {
        if (preparedPlan.Plan.IsBuilderPlan)
        {
            WriteTraceLog(preparedPlan.Plan.Builder, preparedPlan.Sql);
            return;
        }
        if (Logger.IsEnabled(LogLevel.Trace) == false || EnabledDebugSql == false)
            return;
        WriteTraceLog(preparedPlan.Sql, ToTraceParameters(preparedPlan.ParameterSource), preparedPlan.Sql);
    }

    /// <summary>
    /// 将原生参数源转换为 Trace 使用的名称和值快照。
    /// </summary>
    /// <param name="parameters">原生 SQL 参数源。</param>
    /// <returns>不共享调用方可变状态的参数快照。</returns>
    private static IReadOnlyDictionary<string, object> ToTraceParameters(object parameters)
    {
        if (parameters == null)
            return new Dictionary<string, object>();
        if (parameters is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.ToDictionary(item => item.Key, item => item.Value);
        if (parameters is IDictionary<string, object> dictionary)
            return new Dictionary<string, object>(dictionary);
        return parameters.GetType().GetProperties(System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, property => property.GetValue(parameters));
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
        /// <param name="sql">最终执行 SQL。</param>
        /// <param name="dapperParameters">Dapper 使用的绑定参数。</param>
        /// <param name="parameterSource">诊断和 Trace 使用的原始参数源。</param>
        /// <param name="parameterDiagnostics">增强参数诊断信息。</param>
        public PreparedQueryPlan(SqlQueryPlan plan, string sql, object dapperParameters, object parameterSource,
            IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterDiagnostics)
        {
            Plan = plan;
            Sql = sql;
            DapperParameters = dapperParameters;
            ParameterSource = parameterSource;
            ParameterDiagnostics = parameterDiagnostics;
        }

        /// <summary>
        /// 原始查询计划。
        /// </summary>
        public SqlQueryPlan Plan { get; }

        /// <summary>
        /// 最终执行 SQL。
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// Dapper 使用的绑定参数。
        /// </summary>
        public object DapperParameters { get; }

        /// <summary>
        /// 诊断和 Trace 使用的原始参数源。
        /// </summary>
        public object ParameterSource { get; }

        /// <summary>
        /// 增强参数诊断信息。
        /// </summary>
        public IReadOnlyCollection<SqlParameterDiagnosticInfo> ParameterDiagnostics { get; }
    }
}