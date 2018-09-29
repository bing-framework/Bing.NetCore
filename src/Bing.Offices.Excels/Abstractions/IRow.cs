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
        List<ICell> Cells { get; }

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
        IWorkSheet Sheet { get; set; }

        /// <summary>
        /// 行高
        /// </summary>
        short Height { get; set; }

        /// <summary>
        /// 创建单元格
        /// </summary>
        /// <returns></returns>
        ICell CreateCell();

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        ICell GetCell(int columnIndex);

        /// <summary>
        /// 获取或创建单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        ICell GetOrCreateCell(int columnIndex);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="value">值</param>
        void Add(object value);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        void Add(ICell cell);

        /// <summary>
        /// 清空内容
        /// </summary>
        /// <returns></returns>
        void ClearContent();
    }
}
