using System;
using System.ComponentModel.DataAnnotations;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 限制结果返回数量请求 - 数据传输对象
    /// </summary>
    [Serializable]
    public class LimitedResultRequestDto : ILimitedResultRequest
    {
        /// <summary>
        /// 默认结果数量
        /// </summary>
        public static int DefaultMaxResultCount { get; set; } = 10;

        /// <summary>
        /// 最大结果数量
        /// </summary>
        public static int MaxMaxResultCount { get; set; } = 1000;

        /// <summary>
        /// 最大结果数量。
        /// 通常用于限制分页上的结果数量
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxResultCount { get; set; } = DefaultMaxResultCount;

    }
}
