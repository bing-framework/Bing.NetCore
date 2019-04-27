using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 删除时间审计
    /// </summary>
    public interface IDeletionTime : IDelete
    {
        /// <summary>
        /// 删除时间
        /// </summary>
        DateTime? DeletionTime { get; set; }
    }
}
