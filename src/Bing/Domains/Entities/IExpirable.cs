using System;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 有效时间
    /// </summary>
    public interface IExpirable
    {
        /// <summary>
        /// 生效时间
        /// </summary>
        DateTime? BeginTime { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        DateTime? EndTime { get; set; }
    }
}
