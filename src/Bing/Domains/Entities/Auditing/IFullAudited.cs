using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 全部操作审计
    /// </summary>
    public interface IFullAudited : IFullAudited<Guid?>
    {
    }

    /// <summary>
    /// 全部审计操作
    /// </summary>
    /// <typeparam name="TKey">操作人编号类型</typeparam>
    public interface IFullAudited<TKey>:IAudited<TKey>,IDeletionAudited<TKey>
    {
    }
}
