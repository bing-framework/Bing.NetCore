namespace Bing.Domains.Repositories
{
    /// <summary>
    /// 分页基类
    /// </summary>
    public interface IPagerBase
    {
        /// <summary>
        /// 页数，即第几页，从1开始
        /// </summary>
        int Page { get; set; }

        /// <summary>
        /// 每页显示行数
        /// </summary>
        int PageSize { get; set; }

        /// <summary>
        /// 总行数
        /// </summary>
        int TotalCount { get; set; }
    }
}
