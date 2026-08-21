using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 使用实体映射构建的强类型 Lambda SQL 查询描述。
/// </summary>
/// <typeparam name="TEntity">查询来源实体类型。</typeparam>
/// <remarks>
/// 此类型仅用于实体查询，避免标量 <see cref="SqlQuery{TResult}"/> 承担实体表达式约束。
/// </remarks>
public sealed class SqlLambdaQuery<TEntity> : SqlMultiLambdaQuery where TEntity : class
{
    /// <summary>
    /// 使用独立 SQL Builder 初始化实体 Lambda 查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : base(executor, builder)
    {
    }

    /// <summary>
    /// 使用当前实体类型设置投影列。
    /// </summary>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select(bool propertyAsAlias = false)
    {
        ReplaceSelect(select => select.Select<TEntity>(propertyAsAlias));
        return this;
    }

    /// <summary>
    /// 使用当前实体类型设置投影列。
    /// </summary>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object[]>> columns)
    {
        return Select(columns, false);
    }

    /// <summary>
    /// 使用当前实体类型设置投影列。
    /// </summary>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
    {
        ReplaceSelect(select => select.Select(columns, propertyAsAlias));
        return this;
    }

    /// <summary>
    /// 使用当前实体类型设置单个投影列。
    /// </summary>
    /// <param name="column">实体属性投影表达式。</param>
    /// <param name="columnAlias">投影列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object>> column, string columnAlias = null)
    {
        ReplaceSelect(select => select.Select(column, columnAlias));
        return this;
    }

    /// <summary>
    /// 使用强类型成员初始化表达式设置 DTO 投影。
    /// </summary>
    /// <typeparam name="TProjection">投影形状类型；最终物化类型由显式 TResult 终结方法指定。</typeparam>
    /// <param name="projection">当前实体的直接成员初始化投影表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select<TProjection>(Expression<Func<TEntity, TProjection>> projection)
    {
        if (projection == null)
            throw new ArgumentNullException(nameof(projection));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var fromClause = accessor.FromClause as FromClause ??
            throw new NotSupportedException("当前 SQL Provider 不支持强类型 Lambda 投影。");
        var columns = projection.Body is MemberInitExpression
            ? fromClause.ResolveMultiSourceDtoColumns(projection, new[] { fromClause.Sources[0] })
            : fromClause.ResolveMultiSourceColumns(projection, new[] { fromClause.Sources[0] });
        ReplaceSelect(select => select.Select(string.Join(", ", columns)));
        return this;
    }

    /// <summary>
    /// 在当前投影后追加当前实体类型的全部列。
    /// </summary>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> AppendSelect(bool propertyAsAlias = false)
    {
        GetBuilder().Select<TEntity>(propertyAsAlias);
        return this;
    }

    /// <summary>
    /// 在当前投影后追加当前实体类型的投影列。
    /// </summary>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> AppendSelect(Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false)
    {
        GetBuilder().Select(columns, propertyAsAlias);
        return this;
    }

    /// <summary>
    /// 在当前投影后追加当前实体类型的单个投影列。
    /// </summary>
    /// <param name="column">实体属性投影表达式。</param>
    /// <param name="columnAlias">投影列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> AppendSelect(Expression<Func<TEntity, object>> column, string columnAlias = null)
    {
        GetBuilder().Select(column, columnAlias);
        return this;
    }

    /// <summary>
    /// 清空当前投影列。
    /// </summary>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> ClearSelect()
    {
        GetBuilder().ClearSelect();
        return this;
    }

    /// <summary>
    /// 对当前投影启用去重。
    /// </summary>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Distinct()
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).SelectClause.Distinct();
        return this;
    }

    /// <summary>
    /// 追加当前实体的布尔筛选表达式。
    /// </summary>
    /// <param name="predicate">实体筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        GetBuilder().Where(predicate);
        return this;
    }

    /// <summary>
    /// 追加当前实体的参数化属性筛选条件。
    /// </summary>
    /// <typeparam name="TValue">属性和参数值类型。</typeparam>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Where<TValue>(Expression<Func<TEntity, TValue>> column, TValue value,
        Operator @operator = Operator.Equal)
    {
        var selector = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(column.Body, typeof(object)),
            column.Parameters);
        ((ISqlQueryClauseAccessor)GetBuilder()).WhereClause.Where(selector, value, @operator);
        return this;
    }

    /// <summary>
    /// 按条件追加当前实体的参数化属性筛选条件。
    /// </summary>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="condition">是否追加筛选条件。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIf(Expression<Func<TEntity, object>> column, object value, bool condition,
        Operator @operator = Operator.Equal)
    {
        GetBuilder().WhereIf(column, value, condition, @operator);
        return this;
    }

    /// <summary>
    /// 按条件追加当前实体的布尔筛选表达式。
    /// </summary>
    /// <param name="condition">是否追加筛选条件。</param>
    /// <param name="predicate">实体筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIf(Expression<Func<TEntity, bool>> predicate, bool condition)
    {
        GetBuilder().WhereIf(predicate, condition);
        return this;
    }

    /// <summary>
    /// 当当前实体的筛选值非空时追加参数化条件。
    /// </summary>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIfNotEmpty(Expression<Func<TEntity, object>> column, object value,
        Operator @operator = Operator.Equal)
    {
        GetBuilder().WhereIfNotEmpty(column, value, @operator);
        return this;
    }

    /// <summary>
    /// 当当前实体的布尔筛选表达式非空时追加条件。
    /// </summary>
    /// <param name="predicate">实体筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIfNotEmpty(Expression<Func<TEntity, bool>> predicate)
    {
        GetBuilder().WhereIfNotEmpty(predicate);
        return this;
    }

    /// <summary>
    /// 添加类型化内连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="on">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity, TJoin> Join<TJoin>(Expression<Func<TEntity, TJoin, bool>> on,
        string alias = null, string schema = null) where TJoin : class
    {
        GetTypedJoinClause().Join<TJoin>(GetTypedFromClause(), on, alias, schema);
        return new SqlLambdaQuery<TEntity, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化左外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="on">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity, TJoin> LeftJoin<TJoin>(Expression<Func<TEntity, TJoin, bool>> on,
        string alias = null, string schema = null) where TJoin : class
    {
        GetTypedJoinClause().LeftJoin<TJoin>(GetTypedFromClause(), on, alias, schema);
        return new SqlLambdaQuery<TEntity, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化右外连接表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="on">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>包含实体来源和连接表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TJoin> RightJoin<TJoin>(Expression<Func<TEntity, TJoin, bool>> on,
        string alias = null, string schema = null) where TJoin : class
    {
        GetTypedJoinClause().RightJoin<TJoin>(GetTypedFromClause(), on, alias, schema);
        return new SqlLambdaQuery<TEntity, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化全外连接表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="on">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>包含实体来源和连接表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TJoin> FullJoin<TJoin>(Expression<Func<TEntity, TJoin, bool>> on,
        string alias = null, string schema = null) where TJoin : class
    {
        GetTypedJoinClause().FullJoin<TJoin>(GetTypedFromClause(), on, alias, schema);
        return new SqlLambdaQuery<TEntity, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化交叉连接表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>包含实体来源和连接表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TJoin> CrossJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        GetBuilder().CrossJoin<TJoin>(alias, schema);
        return new SqlLambdaQuery<TEntity, TJoin>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 将严格 DTO 投影冻结为可复用的类型化派生表。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="projection">当前实体的 DTO 成员初始化投影表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <returns>冻结投影、参数、Provider 和数据源身份的类型化派生表。</returns>
    public SqlSubquery<TProjection> SelectSubquery<TProjection>(Expression<Func<TEntity, TProjection>> projection,
        string alias) where TProjection : class
    {
        if (projection == null)
            throw new ArgumentNullException(nameof(projection));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var fromClause = accessor.FromClause as FromClause ??
            throw new NotSupportedException("当前 SQL Provider 不支持单表类型化派生查询。");
        if (fromClause.Sources.Count != 1)
            throw new NotSupportedException("单表类型化派生查询只能包含一个根来源。");
        var columns = fromClause.ResolveMultiSourceDtoColumns(projection, new[] { fromClause.Sources[0] },
            out var projectedMembers);
        var builder = GetBuilder().Clone();
        builder.ClearSelect();
        ((ISqlQueryClauseAccessor)builder).SelectClause.Select(string.Join(", ", columns));
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
    /// 添加带连接条件的类型化内连接派生表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <param name="predicate">包含根表和派生表的连接条件。</param>
    /// <returns>包含实体来源和派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TProjection> Join<TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TEntity, TProjection, bool>> predicate)
        where TProjection : class
    {
        GetTypedJoinClause().Join(GetTypedFromClause(), subquery, predicate);
        return new SqlLambdaQuery<TEntity, TProjection>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加带连接条件的类型化左外连接派生表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <param name="predicate">包含根表和派生表的连接条件。</param>
    /// <returns>包含实体来源和派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TProjection> LeftJoin<TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TEntity, TProjection, bool>> predicate)
        where TProjection : class
    {
        GetTypedJoinClause().LeftJoin(GetTypedFromClause(), subquery, predicate);
        return new SqlLambdaQuery<TEntity, TProjection>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加带连接条件的类型化右外连接派生表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <param name="predicate">包含根表和派生表的连接条件。</param>
    /// <returns>包含实体来源和派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TProjection> RightJoin<TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TEntity, TProjection, bool>> predicate)
        where TProjection : class
    {
        GetTypedJoinClause().RightJoin(GetTypedFromClause(), subquery, predicate);
        return new SqlLambdaQuery<TEntity, TProjection>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加带连接条件的类型化全外连接派生表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <param name="predicate">包含根表和派生表的连接条件。</param>
    /// <returns>包含实体来源和派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TProjection> FullJoin<TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TEntity, TProjection, bool>> predicate)
        where TProjection : class
    {
        GetTypedJoinClause().FullJoin(GetTypedFromClause(), subquery, predicate);
        return new SqlLambdaQuery<TEntity, TProjection>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 添加类型化交叉连接派生表并切换到双表 Lambda 查询描述。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    /// <returns>包含实体来源和派生表来源的双表查询描述。</returns>
    public SqlLambdaQuery<TEntity, TProjection> CrossJoin<TProjection>(SqlSubquery<TProjection> subquery)
        where TProjection : class
    {
        ((JoinClause)((ISqlQueryClauseAccessor)GetBuilder()).JoinClause).CrossJoin(subquery);
        return new SqlLambdaQuery<TEntity, TProjection>(Executor, GetBuilder(), false);
    }

    /// <summary>
    /// 使用实体属性表达式设置分组。
    /// </summary>
    /// <param name="column">分组字段表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> GroupBy(Expression<Func<TEntity, object>> column)
    {
        GetBuilder().GroupBy(column);
        return this;
    }

    /// <summary>
    /// 使用实体属性表达式设置多个分组字段。
    /// </summary>
    /// <param name="columns">分组字段表达式集合。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> GroupBy(params Expression<Func<TEntity, object>>[] columns)
    {
        GetBuilder().GroupBy(columns);
        return this;
    }

    /// <summary>
    /// 使用当前实体属性创建聚合投影。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Aggregate(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string columnAlias = null, bool distinct = false)
    {
        ReplaceSelect(select => select.Aggregate(function, column, columnAlias, distinct));
        return this;
    }

    /// <summary>
    /// 使用已成功配置的候选 Select 子句替换当前投影。
    /// </summary>
    /// <param name="configure">配置候选 Select 子句的操作。</param>
    private void ReplaceSelect(Action<ISelectClause> configure)
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        builder.ReplaceSelect(configure);
    }

    /// <summary>
    /// 获取支持类型化 Lambda 绑定的根来源子句。
    /// </summary>
    /// <returns>当前查询的 From 子句。</returns>
    private FromClause GetTypedFromClause() =>
        ((ISqlQueryClauseAccessor)GetBuilder()).FromClause as FromClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持类型化 Lambda 根来源。");

    /// <summary>
    /// 获取支持类型化 Lambda 绑定的连接子句。
    /// </summary>
    /// <returns>当前查询的 Join 子句。</returns>
    private JoinClause GetTypedJoinClause() =>
        ((ISqlQueryClauseAccessor)GetBuilder()).JoinClause as JoinClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持类型化 Lambda 连接。");

    /// <summary>
    /// 按当前实体属性排序。
    /// </summary>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> OrderBy(Expression<Func<TEntity, object>> column, bool desc = false)
    {
        GetBuilder().OrderBy(column, desc);
        return this;
    }

    /// <summary>
    /// 跳过指定数量的结果行。
    /// </summary>
    /// <param name="count">要跳过的行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Skip(int count)
    {
        GetBuilder().Skip(count);
        return this;
    }

    /// <summary>
    /// 限制返回的结果行数量。
    /// </summary>
    /// <param name="count">最多返回的行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Take(int count)
    {
        GetBuilder().Take(count);
        return this;
    }
}