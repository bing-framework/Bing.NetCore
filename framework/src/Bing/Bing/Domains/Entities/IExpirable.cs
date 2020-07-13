using System;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 有效期
    /// </summary>
    public interface IExpirable
    {
        /// <summary>
        /// 有效期 - 开始时间
        /// </summary>
        DateTime? ExpireLimitedFromTime { get; set; }

        /// <summary>
        /// 有效期 - 结束时间
        /// </summary>
        DateTime? ExpireLimitedToTime { get; set; }

        /// <summary>
        /// 是否开始
        /// </summary>
        bool IsStart();

        /// <summary>
        /// 是否开始
        /// </summary>
        /// <param name="targetTime">目标时间</param>
        bool IsStart(DateTime targetTime);

        /// <summary>
        /// 是否已过期
        /// </summary>
        bool IsExpired();

        /// <summary>
        /// 是否已过期
        /// </summary>
        /// <param name="targetTime">目标时间</param>
        bool IsExpired(DateTime targetTime);

        /// <summary>
        /// 是否生效
        /// </summary>
        bool IsActive();

        /// <summary>
        /// 是否生效
        /// </summary>
        /// <param name="targetTime">目标时间</param>
        bool IsActive(DateTime targetTime);
    }
}
