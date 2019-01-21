using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置排序
    /// </summary>
    public interface IOrderBy
    {
        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表，范例：a.Id, b.Name desc</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlBuilder OrderBy(string order, string tableAlias = null);

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列，范例：t => t.Name</param>
        /// <param name="desc">是否降序</param>
        /// <returns></returns>
        ISqlBuilder OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

        /// <summary>
        /// 添加到OrderBy子句
        /// </summary>
        /// <param name="sql">排序列表，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendOrderBy(string sql);
    }
}
