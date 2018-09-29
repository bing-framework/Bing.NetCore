namespace Bing.Offices.Excels.Mappings.Abstractions
{
    /// <summary>
    /// 筛选映射
    /// </summary>
    public interface IFilterMap
    {
        /// <summary>
        /// 首行索引
        /// </summary>
        int FirstRow { get; }

        /// <summary>
        /// 最后一行索引。如果<see cref="LastRow"/>为空，则该值按代码动态计算
        /// </summary>
        int? LastRow { get; }

        /// <summary>
        /// 首列索引
        /// </summary>
        int FirstColumn { get; }

        /// <summary>
        /// 最后一列索引
        /// </summary>
        int LastColumn { get; }
    }
}
