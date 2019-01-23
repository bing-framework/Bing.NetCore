using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 聚合函数
    /// </summary>
    public interface IAggregateFunc
    {
        /// <summary>
        /// 过滤重复记录
        /// </summary>
        /// <returns></returns>
        ISqlBuilder Distinct();

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Count(string columnAlias = null);

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Count(string column, string columnAlias);

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Sum(string column, string columnAlias = null);

        /// <summary>
        /// 求和
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Avg(string column, string columnAlias = null);

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class;

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Max(string column, string columnAlias = null);

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class;

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Min(string column, string columnAlias = null);

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class;
    }
}
