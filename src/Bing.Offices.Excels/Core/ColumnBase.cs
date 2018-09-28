using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 单元列基类
    /// </summary>
    public abstract class ColumnBase:IColumn
    {
        /// <summary>
        /// 单元格列表
        /// </summary>
        public IList<ICell> Cells { get; protected set; }

        /// <summary>
        /// 单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public ICell this[int rowIndex] => Cells[rowIndex];

        /// <summary>
        /// 设置列宽
        /// </summary>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        public abstract IColumn SetWidth(int width);

        /// <summary>
        /// 获取列宽
        /// </summary>
        /// <returns></returns>
        public abstract int GetWidth();

        /// <summary>
        /// 设置单元列的样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public abstract IColumn SetStyle(CellStyle style);
    }
}
