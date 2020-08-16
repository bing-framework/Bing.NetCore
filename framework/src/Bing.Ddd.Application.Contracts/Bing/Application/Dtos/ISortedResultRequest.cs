namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义排序请求
    /// </summary>
    public interface ISortedResultRequest
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
        string Sorting { get; set; }
    }
}
