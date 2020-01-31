using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 修改时间
    /// </summary>
    public interface IModificationTime
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        DateTime? LastModificationTime { get; set; }
    }
}
