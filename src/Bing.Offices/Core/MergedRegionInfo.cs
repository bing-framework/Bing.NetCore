namespace Bing.Offices.Core
{
    /// <summary>
    /// 合并区域信息
    /// </summary>
    public class MergedRegionInfo
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 起始行
        /// </summary>
        public int FirstRow { get; set; }

        /// <summary>
        /// 结束行
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

        /// <summary>
        /// 初始化一个<see cref="MergedRegionInfo"/>类型的实例
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="firstRow">起始行</param>
        /// <param name="lastRow">结束行</param>
        /// <param name="firstColumn">起始列</param>
        /// <param name="lastColumn">结束列</param>
        public MergedRegionInfo(int index, int firstRow, int lastRow, int firstColumn, int lastColumn)
        {
            Index = index;
            FirstRow = firstRow;
            LastRow = lastRow;
            FirstColumn = firstColumn;
            LastColumn = lastColumn;
        }
    }
}
