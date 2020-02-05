using System;
using Bing.Auditing;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 创建审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedWithNameEntity<TEntity> : CreationAuditedEntity<TEntity>, ICreationAuditedWithNameObject
        where TEntity : class, IEntity
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }
    }

    /// <summary>
    /// 创建审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedWithNameEntity<TEntity, TKey> : CreationAuditedEntity<TEntity, TKey>, ICreationAuditedWithNameObject<TKey>
        where TEntity : class, IEntity
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }
    }
}
