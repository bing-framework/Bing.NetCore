using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>
/// 创建绑定查询执行运行时的查询描述。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlQueryRuntimeFactory
{
    /// <summary>创建结果类型由终结方法决定的结构化查询描述。</summary>
    public static SqlFluentQuery CreateQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

    public static SqlFluentQuery<TResult> CreateAdvancedQuery<TResult>(ISqlQueryPlanExecutor executor,
        ISqlBuilder builder) =>
        new(executor, builder);

    /// <summary>
    /// 创建结果类型由终结方法决定的原生 SQL 文本查询描述。
    /// </summary>
    public static SqlTextQuery CreateTextQuery(ISqlQueryPlanExecutor executor, string commandText, object parameters) =>
        new(executor, commandText, parameters);

    public static SqlTextQuery<TResult> CreateAdvancedTextQuery<TResult>(ISqlQueryPlanExecutor executor,
        string commandText, object parameters) => new(executor, commandText, parameters);

    /// <summary>创建结果类型由终结方法决定的存储过程查询描述。</summary>
    public static SqlProcedureQuery CreateProcedureQuery(ISqlQueryPlanExecutor executor, string procedure,
        object parameters) => new(executor, procedure, parameters);

    public static SqlProcedureQuery<TResult> CreateAdvancedProcedureQuery<TResult>(ISqlQueryPlanExecutor executor,
        string procedure, object parameters) => new(executor, procedure, parameters);

    /// <summary>
    /// 创建唯一的非泛型 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery CreateLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

}