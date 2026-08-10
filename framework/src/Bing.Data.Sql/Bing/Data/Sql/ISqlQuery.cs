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
    SqlQuery<TResult> Sql<TResult>();

    /// <summary>
    /// 创建指定结果类型的原生 SQL 文本查询描述。
    /// </summary>
    /// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <param name="sql">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由后续参数绑定器处理的参数源。</param>
    /// <returns>保留 SQL 文本和参数源的查询描述。</returns>
    SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null);

    /// <summary>
    /// 创建指定结果类型的参数化插值 SQL 文本查询描述。
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
    /// 创建使用实体映射初始化的独立 Lambda SQL 查询描述。
    /// </summary>
    /// <typeparam name="TEntity">查询结果和实体映射类型。</typeparam>
    /// <returns>已设置实体投影和来源表的独立查询描述。</returns>
    SqlLambdaQuery<TEntity> Lambda<TEntity>() where TEntity : class;
}
