using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // 聚合函数
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 过滤重复记录
        /// </summary>
        /// <returns></returns>
        public virtual ISqlBuilder Distinct()
        {
            SelectClause.Distinct();
            return this;
        }

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Count(string columnAlias = null)
        {
            SelectClause.Count(columnAlias);
            return this;
        }

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Count(string column, string columnAlias)
        {
            SelectClause.Count(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
        {
            SelectClause.Count(expression,columnAlias);
            return this;
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Sum(string column, string columnAlias = null)
        {
            SelectClause.Sum(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
        {
            SelectClause.Sum(expression, columnAlias);
            return this;
        }

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Avg(string column, string columnAlias = null)
        {
            SelectClause.Avg(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
        {
            SelectClause.Avg(expression, columnAlias);
            return this;
        }

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Max(string column, string columnAlias = null)
        {
            SelectClause.Max(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
        {
            SelectClause.Max(expression, columnAlias);
            return this;
        }

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Min(string column, string columnAlias = null)
        {
            SelectClause.Min(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
        {
            SelectClause.Min(expression, columnAlias);
            return this;
        }
    }
}
