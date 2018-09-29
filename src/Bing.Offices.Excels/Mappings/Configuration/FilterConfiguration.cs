using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// 筛选配置。表示指定实体的Excel筛选配置
    /// </summary>
    public class FilterConfiguration:IFilterMap
    {
        /// <summary>
        /// 首行索引
        /// </summary>
        public int FirstRow { get; internal set; }

        /// <summary>
        /// 最后一行索引。如果<see cref="LastRow"/>为空，则该值按代码动态计算
        /// </summary>
        public int? LastRow { get; internal set; } = null;

        /// <summary>
        /// 首列索引
        /// </summary>
        public int FirstColumn { get; internal set; }

        /// <summary>
        /// 最后一列索引
        /// </summary>
        public int LastColumn { get; internal set; }
    }
}
