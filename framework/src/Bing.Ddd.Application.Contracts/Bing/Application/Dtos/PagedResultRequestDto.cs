using System;
using System.ComponentModel.DataAnnotations;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 分页请求 - 数据传输对象
    /// </summary>
    [Serializable]
    public class PagedResultRequestDto : LimitedResultRequestDto, IPagedResultRequest
    {
        /// <summary>
        /// 跳过的行数
        /// </summary>
        [Range(0, int.MaxValue)]
        public virtual int SkipCount { get; set; }
    }
}
