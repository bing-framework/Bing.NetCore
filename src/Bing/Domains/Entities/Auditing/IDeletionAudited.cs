using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 删除操作审计
    /// </summary>
    public interface IDeletionAudited : IDeletionAudited<Guid?>
    {
    }

    /// <summary>
    /// 删除操作审计
    /// </summary>
    /// <typeparam name="TKey">删除人编号类型</typeparam>
    public interface IDeletionAudited<TKey> : IDeletionTime
    {
        /// <summary>
        /// 删除人编号
        /// </summary>
        TKey DeleterId { get; set; }
    }
}
