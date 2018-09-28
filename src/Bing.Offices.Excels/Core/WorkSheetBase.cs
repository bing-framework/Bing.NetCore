using System.Collections.Generic;
using System.Linq;
using Bing.Offices.Excels.Abstractions;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 工作表基类
    /// </summary>
    public abstract class WorkSheetBase:IWorkSheet
    {
        /// <summary>
        /// 头部单元范围
        /// </summary>
        protected IRange Header;

        /// <summary>
        /// 正文单元范围
        /// </summary>
        protected IRange Body;

        /// <summary>
        /// 底部单元范围
        /// </summary>
        protected IRange Footer;

        /// <summary>
        /// 当前行索引
        /// </summary>
        protected int RowIndex;

        /// <summary>
        /// 总标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 工作表名
        /// </summary>
        public string SheetName { get; protected set; }

        /// <summary>
        /// 列数
        /// </summary>
        public int ColumnCount => Body?.ColumnCount ?? Header.ColumnCount;

        /// <summary>
        /// 总行数
        /// </summary>
        public int RowCount => HeadRowCount + BodyRowCount + FootRowCount;

        /// <summary>
        /// 表头行数
        /// </summary>
        public int HeadRowCount => Header.RowCount;

        /// <summary>
        /// 正文行数
        /// </summary>
        public int BodyRowCount => Body.RowCount;

        /// <summary>
        /// 页脚行数
        /// </summary>
        public int FootRowCount => Footer.RowCount;

        /// <summary>
        /// 单元行列表
        /// </summary>
        public abstract IList<IRow> Rows { get; }

        /// <summary>
        /// 单元行
        /// </summary>
        /// <param name="rowIndex">列索引</param>
        /// <returns></returns>
        public IRow this[int rowIndex] => Rows[rowIndex];

        /// <summary>
        /// 当前工作表索引
        /// </summary>
        public int SheetIndex { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActived { get; protected set; }

        /// <summary>
        /// 工作簿
        /// </summary>
        public IWorkbook Workbook { get; set; }

        /// <summary>
        /// 获取表头
        /// </summary>
        /// <returns></returns>
        public IList<IRow> GetHeaders()
        {
            return Header.GetRows();
        }

        /// <summary>
        /// 获取表格正文
        /// </summary>
        /// <returns></returns>
        public IList<IRow> GetBodys()
        {
            return Body == null ? new List<IRow>() : Body.GetRows();
        }

        /// <summary>
        /// 获取页脚
        /// </summary>
        /// <returns></returns>
        public IList<IRow> GetFooter()
        {
            return Footer == null ? new List<IRow>() : Footer.GetRows();
        }

        /// <summary>
        /// 创建单元格
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        protected abstract ICell CreateCell(object value);

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="titles">标题</param>
        public void AddHeadRow(params string[] titles)
        {
            if (titles == null)
            {
                return;
            }
            AddHeadRow(titles.Select(CreateCell).ToArray());
        }

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="cells">单元格</param>
        public void AddHeadRow(params ICell[] cells)
        {
            if (cells == null)
            {
                return;
            }
            AddRowToHeader(cells);
            ResetFirstColumnSpan();
        }

        /// <summary>
        /// 添加表头行
        /// </summary>
        /// <param name="cells">单元格集合</param>
        private void AddRowToHeader(IEnumerable<ICell> cells)
        {
            Header.AddRow(RowIndex, cells);
            RowIndex++;
        }

        /// <summary>
        /// 重置第一行的列跨度，第一行可能为总标题
        /// </summary>
        private void ResetFirstColumnSpan()
        {
            if (RowIndex < 2)
            {
                return;
            }

            if (Header.RowCount == 0)
            {
                return;
            }

            if (Header[0].ColumnSpanCount > 1)
            {
                return;
            }

            if (Header.RowCount > 1)
            {
                Header[0][0].ColumnSpan = Header[1].ColumnSpanCount;
                return;
            }

            if (Body == null || Body.RowCount == 0)
            {
                return;
            }
            Header[0][0].ColumnSpan = Header[0].ColumnSpanCount;
        }

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cellValues">值</param>
        public void AddBodyRow(params object[] cellValues)
        {
            if (cellValues == null)
            {
                return;
            }
            AddBodyRow(cellValues.Select(CreateCell));
        }

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cells">单元格集合</param>
        public void AddBodyRow(IEnumerable<ICell> cells)
        {
            if (cells == null)
            {
                return;
            }

            GetBodyRange().AddRow(RowIndex, cells);
            RowIndex++;
            ResetFirstColumnSpan();
        }

        /// <summary>
        /// 获取正文单元格范围
        /// </summary>
        /// <returns></returns>
        protected abstract IRange GetBodyRange();

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cellValues">值</param>
        public void AddFootRow(params string[] cellValues)
        {
            if (cellValues == null)
            {
                return;
            }
            AddFootRow(cellValues.Select(CreateCell).ToArray());
        }

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cells">单元格集合</param>
        public void AddFootRow(params ICell[] cells)
        {
            if (cells == null)
            {
                return;
            }
            GetFootRange().AddRow(RowIndex,cells);
            RowIndex++;
        }

        /// <summary>
        /// 获取页脚单元范围
        /// </summary>
        /// <returns></returns>
        protected abstract IRange GetFootRange();

        /// <summary>
        /// 清空表头
        /// </summary>
        public void ClearHeader()
        {
            Header.Clear();
        }

        /// <summary>
        /// 获取单元列，按列索引
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public abstract IColumn Column(int columnIndex);

        /// <summary>
        /// 获取单元列，按列名称，如:A,B,AC
        /// </summary>
        /// <param name="columnName">列名称</param>
        /// <returns></returns>
        public abstract IColumn Column(string columnName);

        /// <summary>
        /// 获取单元行，按行索引
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public abstract IRow Row(int rowIndex);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：A11,B5
        /// </summary>
        /// <param name="cellPos">单元格坐标，如：A11,B5</param>
        /// <returns></returns>
        public abstract ICell Cell(string cellPos);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(0,11),(1,5)
        /// </summary>
        /// <param name="x">单元格横坐标</param>
        /// <param name="y">单元格纵坐标</param>
        /// <returns></returns>
        public abstract ICell Cell(int x, int y);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(A,11),(B,5)
        /// </summary>
        /// <param name="x">单元格横坐标</param>
        /// <param name="y">单元格纵坐标</param>
        /// <returns></returns>
        public abstract ICell Cell(string x, int y);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(0,11,0,11)
        /// </summary>
        /// <param name="x1">起始横坐标</param>
        /// <param name="y1">起始纵坐标</param>
        /// <param name="x2">结束横坐标</param>
        /// <param name="y2">结束纵坐标</param>
        /// <returns></returns>
        public abstract ICell Cell(int x1, int y1, int x2, int y2);

        /// <summary>
        /// 获取单元格，按单元格范围
        /// </summary>
        /// <param name="range">单元格范围</param>
        /// <returns></returns>
        public abstract ICell Cell(ICellRange range);

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="cellPos">单元格坐标</param>
        /// <returns></returns>
        public abstract object GetCellValue(string cellPos);

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public abstract object GetCelLValue(int rowIndex, int columnIndex);
    }
}
