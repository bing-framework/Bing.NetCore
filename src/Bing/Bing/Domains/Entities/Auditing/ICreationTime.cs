using System;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 创建时间
    /// </summary>
    public interface ICreationTime
    {
        /// <summary>
        /// 创建时间
        /// </summary>
        DateTime? CreationTime { get; set; }
    }
}
