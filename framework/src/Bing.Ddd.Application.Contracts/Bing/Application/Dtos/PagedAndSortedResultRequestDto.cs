using System;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 分页和排序请求 - 数据传输对象
    /// </summary>
    [Serializable]
    public class PagedAndSortedResultRequestDto : PagedResultRequestDto, IPagedAndSortedResultRequest
    {
        /// <summary>
        /// 排序信息。
        /// 应包含排序字段和排序方向(ASC 或 DESC)。
        /// 可以包含多个用逗号(,)分隔的字段.
        /// </summary>
        /// <example>
        /// "Name"
        /// "Name DESC"
        /// "Name ASC, Age DESC"
        /// </example>
        public virtual string Sorting { get; set; }
    }
}
