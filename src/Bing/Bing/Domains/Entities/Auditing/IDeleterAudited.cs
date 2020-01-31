using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 删除人操作审计
    /// </summary>
    public interface IDeleterAudited : IDeleterAudited<Guid?>
    {
    }

    /// <summary>
    /// 删除人操作审计
    /// </summary>
    /// <typeparam name="TKey">删除人标识类型</typeparam>
    public interface IDeleterAudited<TKey> : IDeletionAudited<TKey>, IDeleter
    {
    }
}
