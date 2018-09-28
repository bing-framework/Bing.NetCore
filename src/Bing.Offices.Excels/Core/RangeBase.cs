using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 单元范围基类
    /// </summary>
    public abstract class RangeBase:IRange
    {
        /// <summary>
        /// 单元行列表
        /// </summary>
        private readonly List<IRow> _rows;

        /// <summary>
        /// 起始行索引
        /// </summary>
        private readonly int _startIndex;

        /// <summary>
        /// 单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public IRow this[int rowIndex] => _rows[rowIndex];

        /// <summary>
        /// 列数
        /// </summary>
        public int ColumnCount => _rows.Count > 0 ? _rows[0].ColumnSpanCount : 0;

        /// <summary>
        /// 行数
        /// </summary>
        public int RowCount => _rows.Count;

        /// <summary>
        /// 初始化一个<see cref="RangeBase"/>类型的实例
        /// </summary>
        /// <param name="startIndex">起始行索引</param>
        protected RangeBase(int startIndex = 0)
        {
            _rows = new List<IRow>();
            _startIndex = startIndex;
        }

        /// <summary>
        /// 获取单元行
        /// </summary>
        /// <param name="rowIndex">行索引，对应Excel表格行号</param>
        /// <returns></returns>
        public IRow GetRow(int rowIndex)
        {
            var realIndex = rowIndex - _startIndex;
            if (realIndex < 0)
            {
                return null;
            }

            if (realIndex > _rows.Count - 1)
            {
                return null;
            }

            return _rows[realIndex];
        }

        /// <summary>
        /// 获取单元行列表
        /// </summary>
        /// <returns></returns>
        public IList<IRow> GetRows()
        {
            return _rows;
        }

        /// <summary>
        /// 添加单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="cells">单元格列表</param>
        public void AddRow(int rowIndex, IEnumerable<ICell> cells)
        {
            if (cells == null)
            {
                return;
            }

            var row = CreateRow(rowIndex);
            foreach (var cell in cells)
            {
                AddCell(row, cell, rowIndex);
            }
        }

        /// <summary>
        /// 创建单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        protected abstract IRow CreateRow(int rowIndex);

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="row">单元行</param>
        /// <param name="cell">单元格</param>
        /// <param name="rowIndex">行索引</param>
        protected void AddCell(IRow row, ICell cell, int rowIndex)
        {
            row.Add(cell);
            if (cell.RowSpan <= 1)
            {
                return;
            }

            for (var i = 1; i < cell.RowSpan; i++)
            {
                AddPlaceholderCell(cell, rowIndex + i);
            }
        }

        /// <summary>
        /// 为下方受RowSpan影响的单元行添加占位单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="rowIndex">行索引</param>
        protected abstract void AddPlaceholderCell(ICell cell, int rowIndex);

        /// <summary>
        /// 清空单元行
        /// </summary>
        public void Clear()
        {
            _rows.Clear();
        }
    }
}
