using System;
using Bing.Data;

namespace Bing.Auditing
{
    /// <summary>
    /// 删除时间
    /// </summary>
    public interface IHasDeletionTime : ISoftDelete
    {
        /// <summary>
        /// 删除时间
        /// </summary>
        DateTime? DeletionTime { get; set; }
    }
}
