using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Abstractions
{
    /// <summary>
    /// 分组子句
    /// </summary>
    public interface IGroupByClause
    {
        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="groupBy">分组列表</param>
        /// <param name="having">分组条件</param>
        void GroupBy(string groupBy,string having=null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段</param>
        /// <param name="having">分组条件</param>
        void GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null);

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        void AppendSql(string sql);

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <returns></returns>
        string ToSql();
    }
}
