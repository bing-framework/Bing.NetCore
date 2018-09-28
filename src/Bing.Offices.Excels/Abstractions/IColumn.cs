using System.Collections.Generic;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元列
    /// </summary>
    public interface IColumn
    {
        /// <summary>
        /// 单元格列表
        /// </summary>
        IList<ICell> Cells { get; }

        /// <summary>
        /// 单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        ICell this[int rowIndex] { get; }
        
        /// <summary>
        /// 设置列宽
        /// </summary>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        IColumn SetWidth(int width);

        /// <summary>
        /// 获取列宽
        /// </summary>
        /// <returns></returns>
        int GetWidth();

        /// <summary>
        /// 设置单元列的样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IColumn SetStyle(CellStyle style);
    }
}
