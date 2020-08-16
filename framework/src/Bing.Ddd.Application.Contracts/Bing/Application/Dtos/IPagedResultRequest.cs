namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义分页请求
    /// </summary>
    public interface IPagedResultRequest : ILimitedResultRequest
    {
        /// <summary>
        /// 跳过的行数
        /// </summary>
        int SkipCount { get; set; }
    }
}
