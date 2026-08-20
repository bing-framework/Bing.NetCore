using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 多表强类型 Lambda 查询描述的公共基类。
/// </summary>
/// <typeparam name="TResult">默认结果映射类型。</typeparam>
public abstract class SqlMultiLambdaQuery<TResult> : SqlQuery<TResult> where TResult : class
{
    /// <summary>
    /// 使用独立 SQL Builder 初始化多表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    internal SqlMultiLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : base(executor, builder)
    {
    }

    /// <summary>
    /// 使用已绑定表源解析多表谓词并追加到 Where 子句。
    /// </summary>
    /// <param name="expression">多表布尔筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    protected SqlMultiLambdaQuery<TResult> WhereCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        accessor.WhereClause.Where(GetFromClause(accessor).ResolveMultiSourcePredicate(expression,
            GetBoundSources(accessor)));
        return this;
    }

    /// <summary>
    /// 使用已绑定表源设置多表投影列。
    /// </summary>
    /// <param name="expression">返回 object 数组的多表投影表达式。</param>
    /// <returns>当前查询描述。</returns>
    protected SqlMultiLambdaQuery<TResult> SelectCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor));
        ReplaceSelect(string.Join(", ", columns));
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化表达式设置多表投影列。
    /// </summary>
    /// <param name="expression">返回 DTO 成员初始化对象的多表投影表达式。</param>
    protected void SelectTypedCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = expression.Body is MemberInitExpression
            ? GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, GetBoundSources(accessor))
            : GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor));
        ReplaceSelect(string.Join(", ", columns));
    }

    /// <summary>
    /// 使用严格 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="expression">多表 DTO 成员初始化投影表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <returns>可作为后续多表类型化连接来源的派生表。</returns>
    protected SqlSubquery<TProjection> SelectSubqueryCore<TProjection>(LambdaExpression expression, string alias)
        where TProjection : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, GetBoundSources(accessor),
            out var projectedMembers);
        var builder = GetBuilder().Clone();
        var subqueryAccessor = (ISqlQueryClauseAccessor)builder;
        builder.ClearSelect();
        subqueryAccessor.SelectClause.Select(string.Join(", ", columns));
        if (builder is SqlBuilderBase { HasLimit: false })
            builder.ClearOrderBy();
        var sqlBuilder = builder as SqlBuilderBase;
        var context = sqlBuilder?.GetDatabaseContext();
        var dataSourceKey = context?.DataSource?.Key ?? context?.DbKey;
        return new SqlSubquery<TProjection>(builder, alias, projectedMembers, builder.Provider?.Key, dataSourceKey,
            context?.MappingProfile, context?.TenantId, sqlBuilder?.GetDatabaseIdentity(),
            sqlBuilder?.GetExecutionScope());
    }

    /// <summary>
    /// 使用已绑定表源设置多表分组列。
    /// </summary>
    /// <param name="expression">返回 object 数组的多表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    protected SqlMultiLambdaQuery<TResult> GroupByCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor)));
        return this;
    }

    /// <summary>
    /// 使用已绑定表源设置多表 Having 条件。
    /// </summary>
    /// <param name="expression">多表布尔分组筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    protected SqlMultiLambdaQuery<TResult> HavingCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .SetBoundHaving(GetFromClause(accessor).ResolveMultiSourcePredicate(expression,
                GetBoundSources(accessor)));
        return this;
    }

    /// <summary>
    /// 使用已绑定表源设置多表排序列。
    /// </summary>
    /// <param name="expression">返回 object 数组的多表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    protected SqlMultiLambdaQuery<TResult> OrderByCore(LambdaExpression expression, bool desc)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.OrderByClause as OrderByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表排序查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor)), desc);
        return this;
    }

    /// <summary>
    /// 原子添加类型化内连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">包含当前全部表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    protected void JoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().Join<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>
    /// 原子添加类型化左外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">包含当前全部表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    protected void LeftJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().LeftJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>
    /// 原子添加类型化右外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">包含当前全部表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    protected void RightJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().RightJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>
    /// 原子添加类型化全外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">包含当前全部表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    protected void FullJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().FullJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>
    /// 原子添加类型化派生表内连接。
    /// </summary>
    protected void JoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().Join(GetFromClause(), subquery, predicate);

    /// <summary>
    /// 原子添加类型化派生表左外连接。
    /// </summary>
    protected void LeftJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().LeftJoin(GetFromClause(), subquery, predicate);

    /// <summary>
    /// 原子添加类型化派生表右外连接。
    /// </summary>
    protected void RightJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().RightJoin(GetFromClause(), subquery, predicate);

    /// <summary>
    /// 原子添加类型化派生表全外连接。
    /// </summary>
    protected void FullJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().FullJoin(GetFromClause(), subquery, predicate);

    /// <summary>
    /// 添加类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    protected void CrossJoinCore<TJoin>(string alias, string schema) where TJoin : class =>
        GetBuilder().CrossJoin<TJoin>(alias, schema);

    /// <summary>
    /// 添加类型化交叉连接派生表。
    /// </summary>
    protected void CrossJoinCore<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class =>
        ((JoinClause)((ISqlQueryClauseAccessor)GetBuilder()).JoinClause).CrossJoin(subquery);

    /// <summary>
    /// 使用候选投影原子替换当前 Select 子句。
    /// </summary>
    /// <param name="columns">已解析的完整投影列 SQL。</param>
    private void ReplaceSelect(string columns)
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        builder.ReplaceSelect(select => select.Select(columns));
    }

    /// <summary>
    /// 设置查询的分页偏移量。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    protected void SkipCore(int count) => GetBuilder().Skip(count);

    /// <summary>
    /// 设置查询的最大返回行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    protected void TakeCore(int count) => GetBuilder().Take(count);

    /// <summary>
    /// 获取当前查询的类型化根来源子句。
    /// </summary>
    /// <returns>支持表源绑定图的 From 子句。</returns>
    private protected FromClause GetFromClause() =>
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder());

    /// <summary>
    /// 获取当前查询的类型化连接子句。
    /// </summary>
    /// <returns>支持原子 Lambda Join 的 Join 子句。</returns>
    private protected JoinClause GetJoinClause() =>
        ((ISqlQueryClauseAccessor)GetBuilder()).JoinClause as JoinClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表连接查询。");

    /// <summary>
    /// 获取支持表源实例图的 From 子句。
    /// </summary>
    protected static FromClause GetFromClause(ISqlQueryClauseAccessor accessor) => accessor.FromClause as FromClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表根来源查询。");

    /// <summary>
    /// 获取根来源与类型化连接组成的完整表源绑定图。
    /// </summary>
    /// <param name="accessor">当前查询子句访问器。</param>
    /// <returns>按 Lambda 参数顺序排列的实体表源。</returns>
    private protected static IReadOnlyList<TableSource> GetBoundSources(ISqlQueryClauseAccessor accessor)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        var sources = new List<TableSource>(GetFromClause(accessor).Sources);
        if (accessor.JoinClause is JoinClause joinClause)
            sources.AddRange(joinClause.GetTypedSources());
        return sources;
    }
}

/// <summary>
/// 使用两个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
/// <typeparam name="TFirst">第一个表源及默认结果映射类型。</typeparam>
/// <typeparam name="TSecond">第二个表源类型。</typeparam>
public sealed class SqlLambdaQuery<TFirst, TSecond> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class
{
    /// <summary>
    /// 使用独立 SQL Builder 初始化双表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化双表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以双表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>
    /// 追加双表布尔筛选表达式。
    /// </summary>
    /// <param name="predicate">双表筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Where(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>
    /// 设置双表投影列。
    /// </summary>
    /// <param name="columns">双表投影表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Select(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置双表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">双表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Select<TProjection>(Expression<Func<TFirst, TSecond, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用双表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(Expression<Func<TFirst, TSecond, TProjection>> projection,
        string alias) where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>
    /// 设置双表分组列。
    /// </summary>
    /// <param name="columns">双表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> GroupBy(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>
    /// 设置双表 Having 条件。
    /// </summary>
    /// <param name="predicate">双表分组筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Having(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 添加第三个类型化连接表。
    /// </summary>
    /// <typeparam name="TThird">连接表实体类型。</typeparam>
    /// <param name="predicate">包含三个表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含三个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Join<TThird>(
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate, string alias = null, string schema = null)
        where TThird : class
    {
        JoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化左外连接表。
    /// </summary>
    /// <typeparam name="TThird">连接表实体类型。</typeparam>
    /// <param name="predicate">包含三个表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含三个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> LeftJoin<TThird>(
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate, string alias = null, string schema = null)
        where TThird : class
    {
        LeftJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化右外连接表。
    /// </summary>
    /// <typeparam name="TThird">连接表实体类型。</typeparam>
    /// <param name="predicate">包含三个表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含三个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> RightJoin<TThird>(
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate, string alias = null, string schema = null)
        where TThird : class
    {
        RightJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化全外连接表。
    /// </summary>
    /// <typeparam name="TThird">连接表实体类型。</typeparam>
    /// <param name="predicate">包含三个表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含三个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> FullJoin<TThird>(
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate, string alias = null, string schema = null)
        where TThird : class
    {
        FullJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TThird">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含三个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> CrossJoin<TThird>(string alias = null, string schema = null)
        where TThird : class
    {
        CrossJoinCore<TThird>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化内连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Join<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化左外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> LeftJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化右外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> RightJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化全外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> FullJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第三个类型化交叉连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> CrossJoin<TThird>(SqlSubquery<TThird> subquery) where TThird : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 跳过指定数量的双表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制双表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>
    /// 设置双表排序列。
    /// </summary>
    /// <param name="columns">双表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond> OrderBy(Expression<Func<TFirst, TSecond, object[]>> columns,
        bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

/// <summary>
/// 使用三个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化三表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以三表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>追加三表布尔筛选表达式。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Where(Expression<Func<TFirst, TSecond, TThird, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>设置三表投影列。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Select(Expression<Func<TFirst, TSecond, TThird, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置三表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">三表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用三表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>设置三表分组列。</summary>
    /// <param name="columns">三表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> GroupBy(Expression<Func<TFirst, TSecond, TThird, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>设置三表 Having 条件。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Having(Expression<Func<TFirst, TSecond, TThird, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 添加第四个类型化连接表。
    /// </summary>
    /// <typeparam name="TFourth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含四个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Join<TFourth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate, string alias = null, string schema = null)
        where TFourth : class
    {
        JoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化左外连接表。
    /// </summary>
    /// <typeparam name="TFourth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含四个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> LeftJoin<TFourth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate, string alias = null, string schema = null)
        where TFourth : class
    {
        LeftJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化右外连接表。
    /// </summary>
    /// <typeparam name="TFourth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含四个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> RightJoin<TFourth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate, string alias = null, string schema = null)
        where TFourth : class
    {
        RightJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化全外连接表。
    /// </summary>
    /// <typeparam name="TFourth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含四个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> FullJoin<TFourth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate, string alias = null, string schema = null)
        where TFourth : class
    {
        FullJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TFourth">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含四个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> CrossJoin<TFourth>(string alias = null, string schema = null)
        where TFourth : class
    {
        CrossJoinCore<TFourth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化内连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Join<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
        where TFourth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化左外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> LeftJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
        where TFourth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化右外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> RightJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
        where TFourth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化全外连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> FullJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
        where TFourth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第四个类型化交叉连接派生表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> CrossJoin<TFourth>(SqlSubquery<TFourth> subquery)
        where TFourth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 跳过指定数量的三表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制三表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>设置三表排序列。</summary>
    /// <param name="columns">三表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird> OrderBy(Expression<Func<TFirst, TSecond, TThird, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

/// <summary>
/// 使用四个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化四表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以四表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>追加四表布尔筛选表达式。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>设置四表投影列。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置四表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">四表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用四表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>设置四表分组列。</summary>
    /// <param name="columns">四表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>设置四表 Having 条件。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 添加第五个类型化连接表。
    /// </summary>
    /// <typeparam name="TFifth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含五个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Join<TFifth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate, string alias = null, string schema = null)
        where TFifth : class
    {
        JoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第五个类型化左外连接表。
    /// </summary>
    /// <typeparam name="TFifth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含五个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> LeftJoin<TFifth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate, string alias = null, string schema = null)
        where TFifth : class
    {
        LeftJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第五个类型化右外连接表。
    /// </summary>
    /// <typeparam name="TFifth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含五个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> RightJoin<TFifth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate, string alias = null, string schema = null)
        where TFifth : class
    {
        RightJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第五个类型化全外连接表。
    /// </summary>
    /// <typeparam name="TFifth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含五个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> FullJoin<TFifth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate, string alias = null, string schema = null)
        where TFifth : class
    {
        FullJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第五个类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TFifth">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含五个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> CrossJoin<TFifth>(string alias = null, string schema = null)
        where TFifth : class
    {
        CrossJoinCore<TFifth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第五个类型化内连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Join<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
        where TFifth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第五个类型化左外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> LeftJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
        where TFifth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第五个类型化右外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> RightJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
        where TFifth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第五个类型化全外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> FullJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
        where TFifth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第五个类型化交叉连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> CrossJoin<TFifth>(SqlSubquery<TFifth> subquery)
        where TFifth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 跳过指定数量的四表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制四表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>设置四表排序列。</summary>
    /// <param name="columns">四表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

/// <summary>
/// 使用五个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化五表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以五表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>追加五表布尔筛选表达式。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>设置五表投影列。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置五表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">五表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用五表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TProjection>> projection, string alias)
        where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>设置五表分组列。</summary>
    /// <param name="columns">五表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>设置五表 Having 条件。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 添加第六个类型化连接表。
    /// </summary>
    /// <typeparam name="TSixth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含六个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Join<TSixth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate, string alias = null, string schema = null)
        where TSixth : class
    {
        JoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第六个类型化左外连接表。
    /// </summary>
    /// <typeparam name="TSixth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含六个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> LeftJoin<TSixth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate, string alias = null, string schema = null)
        where TSixth : class
    {
        LeftJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第六个类型化右外连接表。
    /// </summary>
    /// <typeparam name="TSixth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含六个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> RightJoin<TSixth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate, string alias = null, string schema = null)
        where TSixth : class
    {
        RightJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第六个类型化全外连接表。
    /// </summary>
    /// <typeparam name="TSixth">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含六个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> FullJoin<TSixth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate, string alias = null, string schema = null)
        where TSixth : class
    {
        FullJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第六个类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TSixth">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含六个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> CrossJoin<TSixth>(string alias = null, string schema = null)
        where TSixth : class
    {
        CrossJoinCore<TSixth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第六个类型化内连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Join<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
        where TSixth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第六个类型化左外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> LeftJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
        where TSixth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第六个类型化右外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> RightJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
        where TSixth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第六个类型化全外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> FullJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
        where TSixth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第六个类型化交叉连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> CrossJoin<TSixth>(SqlSubquery<TSixth> subquery)
        where TSixth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 跳过指定数量的五表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制五表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>设置五表排序列。</summary>
    /// <param name="columns">五表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

/// <summary>
/// 使用六个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class where TSixth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化六表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以六表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>追加六表布尔筛选表达式。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>设置六表投影列。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置六表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">六表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用六表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TProjection>> projection, string alias)
        where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>设置六表分组列。</summary>
    /// <param name="columns">六表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>设置六表 Having 条件。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 添加第七个类型化连接表。
    /// </summary>
    /// <typeparam name="TSeventh">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含七个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Join<TSeventh>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null)
        where TSeventh : class
    {
        JoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第七个类型化左外连接表。
    /// </summary>
    /// <typeparam name="TSeventh">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含七个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> LeftJoin<TSeventh>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null)
        where TSeventh : class
    {
        LeftJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第七个类型化右外连接表。
    /// </summary>
    /// <typeparam name="TSeventh">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含七个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> RightJoin<TSeventh>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null)
        where TSeventh : class
    {
        RightJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第七个类型化全外连接表。
    /// </summary>
    /// <typeparam name="TSeventh">连接表实体类型。</typeparam>
    /// <param name="predicate">包含所有表源的连接条件。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含七个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> FullJoin<TSeventh>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null)
        where TSeventh : class
    {
        FullJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加第七个类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TSeventh">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构。</param>
    /// <returns>包含七个表源的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> CrossJoin<TSeventh>(string alias = null, string schema = null)
        where TSeventh : class
    {
        CrossJoinCore<TSeventh>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第七个类型化内连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Join<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
        where TSeventh : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第七个类型化左外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> LeftJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
        where TSeventh : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第七个类型化右外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> RightJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
        where TSeventh : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第七个类型化全外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> FullJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
        where TSeventh : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>添加第七个类型化交叉连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> CrossJoin<TSeventh>(SqlSubquery<TSeventh> subquery)
        where TSeventh : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 跳过指定数量的六表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制六表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>设置六表排序列。</summary>
    /// <param name="columns">六表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

/// <summary>
/// 使用七个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class where TSixth : class where TSeventh : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    /// <summary>
    /// 使用既有查询图初始化七表查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="initializeRoots">是否以七表根来源替换当前查询图。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh) });
            GetBuilder().Select<TFirst>();
        }
    }

    /// <summary>追加七表布尔筛选表达式。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    /// <summary>设置七表投影列。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    /// <summary>
    /// 使用 DTO 成员初始化设置七表投影并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TProjection">投影结果映射类型。</typeparam>
    /// <param name="projection">七表 DTO 成员初始化投影表达式。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    /// <summary>
    /// 使用七表 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TProjection>> projection,
        string alias) where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);

    /// <summary>设置七表分组列。</summary>
    /// <param name="columns">七表分组表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    /// <summary>设置七表 Having 条件。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    /// <summary>
    /// 跳过指定数量的七表查询结果行。
    /// </summary>
    /// <param name="count">要跳过的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制七表查询返回的结果行数。
    /// </summary>
    /// <param name="count">最多返回的结果行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>设置七表排序列。</summary>
    /// <param name="columns">七表排序表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    /// <summary>
    /// 原子添加第八个类型化内连接表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Join<TEighth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        JoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>
    /// 原子添加第八个类型化左外连接表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> LeftJoin<TEighth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        LeftJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>
    /// 原子添加第八个类型化右外连接表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> RightJoin<TEighth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        RightJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>
    /// 原子添加第八个类型化全外连接表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> FullJoin<TEighth>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        FullJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>
    /// 添加第八个类型化交叉连接表。
    /// </summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> CrossJoin<TEighth>(
        string alias = null, string schema = null) where TEighth : class
    {
        CrossJoinCore<TEighth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>原子添加第八个类型化内连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Join<TEighth>(
        SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
        where TEighth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>原子添加第八个类型化左外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> LeftJoin<TEighth>(
        SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
        where TEighth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>原子添加第八个类型化右外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> RightJoin<TEighth>(
        SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
        where TEighth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>原子添加第八个类型化全外连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> FullJoin<TEighth>(
        SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
        where TEighth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }

    /// <summary>添加第八个类型化交叉连接派生表。</summary>
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> CrossJoin<TEighth>(
        SqlSubquery<TEighth> subquery) where TEighth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor,
            GetBuilder(), false);
    }
}

public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
    where TSixth : class where TSeventh : class where TEighth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true) { }

    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[]
            {
                typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh), typeof(TEighth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) { WhereCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns) { SelectCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TProjection>> projection, string alias) where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns) { GroupByCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) { HavingCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Skip(int count) { SkipCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Take(int count) { TakeCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns, bool desc = false) { OrderByCore(columns, desc); return this; }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Join<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate, string alias = null, string schema = null) where TNinth : class { JoinCore<TNinth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> LeftJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate, string alias = null, string schema = null) where TNinth : class { LeftJoinCore<TNinth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> RightJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate, string alias = null, string schema = null) where TNinth : class { RightJoinCore<TNinth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> FullJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate, string alias = null, string schema = null) where TNinth : class { FullJoinCore<TNinth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> CrossJoin<TNinth>(string alias = null, string schema = null) where TNinth : class { CrossJoinCore<TNinth>(alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Join<TNinth>(SqlSubquery<TNinth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class { JoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> LeftJoin<TNinth>(SqlSubquery<TNinth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class { LeftJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> RightJoin<TNinth>(SqlSubquery<TNinth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class { RightJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> FullJoin<TNinth>(SqlSubquery<TNinth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class { FullJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> CrossJoin<TNinth>(SqlSubquery<TNinth> subquery) where TNinth : class { CrossJoinCore(subquery); return new(Executor, GetBuilder(), false); }
}

public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
    where TSixth : class where TSeventh : class where TEighth : class where TNinth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true) { }

    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[]
            {
                typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh), typeof(TEighth), typeof(TNinth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) { WhereCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, object[]>> columns) { SelectCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TProjection>> projection, string alias) where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, object[]>> columns) { GroupByCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) { HavingCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Skip(int count) { SkipCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Take(int count) { TakeCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, object[]>> columns, bool desc = false) { OrderByCore(columns, desc); return this; }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Join<TTenth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate, string alias = null, string schema = null) where TTenth : class { JoinCore<TTenth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> LeftJoin<TTenth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate, string alias = null, string schema = null) where TTenth : class { LeftJoinCore<TTenth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> RightJoin<TTenth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate, string alias = null, string schema = null) where TTenth : class { RightJoinCore<TTenth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> FullJoin<TTenth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate, string alias = null, string schema = null) where TTenth : class { FullJoinCore<TTenth>(predicate, alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> CrossJoin<TTenth>(string alias = null, string schema = null) where TTenth : class { CrossJoinCore<TTenth>(alias, schema); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Join<TTenth>(SqlSubquery<TTenth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) where TTenth : class { JoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> LeftJoin<TTenth>(SqlSubquery<TTenth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) where TTenth : class { LeftJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> RightJoin<TTenth>(SqlSubquery<TTenth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) where TTenth : class { RightJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> FullJoin<TTenth>(SqlSubquery<TTenth> subquery, Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) where TTenth : class { FullJoinCore(subquery, predicate); return new(Executor, GetBuilder(), false); }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> CrossJoin<TTenth>(SqlSubquery<TTenth> subquery) where TTenth : class { CrossJoinCore(subquery); return new(Executor, GetBuilder(), false); }
}

public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> : SqlMultiLambdaQuery<TFirst>
    where TFirst : class where TSecond : class where TThird : class where TFourth : class where TFifth : class
    where TSixth : class where TSeventh : class where TEighth : class where TNinth : class where TTenth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true) { }

    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[]
            {
                typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh), typeof(TEighth), typeof(TNinth), typeof(TTenth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) { WhereCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns) { SelectCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TProjection>> projection, string alias) where TProjection : class => SelectSubqueryCore<TProjection>(projection, alias);
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns) { GroupByCore(columns); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate) { HavingCore(predicate); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Skip(int count) { SkipCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Take(int count) { TakeCore(count); return this; }
    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns, bool desc = false) { OrderByCore(columns, desc); return this; }
}
