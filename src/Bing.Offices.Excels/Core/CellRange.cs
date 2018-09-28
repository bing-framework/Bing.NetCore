using Bing.Offices.Excels.Abstractions;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 单元格范围
    /// </summary>
    public class CellRange:ICellRange
    {
        /// <summary>
        /// 起始行索引
        /// </summary>
        public int FirstRow { get; set; }

        /// <summary>
        /// 结束行索引
        /// </summary>
        public int LastRow { get; set; }

        /// <summary>
        /// 起始列索引
        /// </summary>
        public int FirstColumn { get; set; }

        /// <summary>
        /// 结束列索引
        /// </summary>
        public int LastColumn { get; set; }

        /// <summary>
        /// 初始化一个<see cref="CellRange"/>类型的实例
        /// </summary>
        public CellRange()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="CellRange"/>类型的实例
        /// </summary>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        public CellRange(int firstRow, int lastRow, int firstColumn, int lastColumn)
        {
            FirstRow = firstRow;
            LastRow = lastRow;
            FirstColumn = firstColumn;
            LastColumn = lastColumn;
        }
    }
}
