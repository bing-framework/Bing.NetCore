namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元格范围
    /// </summary>
    public interface ICellRange
    {
        /// <summary>
        /// 起始行索引
        /// </summary>
        int FirstRow { get; set; }

        /// <summary>
        /// 结束行索引
        /// </summary>
        int LastRow { get; set; }

        /// <summary>
        /// 起始列索引
        /// </summary>
        int FirstColumn { get; set; }

        /// <summary>
        /// 结束列索引
        /// </summary>
        int LastColumn { get; set; }
    }
}
