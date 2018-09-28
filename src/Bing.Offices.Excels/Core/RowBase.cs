using System.Collections.Generic;
using System.Linq;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Internal;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 单元行基类
    /// </summary>
    public abstract class RowBase : IRow
    {
        /// <summary>
        /// 索引管理器
        /// </summary>
        protected readonly IndexManager IndexManager;

        /// <summary>
        /// 单元格列表
        /// </summary>
        public IList<ICell> Cells { get; protected set; }

        /// <summary>
        /// 单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public ICell this[int columnIndex] => Cells[columnIndex];

        /// <summary>
        /// 当前行索引
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// 列数。返回所在行的列数量，合并的列算1列
        /// </summary>
        public int ColumnCount => Cells.Count;

        /// <summary>
        /// 列数。返回所在行的列数量，按拆分的列算
        /// </summary>
        public int ColumnSpanCount => Cells.Sum(x => x.ColumnSpan);

        /// <summary>
        /// 工作表
        /// </summary>
        public abstract IWorkSheet Sheet { get; }

        protected RowBase(int rowIndex)
        {
            Cells = new List<ICell>();
            RowIndex = rowIndex;
            IndexManager = new IndexManager();
        }

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="columnSpan">列跨度</param>
        /// <param name="rowSpan">行跨度</param>
        public abstract void Add(object value, int columnSpan = 1, int rowSpan = 1);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        public void Add(ICell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.Row = this;
            SetColumnIndex(cell);
            Cells.Add(cell);
        }

        /// <summary>
        /// 设置列索引
        /// </summary>
        /// <param name="cell">单元格</param>
        private void SetColumnIndex(ICell cell)
        {
            if (cell.ColumnIndex > 0)
            {
                IndexManager.AddIndex(cell.ColumnIndex, cell.ColumnSpan);
                return;
            }

            cell.ColumnIndex = IndexManager.GetIndex(cell.ColumnSpan);
        }

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="cells">单元格列表</param>
        public void Add(IList<ICell> cells)
        {
            foreach (var cell in cells)
            {
                Add(cell);
            }
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        /// <returns></returns>
        public IRow ClearContent()
        {
            foreach (var cell in Cells)
            {
                cell.SetValue(string.Empty);
            }
            return this;
        }

        /// <summary>
        /// 设置行高
        /// </summary>
        /// <param name="height">高度</param>
        public abstract void SetHeight(int height);

        /// <summary>
        /// 获取行高
        /// </summary>
        /// <returns></returns>
        public abstract int GetHeight();
    }
}
