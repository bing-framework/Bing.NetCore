using System;
using System.Linq.Expressions;
using Bing.Datas.Sql.Builders;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置查询条件
    /// </summary>
    public interface IWhere
    {
        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Where(ICondition condition);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age>1 )</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="builder">子查询Sql生成器</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where(string column, ISqlBuilder builder, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="builder">子查询Sql生成器</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, ISqlBuilder builder,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="action">子查询操作</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where(string column, Action<ISqlBuilder> action, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="action">子查询操作</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 添加到Where子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendWhere(string sql);
    }
}
