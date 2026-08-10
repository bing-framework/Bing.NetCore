using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用实体映射构建的强类型 Lambda SQL 查询描述。
/// </summary>
/// <typeparam name="TEntity">查询结果和实体映射类型。</typeparam>
/// <remarks>
/// 此类型仅用于实体查询，避免标量 <see cref="SqlQuery{TResult}"/> 承担实体表达式约束。
/// </remarks>
public sealed class SqlLambdaQuery<TEntity> : SqlQuery<TEntity> where TEntity : class
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
        GetBuilder().ClearSelect().Select<TEntity>(propertyAsAlias);
        return this;
    }

    /// <summary>
    /// 使用当前实体类型设置投影列。
    /// </summary>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false)
    {
        GetBuilder().ClearSelect().Select(columns, propertyAsAlias);
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
        GetBuilder().ClearSelect().Select(column, columnAlias);
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
    /// 追加指定实体类型的投影列。
    /// </summary>
    /// <typeparam name="TSelect">投影所属实体类型。</typeparam>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> SelectFrom<TSelect>(Expression<Func<TSelect, object[]>> columns,
        bool propertyAsAlias = false) where TSelect : class
    {
        GetBuilder().ClearSelect().Select(columns, propertyAsAlias);
        return this;
    }

    /// <summary>
    /// 在当前投影后追加指定实体类型的投影列。
    /// </summary>
    /// <typeparam name="TSelect">投影所属实体类型。</typeparam>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将实体属性映射为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> AppendSelectFrom<TSelect>(Expression<Func<TSelect, object[]>> columns,
        bool propertyAsAlias = false) where TSelect : class
    {
        GetBuilder().Select(columns, propertyAsAlias);
        return this;
    }

    /// <summary>
    /// 使用当前实体类型设置来源表。
    /// </summary>
    /// <param name="alias">来源表别名。</param>
    /// <param name="schema">数据库架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> From(string alias = null, string schema = null)
    {
        GetBuilder().From<TEntity>(alias, schema);
        return this;
    }

    /// <summary>
    /// 使用指定实体类型设置来源表。
    /// </summary>
    /// <typeparam name="TSource">来源实体类型。</typeparam>
    /// <param name="alias">来源表别名。</param>
    /// <param name="schema">数据库架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> From<TSource>(string alias = null, string schema = null) where TSource : class
    {
        GetBuilder().From<TSource>(alias, schema);
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
    /// 追加指定实体的布尔筛选表达式。
    /// </summary>
    /// <typeparam name="TSource">筛选所属实体类型。</typeparam>
    /// <param name="predicate">实体筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereFrom<TSource>(Expression<Func<TSource, bool>> predicate)
        where TSource : class
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
    /// 追加指定实体的参数化属性筛选条件。
    /// </summary>
    /// <typeparam name="TSource">筛选所属实体类型。</typeparam>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Where<TSource>(Expression<Func<TSource, object>> column, object value,
        Operator @operator = Operator.Equal) where TSource : class
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).WhereClause.Where(column, value, @operator);
        return this;
    }

    /// <summary>
    /// 按条件追加指定实体的参数化属性筛选条件。
    /// </summary>
    /// <typeparam name="TSource">筛选所属实体类型。</typeparam>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="condition">是否追加筛选条件。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIf<TSource>(Expression<Func<TSource, object>> column, object value,
        bool condition, Operator @operator = Operator.Equal) where TSource : class
    {
        GetBuilder().WhereIf(column, value, condition, @operator);
        return this;
    }

    /// <summary>
    /// 当指定实体的筛选值非空时追加参数化条件。
    /// </summary>
    /// <typeparam name="TSource">筛选所属实体类型。</typeparam>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="value">比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIfNotEmpty<TSource>(Expression<Func<TSource, object>> column, object value,
        Operator @operator = Operator.Equal) where TSource : class
    {
        GetBuilder().WhereIfNotEmpty(column, value, @operator);
        return this;
    }

    /// <summary>
    /// 当指定实体的布尔筛选表达式非空时追加条件。
    /// </summary>
    /// <typeparam name="TSource">筛选所属实体类型。</typeparam>
    /// <param name="predicate">实体筛选表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> WhereIfNotEmpty<TSource>(Expression<Func<TSource, bool>> predicate)
        where TSource : class
    {
        GetBuilder().WhereIfNotEmpty(predicate);
        return this;
    }

    /// <summary>
    /// 添加类型化内连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Join<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        GetBuilder().Join<TJoin>(alias, schema);
        return this;
    }

    /// <summary>
    /// 添加类型化左外连接表。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名称。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> LeftJoin<TJoin>(string alias = null, string schema = null) where TJoin : class
    {
        GetBuilder().LeftJoin<TJoin>(alias, schema);
        return this;
    }

    /// <summary>
    /// 为最后一个连接设置类型化条件。
    /// </summary>
    /// <typeparam name="TLeft">左侧实体类型。</typeparam>
    /// <typeparam name="TRight">右侧实体类型。</typeparam>
    /// <param name="expression">连接条件表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
        where TLeft : class where TRight : class
    {
        GetBuilder().On(expression);
        return this;
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
    /// 设置受信任的原始 Having 条件。
    /// </summary>
    /// <param name="sql">Having SQL 条件；外部输入必须通过参数 API 提供。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> HavingRaw(string sql)
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).GroupByClause.HavingRaw(sql);
        return this;
    }

    /// <summary>
    /// 设置 Having 条件，并按当前方言解析方括号标识符。
    /// </summary>
    /// <param name="sql">Having SQL 条件；外部输入必须通过参数 API 提供。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> Having(string sql)
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).GroupByClause.Having(sql);
        return this;
    }

    /// <summary>
    /// 使用当前实体属性投影切换结果映射类型。
    /// </summary>
    /// <typeparam name="TResult">投影结果类型。</typeparam>
    /// <param name="columns">实体属性投影表达式。</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名。</param>
    /// <returns>使用投影结果类型的查询描述。</returns>
    public SqlQuery<TResult> Select<TResult>(Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false)
    {
        GetBuilder().ClearSelect().Select(columns, propertyAsAlias);
        return WithResult<TResult>();
    }

    /// <summary>
    /// 使用当前实体属性创建聚合投影，并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TResult">聚合结果类型。</typeparam>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>使用聚合结果类型的查询描述。</returns>
    public SqlQuery<TResult> Aggregate<TResult>(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string columnAlias = null, bool distinct = false)
    {
        GetBuilder().ClearSelect().Aggregate(function, column, columnAlias, distinct);
        return WithResult<TResult>();
    }

    /// <summary>
    /// 保持当前 SQL 结构并切换结果映射类型。
    /// </summary>
    /// <typeparam name="TResult">结果映射类型。</typeparam>
    /// <returns>使用指定结果映射类型的查询描述。</returns>
    public SqlQuery<TResult> As<TResult>() => WithResult<TResult>();

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
    /// 按指定实体属性排序。
    /// </summary>
    /// <typeparam name="TSource">排序所属实体类型。</typeparam>
    /// <param name="column">实体属性表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery<TEntity> OrderBy<TSource>(Expression<Func<TSource, object>> column, bool desc = false)
        where TSource : class
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