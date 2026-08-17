namespace Bing.Data.Sql;

/// <summary>
/// 创建绑定查询执行运行时的查询描述。
/// </summary>
public static class SqlQueryRuntimeFactory
{
    /// <summary>
    /// 创建结构化查询描述。
    /// </summary>
    public static SqlQuery<TResult> CreateQuery<TResult>(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        new(executor, builder);

    /// <summary>
    /// 创建原生 SQL 查询描述。
    /// </summary>
    public static SqlTextQuery<TResult> CreateTextQuery<TResult>(ISqlQueryPlanExecutor executor, string commandText,
        object parameters) => new(executor, commandText, parameters);

    /// <summary>
    /// 创建存储过程查询描述。
    /// </summary>
    public static SqlProcedureQuery<TResult> CreateProcedureQuery<TResult>(ISqlQueryPlanExecutor executor,
        string procedure, object parameters) => new(executor, procedure, parameters);

    /// <summary>
    /// 创建单来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TEntity> CreateLambdaQuery<TEntity>(ISqlQueryPlanExecutor executor,
        ISqlBuilder builder) where TEntity : class => new(executor, builder);

    /// <summary>
    /// 创建派生来源 Lambda 查询描述。
    /// </summary>
    public static SqlSubqueryLambdaQuery<TProjection> CreateSubqueryLambdaQuery<TProjection>(
        ISqlQueryPlanExecutor executor, ISqlBuilder builder, SqlSubquery<TProjection> subquery)
        where TProjection : class => new(executor, builder, subquery);

    /// <summary>
    /// 创建双来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond> CreateLambdaQuery<TFirst, TSecond>(ISqlQueryPlanExecutor executor,
        ISqlBuilder builder) where TFirst : class where TSecond : class => new(executor, builder);

    /// <summary>
    /// 创建三来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond, TThird> CreateLambdaQuery<TFirst, TSecond, TThird>(
        ISqlQueryPlanExecutor executor, ISqlBuilder builder) where TFirst : class where TSecond : class
        where TThird : class => new(executor, builder);

    /// <summary>
    /// 创建四来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> CreateLambdaQuery<TFirst, TSecond, TThird, TFourth>(
        ISqlQueryPlanExecutor executor, ISqlBuilder builder) where TFirst : class where TSecond : class
        where TThird : class where TFourth : class => new(executor, builder);

    /// <summary>
    /// 创建五来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> CreateLambdaQuery<TFirst, TSecond, TThird,
        TFourth, TFifth>(ISqlQueryPlanExecutor executor, ISqlBuilder builder) where TFirst : class where TSecond : class
        where TThird : class where TFourth : class where TFifth : class => new(executor, builder);

    /// <summary>
    /// 创建六来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> CreateLambdaQuery<TFirst, TSecond,
        TThird, TFourth, TFifth, TSixth>(ISqlQueryPlanExecutor executor, ISqlBuilder builder) where TFirst : class
        where TSecond : class where TThird : class where TFourth : class where TFifth : class where TSixth : class =>
        new(executor, builder);

    /// <summary>
    /// 创建七来源 Lambda 查询描述。
    /// </summary>
    public static SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> CreateLambdaQuery<TFirst,
        TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(ISqlQueryPlanExecutor executor, ISqlBuilder builder)
        where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
        where TSixth : class where TSeventh : class => new(executor, builder);
}