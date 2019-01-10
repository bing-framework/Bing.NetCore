using System;
using Bing.Datas.Sql.Queries.Builders.Abstractions;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    /// <summary>
    /// Sql查询执行上下文
    /// </summary>
    public class SqlQueryContext
    {
        /// <summary>
        /// 实体别名注册器
        /// </summary>
        public IEntityAliasRegister EntityAliasRegister { get; }

        /// <summary>
        /// Where子句
        /// </summary>
        public IWhereClause WhereClause { get; }

        /// <summary>
        /// 初始化一个<see cref="SqlQueryContext"/>类型的实例
        /// </summary>
        /// <param name="entityAliasRegister">实体别名注册器</param>
        /// <param name="whereClause">Where子句</param>
        public SqlQueryContext(IEntityAliasRegister entityAliasRegister, IWhereClause whereClause)
        {
            EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
            WhereClause = whereClause ?? throw new ArgumentNullException(nameof(whereClause));
        }
    }
}
