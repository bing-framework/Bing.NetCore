using System;
using Bing.Judgments;

namespace Bing
{
    /// <summary>
    /// <see cref="Guid"/> 扩展
    /// </summary>
    public static class GuidExtensions
    {
        /// <summary>
        /// 是否为空
        /// </summary>
        /// <param name="guid">值</param>
        public static bool IsNullOrEmpty(this Guid? guid) => GuidJudgment.IsNullOrEmpty(guid);

        /// <summary>
        /// 是否为空
        /// </summary>
        /// <param name="guid">值</param>
        public static bool IsNullOrEmpty(this Guid guid) => GuidJudgment.IsNullOrEmpty(guid);
    }
}
