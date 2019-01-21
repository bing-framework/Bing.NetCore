using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // OrderBy(设置排序)
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表，范例：a.Id, b.Name desc</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder OrderBy(string order, string tableAlias = null)
        {
            OrderByClause.OrderBy(order, tableAlias);
            return this;
        }

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列</param>
        /// <param name="desc">是否降序</param>
        /// <returns></returns>
        public virtual ISqlBuilder OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false)
        {
            OrderByClause.OrderBy(column, desc);
            return this;
        }

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表</param>
        /// <returns></returns>
        public virtual ISqlBuilder AppendOrderBy(string order)
        {
            OrderByClause.AppendSql(order);
            return this;
        }
    }
}
