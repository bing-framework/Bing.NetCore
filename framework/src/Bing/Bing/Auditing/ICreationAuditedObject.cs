using System;

namespace Bing.Auditing
{
    /// <summary>
    /// 创建操作审计对象
    /// </summary>
    public interface ICreationAuditedObject : ICreationAuditedObject<Guid?> { }

    /// <summary>
    /// 创建操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface ICreationAuditedObject<TKey> : IHasCreationTime
    {
        /// <summary>
        /// 创建人标识
        /// </summary>
        TKey CreatorId { get; set; }
    }
}
