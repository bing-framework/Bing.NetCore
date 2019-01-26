using System;
using System.Linq.Expressions;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // Where(设置查询条件)
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where(ICondition condition)
        {
            WhereClause.Where(condition);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where(string column, object value, Operator @operator = Operator.Equal)
        {
            WhereClause.Where(column, value, @operator);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal) where TEntity : class
        {
            WhereClause.Where(expression, value, @operator);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class
        {
            WhereClause.Where(expression);
            return this;
        }

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="builder">子查询Sql生成器</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where(string column, ISqlBuilder builder, Operator @operator = Operator.Equal)
        {
            WhereClause.Where(column, builder, @operator);
            return this;
        }

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="builder">子查询Sql生成器</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, ISqlBuilder builder, Operator @operator = Operator.Equal) where TEntity : class
        {
            WhereClause.Where(expression, builder, @operator);
            return this;
        }

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="action">子查询操作</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where(string column, Action<ISqlBuilder> action, Operator @operator = Operator.Equal)
        {
            WhereClause.Where(column, action, @operator);
            return this;
        }

        /// <summary>
        /// 设置子查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="action">子查询操作</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, Action<ISqlBuilder> action, Operator @operator = Operator.Equal) where TEntity : class
        {
            WhereClause.Where(expression, action, @operator);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder WhereIfNotEmpty(string column, object value, Operator @operator = Operator.Equal)
        {
            WhereClause.WhereIfNotEmpty(column, value, @operator);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression, object value, Operator @operator = Operator.Equal) where TEntity : class
        {
            WhereClause.WhereIfNotEmpty(expression, value, @operator);
            return this;
        }

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class
        {
            WhereClause.WhereIfNotEmpty(expression);
            return this;
        }

        /// <summary>
        /// 添加到Where子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public virtual ISqlBuilder AppendWhere(string sql)
        {
            WhereClause.AppendSql(sql);
            return this;
        }
    }
}
