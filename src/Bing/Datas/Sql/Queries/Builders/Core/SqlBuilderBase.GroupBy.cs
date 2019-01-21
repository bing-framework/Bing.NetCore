using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // GroupBy(设置分组)
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="group">分组字段</param>
        /// <param name="having">分组条件</param>
        /// <returns></returns>
        public ISqlBuilder GroupBy(string @group, string having = null)
        {
            GroupByClause.GroupBy(group, having);
            return this;
        }

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">分组字段</param>
        /// <returns></returns>
        public ISqlBuilder GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns)
        {
            GroupByClause.GroupBy(columns);
            return this;
        }

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段</param>
        /// <param name="having">分组条件</param>
        /// <returns></returns>
        public ISqlBuilder GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null) where TEntity : class
        {
            GroupByClause.GroupBy(column, having);
            return this;
        }

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public ISqlBuilder AppendGroupBy(string sql)
        {
            GroupByClause.AppendSql(sql);
            return this;
        }
    }
}
