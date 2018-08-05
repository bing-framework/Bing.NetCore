namespace Bing.Offices.Abstractions
{
    /// <summary>
    /// 区域范围信息
    /// </summary>
    public interface IRegionRangeInfo
    {
        /// <summary>
        /// 起始行
        /// </summary>
        int FirstRow { get; set; }

        /// <summary>
        /// 结束行
        /// </summary>
        int LastRow { get; set; }

        /// <summary>
        /// 起始列
        /// </summary>
        int FirstColumn { get; set; }

        /// <summary>
        /// 结束列
        /// </summary>
        int LastColumn { get; set; }
    }
}
