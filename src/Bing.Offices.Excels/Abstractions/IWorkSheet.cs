using System.Collections.Generic;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 工作表
    /// </summary>
    public interface IWorkSheet
    {
        /// <summary>
        /// 总标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// 工作表名
        /// </summary>
        string SheetName { get; }

        /// <summary>
        /// 列数
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// 总行数
        /// </summary>
        int RowCount { get; }

        /// <summary>
        /// 表头行数
        /// </summary>
        int HeadRowCount { get; }

        /// <summary>
        /// 正文行数
        /// </summary>
        int BodyRowCount { get; }

        /// <summary>
        /// 页脚行数
        /// </summary>
        int FootRowCount { get; }

        /// <summary>
        /// 单元行列表
        /// </summary>
        IList<IRow> Rows { get; }

        /// <summary>
        /// 单元行
        /// </summary>
        /// <param name="rowIndex">列索引</param>
        /// <returns></returns>
        IRow this[int rowIndex] { get; }

        /// <summary>
        /// 当前工作表索引
        /// </summary>
        int SheetIndex { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        bool IsActived { get; }

        /// <summary>
        /// 工作簿
        /// </summary>
        IWorkbook Workbook { get; set; }

        /// <summary>
        /// 获取表头
        /// </summary>
        /// <returns></returns>
        IList<IRow> GetHeaders();

        /// <summary>
        /// 获取表格正文
        /// </summary>
        /// <returns></returns>
        IList<IRow> GetBodys();

        /// <summary>
        /// 获取页脚
        /// </summary>
        /// <returns></returns>
        IList<IRow> GetFooter();

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="titles">标题</param>
        void AddHeadRow(params string[] titles);

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="cells">单元格</param>
        void AddHeadRow(params ICell[] cells);

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cellValues">值</param>
        void AddBodyRow(params object[] cellValues);

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cells">单元格集合</param>
        void AddBodyRow(IEnumerable<ICell> cells);

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cellValues">值</param>
        void AddFootRow(params string[] cellValues);

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cells">单元格集合</param>
        void AddFootRow(params ICell[] cells);

        /// <summary>
        /// 清空表头
        /// </summary>
        void ClearHeader();

        /// <summary>
        /// 获取单元列，按列索引
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        IColumn Column(int columnIndex);

        /// <summary>
        /// 获取单元列，按列名称，如:A,B,AC
        /// </summary>
        /// <param name="columnName">列名称</param>
        /// <returns></returns>
        IColumn Column(string columnName);

        /// <summary>
        /// 获取单元行，按行索引
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        IRow Row(int rowIndex);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：A11,B5
        /// </summary>
        /// <param name="cellPos">单元格坐标，如：A11,B5</param>
        /// <returns></returns>
        ICell Cell(string cellPos);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(0,11),(1,5)
        /// </summary>
        /// <param name="x">单元格横坐标</param>
        /// <param name="y">单元格纵坐标</param>
        /// <returns></returns>
        ICell Cell(int x, int y);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(A,11),(B,5)
        /// </summary>
        /// <param name="x">单元格横坐标</param>
        /// <param name="y">单元格纵坐标</param>
        /// <returns></returns>
        ICell Cell(string x, int y);

        /// <summary>
        /// 获取单元格，按单元格坐标，如：(0,11,0,11)
        /// </summary>
        /// <param name="x1">起始横坐标</param>
        /// <param name="y1">起始纵坐标</param>
        /// <param name="x2">结束横坐标</param>
        /// <param name="y2">结束纵坐标</param>
        /// <returns></returns>
        ICell Cell(int x1, int y1, int x2, int y2);

        /// <summary>
        /// 获取单元格，按单元格范围
        /// </summary>
        /// <param name="range">单元格范围</param>
        /// <returns></returns>
        ICell Cell(ICellRange range);

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="cellPos">单元格坐标</param>
        /// <returns></returns>
        object GetCellValue(string cellPos);

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        object GetCelLValue(int rowIndex, int columnIndex);
    }
}
