using Bing.Offices.Abstractions;

namespace Bing.Offices.Core.Properties
{
    /// <summary>
    /// 过滤器属性信息
    /// </summary>
    public class FilterPropertyInfo:IRegionRangeInfo
    {
        /// <summary>
        /// 起始行
        /// </summary>
        public int FirstRow { get; set; }

        /// <summary>
        /// 结束行，如果<see cref="LastRow"/>为空，则该值按代码动态计算
        /// </summary>
        public int LastRow { get; set; }

        /// <summary>
        /// 起始列
        /// </summary>
        public int FirstColumn { get; set; }

        /// <summary>
        /// 结束列
        /// </summary>
        public int LastColumn { get; set; }
    }
}
