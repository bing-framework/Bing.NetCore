using System.Collections.Generic;
using System.Drawing;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元行
    /// </summary>
    public interface IRow
    {
        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        bool Bold { get; set; }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        bool Italic { get; set; }

        /// <summary>
        /// 行索引。返回行所在索引
        /// </summary>
        int RowIndex { get; }

        /// <summary>
        /// 列数。返回所在行的列数量，合并的列算1列
        /// </summary>
        int ColCount { get; }

        /// <summary>
        /// 列数。返回所在行的列数量，合并的列算1列，按拆分的列算
        /// </summary>
        int ColSpanCount { get; }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        void SetFontStyle(Font font);

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetBackgroundColor(Color color);

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetFontColor(Color color);

        /// <summary>
        /// 设置高度
        /// </summary>
        /// <param name="height">高度</param>
        void SetHeight(int height);

        /// <summary>
        /// 获取单元格。返回指定位置单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        ICell GetCell(int columnIndex);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cell"></param>
        void AddCell(ICell cell);

        /// <summary>
        /// 获取单元格列表。返回该行所有单元格
        /// </summary>
        /// <returns></returns>
        IList<ICell> GetCells();

    }
}
