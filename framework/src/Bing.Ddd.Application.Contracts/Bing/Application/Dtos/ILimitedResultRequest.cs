namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义限制结果返回数量请求
    /// </summary>
    public interface ILimitedResultRequest
    {
        /// <summary>
        /// 最大结果数量。
        /// 通常用于限制分页上的结果数量
        /// </summary>
        int MaxResultCount { get; set; }
    }
}
