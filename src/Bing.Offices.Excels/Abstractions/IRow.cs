using System.Collections.Generic;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元行
    /// </summary>
    public interface IRow
    {
        /// <summary>
        /// 单元格列表
        /// </summary>
        IList<ICell> Cells { get; }

        /// <summary>
        /// 单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        ICell this[int columnIndex] { get; }

        /// <summary>
        /// 当前行索引
        /// </summary>
        int RowIndex { get; set; }

        /// <summary>
        /// 列数。返回所在行的列数量，合并的列算1列
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// 列数。返回所在行的列数量，按拆分的列算
        /// </summary>
        int ColumnSpanCount { get; }

        /// <summary>
        /// 工作表
        /// </summary>
        IWorkSheet Sheet { get; }

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="columnSpan">列跨度</param>
        /// <param name="rowSpan">行跨度</param>
        void Add(object value, int columnSpan = 1, int rowSpan = 1);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        void Add(ICell cell);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cells">单元格列表</param>
        void Add(IList<ICell> cells);

        /// <summary>
        /// 清空内容
        /// </summary>
        /// <returns></returns>
        IRow ClearContent();

        /// <summary>
        /// 设置行高
        /// </summary>
        /// <param name="height">高度</param>
        void SetHeight(int height);

        /// <summary>
        /// 获取行高
        /// </summary>
        /// <returns></returns>
        int GetHeight();
    }
}
