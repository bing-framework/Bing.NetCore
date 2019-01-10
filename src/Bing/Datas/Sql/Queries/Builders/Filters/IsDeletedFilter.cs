using System;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Core;
using Bing.Domains.Entities;

namespace Bing.Datas.Sql.Queries.Builders.Filters
{
    /// <summary>
    /// 逻辑删除过滤器
    /// </summary>
    public class IsDeletedFilter : ISqlFilter
    {
        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="context">Sql查询执行上下文</param>
        public void Filter(SqlQueryContext context)
        {
            foreach (var item in context.EntityAliasRegister.Data)
            {
                Filter(context.WhereClause, item.Key, item.Value);
            }
        }

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="whereClause">Where子句</param>
        /// <param name="type">类型</param>
        /// <param name="alias">表别名</param>
        private void Filter(IWhereClause whereClause, Type type, string alias)
        {
            if (type == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            if (typeof(IDelete).IsAssignableFrom(type))
            {
                whereClause.Where($"{alias}.IsDeleted", false);
            }
        }
    }
}
