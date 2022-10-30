using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Data.Queries;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象扩展 - 拼接Sql相关
/// </summary>
public static partial class Extensions
{
    #region Count(求总行数)

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Count<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Count(expression, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region Sum(求和)

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Sum<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Sum(expression, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region Avg(求平均值)

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Avg<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Avg(expression, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region Max(求最大值)

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Max<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Max(expression, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region Min(求最小值)

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Min<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Min(expression, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region Select(设置列名)

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public static ISqlQuery Select<TEntity>(this ISqlQuery sqlQuery, bool propertyAsAlias)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Select<TEntity>(propertyAsAlias);
        return sqlQuery;
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public static ISqlQuery Select<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Select(columns, propertyAsAlias);
        return sqlQuery;
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="column">列名，范例：t => t.A，支持字典批量设置列和列别名，
    /// 范例：Select&lt;Sample&gt;( t => new Dictionary&lt;object, string&gt; { { t.Email, "e" }, { t.Url, "u" } } );</param>
    /// <param name="columnAlias">列别名</param>
    public static ISqlQuery Select<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> column, string columnAlias = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Select(column, columnAlias);
        return sqlQuery;
    }

    #endregion

    #region RemoveSelect(移除列名)

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
    public static ISqlQuery RemoveSelect<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object[]>> columns)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.RemoveSelect(columns);
        return sqlQuery;
    }

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="column">列名，范例：t => t.A，支持字典批量设置列和列别名，
    /// 范例：Select&lt;Sample&gt;( t => new Dictionary&lt;object, string&gt; { { t.Email, "e" }, { t.Url, "u" } } );</param>
    public static ISqlQuery RemoveSelect<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> column)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.RemoveSelect(column);
        return sqlQuery;
    }

    #endregion

    #region From(设置表名)

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlQuery From<TEntity>(this ISqlQuery sqlQuery, string alias = null, string schema = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.From<TEntity>(alias, schema);
        return sqlQuery;
    }

    #endregion

    #region Join(内连接)

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlQuery Join<TEntity>(this ISqlQuery sqlQuery, string alias = null, string schema = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Join<TEntity>(alias, schema);
        return sqlQuery;
    }

    #endregion

    #region LeftJoin(左外连接)

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlQuery LeftJoin<TEntity>(this ISqlQuery sqlQuery, string alias = null, string schema = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.LeftJoin<TEntity>(alias, schema);
        return sqlQuery;
    }

    #endregion

    #region RightJoin(右外连接)

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    public static ISqlQuery RightJoin<TEntity>(this ISqlQuery sqlQuery, string alias = null, string schema = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.RightJoin<TEntity>(alias, schema);
        return sqlQuery;
    }

    #endregion

    #region On(设置连接条件)

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="left">左表列名</param>
    /// <param name="right">右表列名</param>
    /// <param name="operator">条件运算符</param>
    public static ISqlQuery On<TLeft, TRight>(this ISqlQuery sqlQuery, Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right, Operator @operator = Operator.Equal)
        where TLeft : class
        where TRight : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.On(left, right, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">条件表达式</param>
    public static ISqlQuery On<TLeft, TRight>(this ISqlQuery sqlQuery, Expression<Func<TLeft, TRight, bool>> expression)
        where TLeft : class
        where TRight : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.On(expression);
        return sqlQuery;
    }

    #endregion

    #region Or(Or连接条件)

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="conditions">查询条件</param>
    public static ISqlQuery Or<TEntity>(this ISqlQuery sqlQuery, params Expression<Func<TEntity, bool>>[] conditions)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Or(conditions);
        return sqlQuery;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public static ISqlQuery OrIf<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, bool>> predicate, bool condition)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.OrIf(predicate, condition);
        return sqlQuery;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="predicates">查询条件</param>
    public static ISqlQuery OrIf<TEntity>(this ISqlQuery sqlQuery, bool condition, params Expression<Func<TEntity, bool>>[] predicates)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.OrIf(condition, predicates);
        return sqlQuery;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="conditions">查询条件。如果表达式中的值为空，则忽略该查询条件</param>
    public static ISqlQuery OrIfNotEmpty<TEntity>(this ISqlQuery sqlQuery, params Expression<Func<TEntity, bool>>[] conditions)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.OrIfNotEmpty(conditions);
        return sqlQuery;
    }

    #endregion

    #region Where(设置查询条件)

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery Where<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Where(expression, value, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age > 1 )</param>
    public static ISqlQuery Where<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, bool>> expression)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Where(expression);
        return sqlQuery;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery Where<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, ISqlBuilder builder, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.Where(expression, builder, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="action">子查询操作</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery Where<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.Where(expression, action, @operator);
        return sqlQuery;
    }

    #endregion

    #region WhereIf(设置查询条件)

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery WhereIf<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value, bool condition, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.WhereIf(expression, value, condition, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age > 1 )</param>
    /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
    public static ISqlQuery WhereIf<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, bool>> expression, bool condition)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.WhereIf(expression, condition);
        return sqlQuery;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式,范例：t => t.Name</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery WhereIf<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, ISqlBuilder builder, bool condition, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.WhereIf(expression, builder, condition, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式,范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery WhereIf<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action, bool condition, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.WhereIf(expression, action, condition, @operator);
        return sqlQuery;
    }

    #endregion

    #region WhereIfNotEmpty(设置查询条件)

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
    /// <param name="operator">运算符</param>
    public static ISqlQuery WhereIfNotEmpty<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.WhereIfNotEmpty(expression, value, @operator);
        return sqlQuery;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
    public static ISqlQuery WhereIfNotEmpty<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, bool>> expression)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.WhereIfNotEmpty(expression);
        return sqlQuery;
    }

    #endregion

    #region Equal(设置相等查询条件)

    /// <summary>
    /// 设置相等查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Equal<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Equal(expression, value);
        return sqlQuery;
    }

    #endregion

    #region NotEqual(设置不相等查询条件)

    /// <summary>
    /// 设置不相等查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery NotEqual<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.NotEqual(expression, value);
        return sqlQuery;
    }

    #endregion

    #region Greater(设置大于查询条件)

    /// <summary>
    /// 设置大于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Greater<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Greater(expression, value);
        return sqlQuery;
    }

    #endregion

    #region GreaterEqual(设置大于等于查询条件)

    /// <summary>
    /// 设置大于等于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery GreaterEqual<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.GreaterEqual(expression, value);
        return sqlQuery;
    }

    #endregion

    #region Less(设置小于查询条件)

    /// <summary>
    /// 设置小于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Less<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Less(expression, value);
        return sqlQuery;
    }

    #endregion

    #region LessEqual(设置小于等于查询条件)

    /// <summary>
    /// 设置小于等于查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery LessEqual<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.LessEqual(expression, value);
        return sqlQuery;
    }

    #endregion

    #region Contains(设置模糊匹配查询条件)

    /// <summary>
    /// 设置模糊匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Contains<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Contains(expression, value);
        return sqlQuery;
    }

    #endregion

    #region Starts(设置头匹配查询条件)

    /// <summary>
    /// 设置头匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Starts<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Starts(expression, value);
        return sqlQuery;
    }

    #endregion

    #region Ends(设置尾匹配查询条件)

    /// <summary>
    /// 设置尾匹配查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="value">值</param>
    public static ISqlQuery Ends<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, object value)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Ends(expression, value);
        return sqlQuery;
    }

    #endregion

    #region IsNull(设置Is Null查询条件)

    /// <summary>
    /// 设置Is Null查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    public static ISqlQuery IsNull<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.IsNull(expression);
        return sqlQuery;
    }

    #endregion

    #region IsNotNull(设置Is Not Null查询条件)

    /// <summary>
    /// 设置Is Not Null查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    public static ISqlQuery IsNotNull<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.IsNotNull(expression);
        return sqlQuery;
    }

    #endregion

    #region IsEmpty(设置空条件)

    /// <summary>
    /// 设置空条件，范例：[Name] Is Null Or [Name]=''
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    public static ISqlQuery IsEmpty<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.IsEmpty(expression);
        return sqlQuery;
    }

    #endregion

    #region IsNotEmpty(设置非空条件)

    /// <summary>
    /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    public static ISqlQuery IsNotEmpty<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression) where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.IsNotEmpty(expression);
        return sqlQuery;
    }

    #endregion

    #region In(设置In条件)

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="values">值集合</param>
    public static ISqlQuery In<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, IEnumerable<object> values)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.In(expression, values);
        return sqlQuery;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="builder">Sql生成器</param>
    public static ISqlQuery In<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, ISqlBuilder builder)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.In(expression, builder);
        return sqlQuery;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    public static ISqlQuery In<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.In(expression, action);
        return sqlQuery;
    }

    #endregion

    #region NotIn(设置Not In条件)

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式</param>
    /// <param name="values">值集合</param>
    public static ISqlQuery NotIn<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, IEnumerable<object> values)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.NotIn(expression, values);
        return sqlQuery;
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="builder">Sql生成器</param>
    public static ISqlQuery NotIn<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, ISqlBuilder builder)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.NotIn(expression, builder);
        return sqlQuery;
    }

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="action">子查询操作</param>
    public static ISqlQuery NotIn<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action)
        where TEntity : class
    {
        var sqlBuilder = sqlQuery.GetBuilder();
        sqlBuilder.NotIn(expression, action);
        return sqlQuery;
    }

    #endregion

    #region Between(设置范围查询条件)

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, int? min, int? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, boundary);
        return sqlQuery;
    }

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, long? min, long? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, boundary);
        return sqlQuery;
    }

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, float? min, float? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, boundary);
        return sqlQuery;
    }

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, double? min, double? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, boundary);
        return sqlQuery;
    }

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, decimal? min, decimal? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, boundary);
        return sqlQuery;
    }

    /// <summary>
    /// 设置范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="expression">列名表达式，范例：t => t.Name</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <param name="boundary">包含边界</param>
    public static ISqlQuery Between<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.Between(expression, min, max, includeTime, boundary);
        return sqlQuery;
    }

    #endregion

    #region GroupBy(分组)

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="columns">分组字段</param>
    public static ISqlQuery GroupBy<TEntity>(this ISqlQuery sqlQuery, params Expression<Func<TEntity, object>>[] columns)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.GroupBy(columns);
        return sqlQuery;
    }

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="column">分组字段，范例：t => t.Name</param>
    /// <param name="having">分组条件，范例：Count(*) > 1</param>
    public static ISqlQuery GroupBy<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> column, string having = null)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.GroupBy(column, having);
        return sqlQuery;
    }

    #endregion

    #region OrderBy(排序)

    /// <summary>
    /// 排序
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="column">排序列</param>
    /// <param name="desc">是否倒序</param>
    public static ISqlQuery OrderBy<TEntity>(this ISqlQuery sqlQuery, Expression<Func<TEntity, object>> column, bool desc = false)
        where TEntity : class
    {
        var builder = sqlQuery.GetBuilder();
        builder.OrderBy(column, desc);
        return sqlQuery;
    }

    #endregion

    #region Page(分页)

    /// <summary>
    /// 设置跳过行数
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="count">跳过的行数</param>
    public static ISqlQuery Skip(this ISqlQuery sqlQuery, int count)
    {
        var builder = sqlQuery.GetBuilder();
        builder.Skip(count);
        return sqlQuery;
    }

    /// <summary>
    /// 设置获取行数
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="count">获取的行数</param>
    public static ISqlQuery Take(this ISqlQuery sqlQuery, int count)
    {
        var builder = sqlQuery.GetBuilder();
        builder.Take(count);
        return sqlQuery;
    }

    #endregion
}