namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义总行数
    /// </summary>
    public interface IHasTotalCount
    {
        /// <summary>
        /// 总行数
        /// </summary>
        long TotalCount { get; set; }
    }
}
