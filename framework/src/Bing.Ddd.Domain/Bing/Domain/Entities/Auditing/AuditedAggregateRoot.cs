using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{

    [Serializable]
    public class AuditedAggregateRoot<TEntity, TKey> : CreationAuditedAggregateRoot<TEntity, TKey>, IAuditedObject<TKey>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public TKey LastModifierId { get; set; }
    }
}
