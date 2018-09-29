using System;
using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Core.Styles;
using Bing.Offices.Excels.Npoi.Resolvers;
using ICell = Bing.Offices.Excels.Abstractions.ICell;
using IWorkbook = Bing.Offices.Excels.Abstractions.IWorkbook;

namespace Bing.Offices.Excels.Npoi
{
    /// <summary>
    /// Excel 操作基类
    /// </summary>
    public abstract class ExcelBase:IExcel
    {
        #region 字段

        /// <summary>
        /// 工作簿
        /// </summary>
        private NPOI.SS.UserModel.IWorkbook _workbook;

        /// <summary>
        /// 工作表
        /// </summary>
        private NPOI.SS.UserModel.ISheet _sheet;

        /// <summary>
        /// 当前单元行
        /// </summary>
        private NPOI.SS.UserModel.IRow _row;

        /// <summary>
        /// 当前单元格
        /// </summary>
        private NPOI.SS.UserModel.ICell _cell;

        /// <summary>
        /// 日期格式
        /// </summary>
        private string _dateFormat;

        /// <summary>
        /// 表头样式
        /// </summary>
        private NPOI.SS.UserModel.ICellStyle _headStyle;

        /// <summary>
        /// 正文样式
        /// </summary>
        private NPOI.SS.UserModel.ICellStyle _bodyStyle;

        /// <summary>
        /// 页脚样式
        /// </summary>
        private NPOI.SS.UserModel.ICellStyle _footStyle;

        /// <summary>
        /// 日期样式
        /// </summary>
        private NPOI.SS.UserModel.ICellStyle _dateStyle;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ExcelBase"/>类型的实例
        /// </summary>
        protected ExcelBase()
        {
            _dateFormat = "yyyy-mm-dd";
            CreateWorkbook().CreateSheet();
        }

        #endregion

        #region CreateWorkbook(创建工作簿)

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <returns></returns>
        public IExcel CreateWorkbook()
        {
            _workbook = CreateInternalWorkbook();
            return this;
        }

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="filePath">文件路径，绝对路径</param>
        /// <returns></returns>
        public IExcel CreateWorkbook(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                _workbook = CreateInternalWorkbook(fileStream);
            }
            return this;
        }

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="stream">文件流，传递过来的创建的工作簿对象</param>
        /// <returns></returns>
        public IExcel CreateWorkbook(Stream stream)
        {
            _workbook = CreateInternalWorkbook(stream);
            return this;
        }

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <returns></returns>
        protected abstract NPOI.SS.UserModel.IWorkbook CreateInternalWorkbook();

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="stream">内存流</param>
        /// <returns></returns>
        protected abstract NPOI.SS.UserModel.IWorkbook CreateInternalWorkbook(Stream stream);

        #endregion

        #region CreateSheet(创建工作表)

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public IExcel CreateSheet(string sheetName = "")
        {
            _sheet = string.IsNullOrWhiteSpace(sheetName) ? _workbook.CreateSheet() : _workbook.CreateSheet(sheetName);
            return this;
        }

        #endregion

        #region CreateRow(创建行)

        /// <summary>
        /// 创建行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public IExcel CreateRow(int rowIndex)
        {
            _row = GetOrCreateRow(rowIndex);
            return this;
        }

        /// <summary>
        /// 获取行，如果不存在则创建
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        private NPOI.SS.UserModel.IRow GetOrCreateRow(int rowIndex)
        {
            return _sheet.GetRow(rowIndex) ?? _sheet.CreateRow(rowIndex);
        }

        #endregion

        #region CreateCell(创建单元格)

        /// <summary>
        /// 创建单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        public IExcel CreateCell(ICell cell)
        {
            if (cell == null)
            {
                return this;
            }

            _cell = GetOrCreateCell(_row, cell.ColumnIndex);
            SetCellValue(cell.Value);
            MergeCell(cell);
            return this;
        }

        /// <summary>
        /// 获取单元格，如果不存在则创建
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="columnIndex">单元格索引</param>
        /// <returns></returns>
        private NPOI.SS.UserModel.ICell GetOrCreateCell(NPOI.SS.UserModel.IRow row, int columnIndex)
        {
            return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
        }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        private void SetCellValue(object value)
        {
            if (value == null)
            {
                return;
            }
            switch (value.GetType().ToString())
            {
                case "System.String":
                    _cell.SetCellValue(value.ToString());
                    break;
                case "System.DateTime":
                    _cell.SetCellValue(Convert.ToDateTime(value));
                    _cell.CellStyle = CreateDateStyle();
                    break;
                case "System.Boolean":
                    _cell.SetCellValue(Convert.ToBoolean(value));
                    break;
                case "System.Byte":
                case "System.Int16":
                case "System.Int32":
                case "System.Int64":
                    _cell.SetCellValue(Convert.ToInt32(value));
                    break;
                case "System.Single":
                case "System.Double":
                case "System.Decimal":
                    _cell.SetCellValue(Convert.ToDouble(value));
                    break;
                default:
                    _cell.SetCellValue("");
                    break;

            }
        }

        /// <summary>
        /// 创建日期样式
        /// </summary>
        /// <returns></returns>
        private NPOI.SS.UserModel.ICellStyle CreateDateStyle()
        {
            if (_dateStyle != null)
            {
                return _dateStyle;
            }
            _dateStyle = CellStyleResolver.Resolve(_workbook, CellStyle.Body());
            var format = _workbook.CreateDataFormat();
            _dateStyle.DataFormat = format.GetFormat(_dateFormat);
            return _dateStyle;
        }

        #endregion

        #region Write(写入流)

        /// <summary>
        /// 写入流
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public IExcel Write(Stream stream)
        {
            _workbook.Write(stream);
            return this;
        }

        #endregion

        #region DateFormat(设置日期格式)

        /// <summary>
        /// 设置日期格式
        /// </summary>
        /// <param name="format">日期格式，默认：yyyy-mm-dd</param>
        /// <returns></returns>
        public IExcel DateFormat(string format = "yyyy-mm-dd")
        {
            _dateFormat = format;
            return this;
        }

        #endregion

        #region MergeCell(合并单元格)

        /// <summary>
        /// 合并单元格。坐标：(x1,x2,y1,y2)
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="startColumnIndex">开始列索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        /// <returns></returns>
        public IExcel MergeCell(int startRowIndex, int endRowIndex, int startColumnIndex, int endColumnIndex)
        {
            var region = new NPOI.SS.Util.CellRangeAddress(startRowIndex, endRowIndex, startColumnIndex, endColumnIndex);
            _sheet.AddMergedRegion(region);
            return this;
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        public IExcel MergeCell(ICell cell)
        {
            if (cell.NeedMerge)
            {
                MergeCell(cell.RowIndex, cell.EndRowIndex, cell.ColumnIndex, cell.EndColumnIndex);
            }
            return this;
        }

        #endregion

        #region TitleStyle(设置标题样式)

        /// <summary>
        /// 设置标题样式
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExcel TitleStyle(IWorkSheet sheet, CellStyle style)
        {
            return this;
        }

        #endregion

        #region HeadStyle(设置表头样式)

        /// <summary>
        /// 设置表头样式
        /// </summary>
        /// <param name="table">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExcel HeadStyle(IWorkSheet table, CellStyle style)
        {
            if (_headStyle == null)
            {
                _headStyle = CellStyleResolver.Resolve(_workbook, style);
            }
            Style(0, table.HeadRowCount - 1, 0, table.ColumnCount - 1, _headStyle);
            return this;
        }

        /// <summary>
        /// 设置样式
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        protected IExcel Style(int startRowIndex, int endRowIndex, int startColumnIndex, int endColumnIndex,
            NPOI.SS.UserModel.ICellStyle style)
        {
            for (var i = startRowIndex; i <= endColumnIndex; i++)
            {
                var row = GetOrCreateRow(i);
                for (var j = startColumnIndex; j <= endColumnIndex; j++)
                {
                    GetOrCreateCell(row, j).CellStyle = style;
                }
            }
            return this;
        }

        #endregion

        #region BodyStyle(设置正文样式)

        /// <summary>
        /// 设置正文样式
        /// </summary>
        /// <param name="table">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExcel BodyStyle(IWorkSheet table, CellStyle style)
        {
            if (_bodyStyle == null)
            {
                _bodyStyle = CellStyleResolver.Resolve(_workbook, style);
            }
            Style(table.HeadRowCount, table.HeadRowCount + table.BodyRowCount - 1, 0, table.ColumnCount - 1,
                _bodyStyle);
            return this;
        }

        #endregion

        #region FootStyle(设置页脚样式)

        /// <summary>
        /// 设置页脚样式
        /// </summary>
        /// <param name="table">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public IExcel FootStyle(IWorkSheet table, CellStyle style)
        {
            if (_footStyle == null)
            {
                _footStyle = CellStyleResolver.Resolve(_workbook, style);
            }
            Style(table.HeadRowCount + table.BodyRowCount, table.RowCount - 1, 0, table.ColumnCount - 1, _footStyle);
            return this;
        }

        #endregion

        #region ColumnWidth(设置单元格列宽)

        /// <summary>
        /// 设置单元格列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <param name="width">列宽度，设置字符数</param>
        /// <returns></returns>
        public IExcel ColumnWidth(int columnIndex, int width)
        {
            _sheet.SetColumnWidth(columnIndex, width * 256);
            return this;
        }

        #endregion

        #region AutoColumnWidth(设置单元格自动列宽)

        /// <summary>
        /// 设置单元格自动列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public IExcel AutoColumnWidth(int columnIndex)
        {
            return this;
        }

        /// <summary>
        /// 设置单元格自动列宽
        /// </summary>
        /// <returns></returns>
        public IExcel AutoColumnWidth()
        {
            return this;
        }

        #endregion

        #region RowHeight(设置单元行行高)

        /// <summary>
        /// 设置单元行行高
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="height">行高度</param>
        /// <returns></returns>
        public IExcel RowHeight(int rowIndex, int height)
        {
            return this;
        }

        #endregion

        #region AutoRowHeight(设置单元行自动行高)

        /// <summary>
        /// 设置单元行自动行高
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public IExcel AutoRowHeight(int rowIndex)
        {
            return this;
        }

        /// <summary>
        /// 设置单元行自动行高
        /// </summary>
        /// <returns></returns>
        public IExcel AutoRowHeight()
        {
            return this;
        }

        #endregion

        #region GetWorkbook(获取工作簿)

        /// <summary>
        /// 获取工作簿
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public IWorkbook GetWorkbook(Stream stream)
        {
            return new Workbook();
        }

        /// <summary>
        /// 获取工作簿
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        /// <returns></returns>
        public IWorkbook GetWorkbook(string fileName)
        {
            return new Workbook();
        }

        #endregion
    }
}
