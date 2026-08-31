using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>
/// 创建绑定查询执行运行时的查询描述。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlQueryRuntimeFactory
{
    /// <summary>创建结果类型由终结方法决定的结构化查询描述。</summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">结构化查询使用的 SQL Builder。</param>
    /// <returns>结构化查询描述。</returns>
    public static SqlFluentQuery CreateQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

    /// <summary>
    /// 创建结果类型由终结方法决定的原生 SQL 文本查询描述。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="commandText">要执行的 SQL 文本。</param>
    /// <param name="parameters">SQL 查询参数。</param>
    /// <returns>原生 SQL 文本查询描述。</returns>
    public static SqlTextQuery CreateTextQuery(ISqlQueryPlanExecutor executor, string commandText, object parameters) =>
        new(executor, commandText, parameters);

    /// <summary>创建结果类型由终结方法决定的存储过程查询描述。</summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="procedure">要执行的存储过程名称。</param>
    /// <param name="parameters">存储过程参数。</param>
    /// <returns>存储过程查询描述。</returns>
    public static SqlProcedureQuery CreateProcedureQuery(ISqlQueryPlanExecutor executor, string procedure,
        object parameters) => new(executor, procedure, parameters);

    /// <summary>
    /// 创建唯一的非泛型 Lambda 查询描述。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">Lambda 查询使用的 SQL Builder。</param>
    /// <returns>非泛型 Lambda 查询描述。</returns>
    public static SqlLambdaQuery CreateLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

}