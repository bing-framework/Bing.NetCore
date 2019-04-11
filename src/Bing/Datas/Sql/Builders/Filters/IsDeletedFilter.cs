using System;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Matedatas;
using Bing.Domains.Entities;

namespace Bing.Datas.Sql.Builders.Filters
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
        public void Filter(SqlContext context)
        {
            foreach (var item in context.EntityAliasRegister.Data)
            {
                Filter(context.Dialect, context.Matedata, context.EntityAliasRegister,
                    context.ClauseAccessor.JoinClause, context.ClauseAccessor.WhereClause, item.Key, item.Value);
            }
        }

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="matedata">实体元数据解析器</param>
        /// <param name="register">实体别名注册器</param>
        /// <param name="join">Join子句</param>
        /// <param name="where">Where子句</param>
        /// <param name="type">类型</param>
        /// <param name="alias">表别名</param>
        private void Filter(IDialect dialect, IEntityMatedata matedata, IEntityAliasRegister register, IJoinClause join,
            IWhereClause where, Type type, string alias)
        {
            if (type == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            if (typeof(IDelete).IsAssignableFrom(type) == false)
            {
                return;
            }

            var isDeleted = $"{dialect.SafeName(alias)}.{dialect.SafeName(matedata.GetColumn(type, "IsDeleted"))}";
            if (register.FromType == type)
            {
                where.Where(isDeleted, false);
                return;
            }

            join.Find(type)?.On(isDeleted, false);
        }
    }
}
