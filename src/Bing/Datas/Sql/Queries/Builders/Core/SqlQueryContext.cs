using System;
using Bing.Datas.Matedatas;
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
        /// 实体元数据解析器
        /// </summary>
        public IEntityMatedata Matedata { get; }

        /// <summary>
        /// 初始化一个<see cref="SqlQueryContext"/>类型的实例
        /// </summary>
        /// <param name="entityAliasRegister">实体别名注册器</param>
        /// <param name="whereClause">Where子句</param>
        /// <param name="matedata">实体元数据解析器</param>
        public SqlQueryContext(IEntityAliasRegister entityAliasRegister, IWhereClause whereClause,IEntityMatedata matedata)
        {
            EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
            WhereClause = whereClause ?? throw new ArgumentNullException(nameof(whereClause));
            Matedata = matedata;
        }
    }
}
