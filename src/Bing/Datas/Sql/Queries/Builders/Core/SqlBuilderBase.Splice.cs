using System;
using System.Linq.Expressions;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // 拼接条件
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder On(string left, string right, Operator @operator = Operator.Equal)
        {
            JoinClause.On(left, right, @operator);
            return this;
        }

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        public virtual ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right, Operator @operator = Operator.Equal) where TLeft : class where TRight : class
        {
            JoinClause.On(left, right, @operator);
            return this;
        }

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="expression">条件表达式</param>
        /// <returns></returns>
        public virtual ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression) where TLeft : class where TRight : class
        {
            JoinClause.On(expression);
            return this;
        }

        /// <summary>
        /// And连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder And(ICondition condition)
        {
            WhereClause.And(condition);
            return this;
        }

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder Or(ICondition condition)
        {
            WhereClause.Or(condition);
            return this;
        }

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions) where TEntity:class
        {
            WhereClause.Or(conditions);
            return this;
        }

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件，如果表达式中的值为空，泽忽略该查询条件</param>
        /// <returns></returns>
        public virtual ISqlBuilder OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions) where TEntity:class
        {
            WhereClause.OrIfNotEmpty(conditions);
            return this;
        }
    }
}
