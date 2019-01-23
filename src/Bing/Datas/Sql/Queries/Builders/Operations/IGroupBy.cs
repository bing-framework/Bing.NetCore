using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置分组
    /// </summary>
    public interface IGroupBy
    {
        /// <summary>
        /// 是否分组
        /// </summary>
        bool IsGroup { get; }

        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="columns">分组字段，范例：a.Id,b.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlBuilder GroupBy(string columns, string having = null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">分组字段</param>
        /// <returns></returns>
        ISqlBuilder GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段，范例：t => t.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlBuilder GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null)
            where TEntity : class;

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendGroupBy(string sql);
    }
}
