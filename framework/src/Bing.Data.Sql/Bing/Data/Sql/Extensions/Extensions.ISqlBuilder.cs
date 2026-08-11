using System.Linq.Expressions;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql生成器扩展
/// </summary>
public static partial class Extensions
{
    #region Select子句

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="alias">聚合结果别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static ISqlBuilder Count<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        string alias = null, bool distinct = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Count(expression, alias, distinct);
        return source;
    }

    /// <summary>
    /// 添加实体表达式聚合列。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="function">聚合函数。</param>
    /// <param name="expression">列名表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder Aggregate<TEntity>(this ISqlBuilder source, SqlAggregateFunction function,
        Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Aggregate(function, expression, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static ISqlBuilder Sum<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Sum(expression, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static ISqlBuilder Avg<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Avg(expression, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static ISqlBuilder Max<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Max(expression, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static ISqlBuilder Min<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Min(expression, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public static ISqlBuilder Select<TEntity>(this ISqlBuilder source, bool propertyAsAlias = false)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Select<TEntity>(propertyAsAlias);
        return source;
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="columns">列名。范例：t => new object[] { t.Id, t.Name }</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public static ISqlBuilder Select<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Select(columns, propertyAsAlias);
        return source;
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="column">列名。范例：t => t.Name，支持字典批量设置列和列别名，
    /// 范例：Select&lt;Sample&gt;( t => new Dictionary&lt;object, string&gt; { { t.Email, "e" }, { t.Url, "u" } } );</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlBuilder Select<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> column,
        string columnAlias = null)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.Select(column, columnAlias);
        return source;
    }

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="columns">列名。范例：t => new object[] { t.Id, t.Name }</param>
    public static ISqlBuilder RemoveSelect<TEntity>(this ISqlBuilder source,
        Expression<Func<TEntity, object[]>> columns) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.RemoveSelect(columns);
        return source;
    }

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="column">列名。范例：t => t.Name，支持字典批量设置列和列别名</param>
    public static ISqlBuilder RemoveSelect<TEntity>(this ISqlBuilder source,
        Expression<Func<TEntity, object>> column) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.SelectClause.RemoveSelect(column);
        return source;
    }

    #endregion

    #region From子句

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlBuilder From<TEntity>(this ISqlBuilder source, string alias = null, string schema = null)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.FromClause.From<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 设置结构化表引用。
    /// </summary>
    /// <param name="source">Sql生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>Sql生成器。</returns>
    public static ISqlBuilder From(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.FromClause.From(reference);
        return source;
    }

    #endregion

    #region Join子句

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlBuilder Join<TEntity>(this ISqlBuilder source, string alias = null, string schema = null)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.Join<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 内连接结构化表引用。
    /// </summary>
    /// <param name="source">Sql生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>Sql生成器。</returns>
    public static ISqlBuilder Join(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.Join(reference);
        return source;
    }

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlBuilder LeftJoin<TEntity>(this ISqlBuilder source, string alias = null, string schema = null)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.LeftJoin<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 左外连接结构化表引用。
    /// </summary>
    /// <param name="source">Sql生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>Sql生成器。</returns>
    public static ISqlBuilder LeftJoin(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.LeftJoin(reference);
        return source;
    }

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlBuilder RightJoin<TEntity>(this ISqlBuilder source, string alias = null, string schema = null)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.RightJoin<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 右外连接结构化表引用。
    /// </summary>
    /// <param name="source">Sql生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>Sql生成器。</returns>
    public static ISqlBuilder RightJoin(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.RightJoin(reference);
        return source;
    }

    /// <summary>
    /// 添加实体全外连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder FullJoin<TEntity>(this ISqlBuilder source)
        where TEntity : class => FullJoin<TEntity>(source, null, null);

    /// <summary>
    /// 添加带别名的实体全外连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="alias">表别名。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder FullJoin<TEntity>(this ISqlBuilder source, string alias)
        where TEntity : class => FullJoin<TEntity>(source, alias, null);

    /// <summary>
    /// 添加实体全外连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="alias">表别名。</param>
    /// <param name="schema">架构名。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder FullJoin<TEntity>(this ISqlBuilder source, string alias, string schema)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.FullJoin<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 添加结构化全外连接表引用。
    /// </summary>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder FullJoin(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.FullJoin(reference);
        return source;
    }

    /// <summary>
    /// 添加实体交叉连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder CrossJoin<TEntity>(this ISqlBuilder source)
        where TEntity : class => CrossJoin<TEntity>(source, null, null);

    /// <summary>
    /// 添加带别名的实体交叉连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="alias">表别名。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder CrossJoin<TEntity>(this ISqlBuilder source, string alias)
        where TEntity : class => CrossJoin<TEntity>(source, alias, null);

    /// <summary>
    /// 添加实体交叉连接表。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="alias">表别名。</param>
    /// <param name="schema">架构名。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder CrossJoin<TEntity>(this ISqlBuilder source, string alias, string schema)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.CrossJoin<TEntity>(alias, schema);
        return source;
    }

    /// <summary>
    /// 添加结构化交叉连接表引用。
    /// </summary>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="reference">结构化表引用。</param>
    /// <returns>SQL 生成器。</returns>
    public static ISqlBuilder CrossJoin(this ISqlBuilder source, SqlTableReference reference)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.CrossJoin(reference);
        return source;
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="source">Sql生成器</param>
    /// <param name="left">左表列名,范例：t => t.Name</param>
    /// <param name="right">右表列名,范例：t => t.Name</param>
    /// <param name="operator">条件运算符</param>
    public static ISqlBuilder On<TLeft, TRight>(this ISqlBuilder source, Expression<Func<TLeft, object>> left,
        Expression<Func<TRight, object>> right,
        Operator @operator = Operator.Equal) where TLeft : class where TRight : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.On(left, right, @operator);
        return source;
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">条件表达式,范例：(l,r) => l.Id == r.OrderId</param>
    public static ISqlBuilder On<TLeft, TRight>(this ISqlBuilder source,
        Expression<Func<TLeft, TRight, bool>> expression) where TLeft : class where TRight : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.JoinClause.On(expression);
        return source;
    }

    #endregion

    #region Where子句

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="conditions">查询条件</param>
    public static ISqlBuilder Or<TEntity>(this ISqlBuilder source, params Expression<Func<TEntity, bool>>[] conditions)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Or(conditions);
        return source;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public static ISqlBuilder OrIf<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, bool>> predicate, bool condition)
        where TEntity : class =>
        OrIf(source, condition, predicate);

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="predicates">查询条件</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public static ISqlBuilder OrIf<TEntity>(this ISqlBuilder source, bool condition, params Expression<Func<TEntity, bool>>[] predicates)
        where TEntity : class =>
        condition ? source.Or(predicates) : source;

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="conditions">查询条件,如果表达式中的值为空，则忽略该查询条件</param>
    public static ISqlBuilder OrIfNotEmpty<TEntity>(this ISqlBuilder source, params Expression<Func<TEntity, bool>>[] conditions)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.OrIfNotEmpty(conditions);
        return source;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public static ISqlBuilder Where<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        object value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Where(expression, value, @operator);
        return source;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">查询条件表达式。范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age > 1 )</param>
    public static ISqlBuilder Where<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, bool>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Where(expression);
        return source;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="operator">运算符</param>
    public static ISqlBuilder Where<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, ISqlBuilder builder,
        Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Where(expression, builder, @operator);
        return source;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="action">子查询操作</param>
    /// <param name="operator">运算符</param>
    public static ISqlBuilder Where<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression,
        Action<ISqlBuilder> action, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Where(expression, action, @operator);
        return source;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    /// <returns>条件成立时已追加条件的原 Builder；否则返回原 Builder。</returns>
    public static ISqlBuilder WhereIf<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value, bool condition, Operator @operator = Operator.Equal)
        where TEntity : class =>
        condition ? source.Where(expression, value, @operator) : source;

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">查询条件表达式,范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age > 1 )</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public static ISqlBuilder WhereIf<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, bool>> expression, bool condition)
        where TEntity : class =>
        condition ? source.Where(expression) : source;

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="subBuilder">子查询Sql生成器</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static ISqlBuilder WhereIf<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, ISqlBuilder subBuilder,
        bool condition, Operator @operator = Operator.Equal)
        where TEntity : class =>
        condition ? source.Where(expression, subBuilder, @operator) : source;

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    /// <returns>条件成立时已追加子查询条件的原 Builder；否则返回原 Builder。</returns>
    public static ISqlBuilder WhereIf<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action,
        bool condition, Operator @operator = Operator.Equal)
        where TEntity : class =>
        condition ? source.Where(expression, action, @operator) : source;

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值,如果值为空，则忽略该查询条件</param>
    /// <param name="operator">运算符</param>
    public static ISqlBuilder WhereIfNotEmpty<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.WhereIfNotEmpty(expression, value, @operator);
        return source;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">查询条件表达式。如果参数值为空，则忽略该查询条件</param>
    public static ISqlBuilder WhereIfNotEmpty<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, bool>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.WhereIfNotEmpty(expression);
        return source;
    }

    /// <summary>
    /// 设置相等查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Equal<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value);
    }

    /// <summary>
    /// 设置不相等查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder NotEqual<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.NotEqual);
    }

    /// <summary>
    /// 设置大于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Greater<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.Greater);
    }

    /// <summary>
    /// 设置小于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Less<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.Less);
    }

    /// <summary>
    /// 设置大于等于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder GreaterEqual<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.GreaterEqual);
    }

    /// <summary>
    /// 设置小于等于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder LessEqual<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.LessEqual);
    }

    /// <summary>
    /// 设置模糊匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Contains<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.Contains);
    }

    /// <summary>
    /// 设置头匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Starts<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.Starts);
    }

    /// <summary>
    /// 设置尾匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlBuilder Ends<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(expression, value, Operator.Ends);
    }

    /// <summary>
    /// 设置Is Null查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    public static ISqlBuilder IsNull<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.IsNull(expression);
        return source;
    }

    /// <summary>
    /// 设置Is Not Null查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    public static ISqlBuilder IsNotNull<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.IsNotNull(expression);
        return source;
    }

    /// <summary>
    /// 设置空条件，范例：[Name] Is Null Or [Name]=''
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    public static ISqlBuilder IsEmpty<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.IsEmpty(expression);
        return source;
    }

    /// <summary>
    /// 设置非空条件，范例：[Name] Is Not Null And [Name]&lt;&gt;''
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    public static ISqlBuilder IsNotEmpty<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.IsNotEmpty(expression);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="values">值集合</param>
    public static ISqlBuilder In<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, IEnumerable<object> values)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.In(expression, values);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="builder">Sql生成器</param>
    public static ISqlBuilder In<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, ISqlBuilder builder)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.In(expression, builder);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    public static ISqlBuilder In<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.In(expression, action);
        return source;
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="values">值集合</param>
    public static ISqlBuilder NotIn<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, IEnumerable<object> values)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.NotIn(expression, values);
        return source;
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="builder">Sql生成器</param>
    public static ISqlBuilder NotIn<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, ISqlBuilder builder)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.NotIn(expression, builder);
        return source;
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    public static ISqlBuilder NotIn<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.NotIn(expression, action);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, int? min, int? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, long? min, long? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, float? min, float? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, double? min, double? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, decimal? min, decimal? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="expression">列名表达式。范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlBuilder Between<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = Boundary.Both)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.WhereClause.Between(expression, min, max, includeTime, boundary);
        return source;
    }

    #endregion

    #region GroupBy子句

    /// <summary>
    /// 按实体属性表达式分组。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="column">分组字段。范例：a.Id,b.Name</param>
    public static ISqlBuilder GroupBy<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> column)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.GroupByClause.GroupBy(column);
        return source;
    }

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="columns">分组字段</param>
    public static ISqlBuilder GroupBy<TEntity>(this ISqlBuilder source, params Expression<Func<TEntity, object>>[] columns)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.GroupByClause.GroupBy(columns);
        return source;
    }

    #endregion

    #region OrderBy子句

    /// <summary>
    /// 排序
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="source">Sql生成器</param>
    /// <param name="column">排序列。范例：t => t.Name</param>
    /// <param name="desc">是否倒排</param>
    public static ISqlBuilder OrderBy<TEntity>(this ISqlBuilder source, Expression<Func<TEntity, object>> column, bool desc = false)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.OrderByClause.OrderBy(column, desc);
        return source;
    }

    #endregion
}
