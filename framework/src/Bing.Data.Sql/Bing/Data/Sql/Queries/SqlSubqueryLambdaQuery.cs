using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql;

/// <summary>
/// 以类型化 DTO 派生表作为唯一根来源的 Lambda 查询描述。
/// </summary>
/// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
/// <remarks>
/// 所有 Lambda 成员访问均按派生表投影白名单解析，不能退化为实体映射访问。
/// </remarks>
public sealed class SqlSubqueryLambdaQuery<TProjection> : SqlMultiLambdaQuery<TProjection>
    where TProjection : class
{
    /// <summary>
    /// 使用已冻结的类型化派生表初始化根查询。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="subquery">作为根来源的类型化派生表。</param>
    internal SqlSubqueryLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
        SqlSubquery<TProjection> subquery) : base(executor, builder)
    {
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).From(subquery);
    }

    /// <summary>
    /// 追加派生表 DTO 成员筛选条件。
    /// </summary>
    /// <param name="predicate">派生表 DTO 筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> Where(Expression<Func<TProjection, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>
    /// 设置派生表 DTO 投影列。
    /// </summary>
    /// <param name="columns">派生表 DTO 投影表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> Select(Expression<Func<TProjection, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TResult">投影结果映射类型。</typeparam>
    /// <param name="projection">派生表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlQuery<TResult> Select<TResult>(Expression<Func<TProjection, TResult>> projection) where TResult : class =>
        SelectTypedCore<TResult>(projection);

    /// <summary>
    /// 将当前派生根的严格 DTO 投影冻结为新的类型化派生表。
    /// </summary>
    /// <typeparam name="TResult">新派生表公开的 DTO 类型。</typeparam>
    /// <param name="projection">派生表 DTO 成员初始化投影表达式。</param>
    /// <param name="alias">新派生表别名。</param>
    /// <returns>冻结的新类型化派生表。</returns>
    public SqlSubquery<TResult> SelectSubquery<TResult>(Expression<Func<TProjection, TResult>> projection, string alias)
        where TResult : class => SelectSubqueryCore<TResult>(projection, alias);

    /// <summary>
    /// 设置派生表 DTO 分组列。
    /// </summary>
    /// <param name="columns">派生表 DTO 分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> GroupBy(Expression<Func<TProjection, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>
    /// 设置派生表 DTO Having 条件。
    /// </summary>
    /// <param name="predicate">派生表 DTO Having 表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> Having(Expression<Func<TProjection, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 设置派生表 DTO 排序列。
    /// </summary>
    /// <param name="columns">派生表 DTO 排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> OrderBy(Expression<Func<TProjection, object[]>> columns,
        bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    /// <summary>
    /// 跳过指定数量的结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制返回的结果行数量。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlSubqueryLambdaQuery<TProjection> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>
    /// 添加类型化内连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含派生根和连接表的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> Join<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        JoinCore<TJoin>(alias, schema);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化左外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含派生根和连接表的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> LeftJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        LeftJoinCore<TJoin>(alias, schema);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化右外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含派生根和连接表的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> RightJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        RightJoinCore<TJoin>(alias, schema);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化全外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含派生根和连接表的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> FullJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        FullJoinCore<TJoin>(alias, schema);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含派生根和连接表的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> CrossJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        CrossJoinCore<TJoin>(alias, schema);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化内连接派生表。
    /// </summary>
    /// <typeparam name="TJoin">连接派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含两个派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> Join<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class
    {
        JoinCore(subquery);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化左外连接派生表。
    /// </summary>
    /// <typeparam name="TJoin">连接派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含两个派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> LeftJoin<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class
    {
        LeftJoinCore(subquery);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化右外连接派生表。
    /// </summary>
    /// <typeparam name="TJoin">连接派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含两个派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> RightJoin<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class
    {
        RightJoinCore(subquery);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化全外连接派生表。
    /// </summary>
    /// <typeparam name="TJoin">连接派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含两个派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> FullJoin<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class
    {
        FullJoinCore(subquery);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化交叉连接派生表。
    /// </summary>
    /// <typeparam name="TJoin">连接派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含两个派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TProjection, TJoin> CrossJoin<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TProjection, TJoin>(Executor, GetBuilder(), false);
    }
}
