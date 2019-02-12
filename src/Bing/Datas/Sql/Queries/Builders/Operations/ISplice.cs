using System;
using System.Linq.Expressions;
using Bing.Datas.Sql.Builders;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置拼接条件
    /// </summary>
    public interface ISplice
    {
        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlBuilder On(string left, string right, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="left">左表列名，范例：t => t.Name</param>
        /// <param name="right">右表列名，范例：t => t.Name</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
            Operator @operator = Operator.Equal) where TLeft : class where TRight : class;

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="expression">条件表达式，范例：(l,r) => l.Id == r.Id</param>
        /// <returns></returns>
        ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
            where TLeft : class where TRight : class;

        /// <summary>
        /// And连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder And(ICondition condition);

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Or(ICondition condition);

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions) where TEntity : class;

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件，如果表达式中的值为空，泽忽略该查询条件</param>
        /// <returns></returns>
        ISqlBuilder OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions) where TEntity : class;
    }
}
