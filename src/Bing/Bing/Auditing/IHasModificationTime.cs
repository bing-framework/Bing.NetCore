using System;

namespace Bing.Auditing
{
    /// <summary>
    /// 修改时间
    /// </summary>
    public interface IHasModificationTime
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        DateTime? LastModificationTime { get; set; }
    }
}
