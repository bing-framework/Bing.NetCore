namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。
/// </remarks>
public partial interface ISqlQuery : IDisposable, IAsyncDisposable
{
    /// <summary>创建结果类型由终结方法决定的独立 Fluent SQL 查询描述。</summary>
    /// <returns>持有独立 SQL Builder 的非泛型查询描述。</returns>
    SqlFluentQuery Query();

    /// <summary>
    /// 创建结果类型由终结方法决定的原生 SQL 文本查询描述。
    /// </summary>
    /// <param name="sql">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由后续参数绑定器处理的参数源。</param>
    /// <returns>保留 SQL 文本和参数源的查询描述。</returns>
    SqlTextQuery Sql(string sql, object parameters = null);

    /// <summary>
    /// 创建结果类型由终结方法决定的参数化插值 SQL 查询描述。
    /// </summary>
    /// <param name="sql">包含参数化插值值的 SQL 文本。</param>
    /// <returns>保留 SQL 文本和插值参数的查询描述。</returns>
    SqlTextQuery SqlInterpolated(FormattableString sql);

    /// <summary>创建结果类型由终结方法决定的存储过程查询描述。</summary>
    /// <param name="procedure">存储过程名称。</param>
    /// <param name="parameters">过程输入和输出参数。</param>
    /// <returns>结果类型由 Execute* 终结方法选择的过程描述。</returns>
    SqlProcedureQuery Procedure(string procedure, object parameters = null);

    /// <summary>创建使用实体映射初始化的独立结构化 SQL 查询描述。</summary>
    /// <typeparam name="TEntity">查询来源实体类型。</typeparam>
    /// <param name="alias">来源别名。</param>
    /// <param name="schema">来源架构。</param>
    /// <returns>可继续追加来源和 Lambda 操作的非泛型查询描述。</returns>
    SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class;

    /// <summary>
    /// 创建以严格 DTO 类型化派生表作为根来源的独立结构化 SQL 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>可继续追加来源和 Lambda 操作的非泛型查询描述。</returns>
    SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery)
        where TProjection : class;
}
