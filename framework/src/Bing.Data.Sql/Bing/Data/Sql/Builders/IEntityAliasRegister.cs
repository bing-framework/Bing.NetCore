using System;
using System.Collections.Generic;

namespace Bing.Data.Sql.Builders
{
    /// <summary>
    /// 实体别名注册器
    /// </summary>
    public interface IEntityAliasRegister
    {
        /// <summary>
        /// From子句设置的实体类型
        /// </summary>
        Type FromType { get; set; }

        /// <summary>
        /// 实体别名
        /// </summary>
        IDictionary<Type, string> Data { get; }

        /// <summary>
        /// 注册实体别名
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <param name="alias">别名</param>
        void Register(Type entity, string alias);

        /// <summary>
        /// 是否包含实体
        /// </summary>
        /// <param name="entity">实体类型</param>
        bool Contains(Type entity);

        /// <summary>
        /// 获取实体别名
        /// </summary>
        /// <param name="entity">实体类型</param>
        string GetAlias(Type entity);

        /// <summary>
        /// 克隆
        /// </summary>
        IEntityAliasRegister Clone();
    }
}
