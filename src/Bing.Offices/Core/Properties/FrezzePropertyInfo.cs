using Bing.Offices.Abstractions;

namespace Bing.Offices.Core.Properties
{
    /// <summary>
    /// 冻结属性信息
    /// </summary>
    public class FrezzePropertyInfo: IRegionRangeInfo
    {
        /// <summary>
        /// 起始行
        /// </summary>
        public int FirstRow { get; set; } = -1;

        /// <summary>
        /// 结束行
        /// </summary>
        public int LastRow { get; set; } = 1;

        /// <summary>
        /// 起始列
        /// </summary>
        public int FirstColumn { get; set; } = 0;

        /// <summary>
        /// 结束列
        /// </summary>
        public int LastColumn { get; set; } = 0;
    }
}
