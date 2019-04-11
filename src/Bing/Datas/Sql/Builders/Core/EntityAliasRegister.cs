using System;
using System.Collections.Generic;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// 实体别名注册器
    /// </summary>
    public class EntityAliasRegister:IEntityAliasRegister
    {
        #region 属性

        /// <summary>
        /// From子句设置的实体类型
        /// </summary>
        public Type FromType { get; set; }

        /// <summary>
        /// 实体别名
        /// </summary>
        public IDictionary<Type, string> Data { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="EntityAliasRegister"/>类型的实例
        /// </summary>
        public EntityAliasRegister(IDictionary<Type, string> data = null, Type fromType = null)
        {
            Data = data ?? new Dictionary<Type, string>();
        }

        #endregion

        #region Register(注册实体别名)

        /// <summary>
        /// 注册实体别名
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <param name="alias">别名</param>
        public void Register(Type entity, string alias)
        {
            if (Data.ContainsKey(entity))
            {
                Data.Remove(entity);
            }
            Data[entity] = alias;
        }

        #endregion

        #region Contains(是否包含实体)

        /// <summary>
        /// 是否包含实体
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <returns></returns>
        public bool Contains(Type entity)
        {
            return entity != null && Data.ContainsKey(entity);
        }

        #endregion

        #region GetAlias(获取实体别名)

        /// <summary>
        /// 获取实体别名
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <returns></returns>
        public string GetAlias(Type entity)
        {
            if (entity == null)
            {
                return null;
            }

            return Data.ContainsKey(entity) ? Data[entity] : null;
        }

        #endregion

        #region Clone(克隆)

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public IEntityAliasRegister Clone()
        {
            return new EntityAliasRegister(new Dictionary<Type, string>(Data));
        }

        #endregion
    }
}
