namespace Bing.Data.Sql;

/// <summary>
/// 创建绑定查询执行运行时的查询描述。
/// </summary>
public static class SqlQueryRuntimeFactory
{
    /// <summary>
    /// 创建结构化查询描述。
    /// </summary>
    public static SqlFluentQuery<TResult> CreateQuery<TResult>(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

    /// <summary>
    /// 创建原生 SQL 查询描述。
    /// </summary>
    public static SqlTextQuery<TResult> CreateTextQuery<TResult>(ISqlQueryPlanExecutor executor, string commandText,
        object parameters) => new(executor, commandText, parameters);

    /// <summary>
    /// 创建结果类型由终结方法决定的原生 SQL 文本查询描述。
    /// </summary>
    public static SqlTextQuery CreateTextQuery(ISqlQueryPlanExecutor executor, string commandText, object parameters) =>
        new(executor, commandText, parameters);

    /// <summary>
    /// 创建存储过程查询描述。
    /// </summary>
    public static SqlProcedureQuery<TResult> CreateProcedureQuery<TResult>(ISqlQueryPlanExecutor executor,
        string procedure, object parameters) => new(executor, procedure, parameters);

    /// <summary>
    /// 创建唯一的非泛型 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery CreateLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

    /// <summary>
    /// 创建已发布一元泛型 Lambda 兼容查询描述。
    /// </summary>
    public static SqlLambdaQuery<TEntity> CreateLambdaQuery<TEntity>(ISqlQueryPlanExecutor executor,
        ISqlBuilder builder) where TEntity : class =>
        new(SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder));
}