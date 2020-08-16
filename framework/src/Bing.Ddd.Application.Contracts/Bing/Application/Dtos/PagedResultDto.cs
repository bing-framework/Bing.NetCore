using System;
using System.Collections.Generic;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 分页结果 - 数据传输对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [Serializable]
    public class PagedResultDto<T> : ListResultDto<T>, IPagedResult<T>
    {
        /// <summary>
        /// 总行数
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// 初始化一个<see cref="PagedResultDto{T}"/>类型的实例
        /// </summary>
        public PagedResultDto() { }

        /// <summary>
        /// 初始化一个<see cref="PagedResultDto{T}"/>类型的实例
        /// </summary>
        /// <param name="totalCount">总行数</param>
        /// <param name="items">列表</param>
        public PagedResultDto(long totalCount, IReadOnlyList<T> items) : base(items) => TotalCount = totalCount;
    }
}
