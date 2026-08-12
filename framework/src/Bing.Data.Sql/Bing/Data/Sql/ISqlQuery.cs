namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。
/// </remarks>
public partial interface ISqlQuery : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 创建指定结果类型的独立 Fluent SQL 查询描述。
    /// </summary>
    /// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <returns>持有独立 SQL Builder 的指定结果类型查询描述。</returns>
    SqlQuery<TResult> Query<TResult>();

    /// <summary>
    /// 创建指定结果类型的原生 SQL 文本查询描述。
    /// 原生 SQL 不会自动应用结构化全局过滤器。
    /// </summary>
    /// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <param name="sql">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由后续参数绑定器处理的参数源。</param>
    /// <returns>保留 SQL 文本和参数源的查询描述。</returns>
    SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null);

    /// <summary>
    /// 创建指定结果类型的参数化插值 SQL 文本查询描述。
    /// 原生 SQL 不会自动应用结构化全局过滤器。
    /// </summary>
    /// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <param name="sql">包含参数化插值值的 SQL 文本。</param>
    /// <returns>保留参数化 SQL 文本和插值参数的查询描述。</returns>
    SqlTextQuery<TResult> SqlInterpolated<TResult>(FormattableString sql);

    /// <summary>
    /// 创建指定结果类型的存储过程查询描述。
    /// </summary>
    /// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <param name="procedure">要执行的存储过程名称。</param>
    /// <param name="parameters">由参数绑定器处理的输入和输出参数源。</param>
    /// <returns>固定以 <c>StoredProcedure</c> 命令类型执行的查询描述。</returns>
    SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null);

    /// <summary>
    /// 创建使用实体映射初始化的独立结构化 SQL 查询描述。
    /// </summary>
    /// <typeparam name="TEntity">查询结果和实体映射类型。</typeparam>
    /// <returns>已设置实体投影和来源表的独立查询描述。</returns>
    SqlLambdaQuery<TEntity> From<TEntity>() where TEntity : class;

    /// <summary>
    /// 创建以严格 DTO 类型化派生表作为根来源的独立结构化 SQL 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>仅允许访问派生表显式投影成员的查询描述。</returns>
    SqlSubqueryLambdaQuery<TProjection> From<TProjection>(SqlSubquery<TProjection> subquery)
        where TProjection : class;

    /// <summary>
    /// 创建使用两个实体映射初始化的独立结构化 SQL 查询描述。
    /// </summary>
    /// <typeparam name="TFirst">第一个表源及默认结果映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个表源类型。</typeparam>
    /// <returns>已设置两个实体根来源的独立查询描述。</returns>
    SqlLambdaQuery<TFirst, TSecond> From<TFirst, TSecond>() where TFirst : class where TSecond : class;

    /// <summary>创建使用三个实体根来源初始化的独立结构化 SQL 查询描述。</summary>
    SqlLambdaQuery<TFirst, TSecond, TThird> From<TFirst, TSecond, TThird>()
        where TFirst : class where TSecond : class where TThird : class;

    /// <summary>创建使用四个实体根来源初始化的独立结构化 SQL 查询描述。</summary>
    SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> From<TFirst, TSecond, TThird, TFourth>()
        where TFirst : class where TSecond : class where TThird : class where TFourth : class;

    /// <summary>创建使用五个实体根来源初始化的独立结构化 SQL 查询描述。</summary>
    SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> From<TFirst, TSecond, TThird, TFourth, TFifth>()
        where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class;

    /// <summary>创建使用六个实体根来源初始化的独立结构化 SQL 查询描述。</summary>
    SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> From<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>()
        where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class where TSixth : class;

    /// <summary>创建使用七个实体根来源初始化的独立结构化 SQL 查询描述。</summary>
    SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> From<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>()
        where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class where TSixth : class where TSeventh : class;
}
