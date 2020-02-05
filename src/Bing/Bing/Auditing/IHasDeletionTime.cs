using System;
using Bing.Domains.Entities;

namespace Bing.Auditing
{
    /// <summary>
    /// 删除时间
    /// </summary>
    public interface IHasDeletionTime: IDelete
    {
        /// <summary>
        /// 删除时间
        /// </summary>
        DateTime? DeletionTime { get; set; }
    }
}
