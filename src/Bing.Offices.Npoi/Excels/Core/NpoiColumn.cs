using System.Drawing;
using Bing.Offices.Npoi.Extensions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的单元列
    /// </summary>
    public class NpoiColumn: Bing.Offices.Excels.Abstractions.IColumn
    {
        /// <summary>
        /// 工作表
        /// </summary>
        private ISheet _sheet;

        /// <summary>
        /// 列索引
        /// </summary>
        private int _columnIndex;

        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        public bool Bold
        {
            get => _sheet.GetColumnStyle(_columnIndex).GetFont(_sheet.Workbook).IsBold;
            set
            {
                var workbook = _sheet.Workbook;
                var cellStyle = workbook.CreateCellStyle();
                cellStyle.CloneStyleFrom(_sheet.GetColumnStyle(_columnIndex));
                cellStyle.GetFont(workbook).IsBold = value;
                _sheet.SetDefaultColumnStyle(_columnIndex, cellStyle);
            }
        }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        public bool Italic
        {
            get => _sheet.GetColumnStyle(_columnIndex).GetFont(_sheet.Workbook).IsItalic;
            set
            {
                var workbook = _sheet.Workbook;
                var cellStyle = workbook.CreateCellStyle();
                cellStyle.CloneStyleFrom(_sheet.GetColumnStyle(_columnIndex));
                cellStyle.GetFont(workbook).IsItalic = value;
                _sheet.SetDefaultColumnStyle(_columnIndex, cellStyle);
            }
        }

        /// <summary>
        /// 初始化一个<see cref="NpoiColumn"/>类型的实例
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="columnIndex">列索引</param>
        public NpoiColumn(ISheet sheet, int columnIndex)
        {
            _sheet = sheet;
            _columnIndex = columnIndex;
        }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        public void SetFontStyle(Font font)
        {
            var workbook = _sheet.Workbook;
            var rowNum = _sheet.LastRowNum;
            for (int i = 0; i <= rowNum; i++)
            {
                IRow row = _sheet.GetRow(i);
                ICell cell = row.GetCell(_columnIndex);

                ICellStyle cellStyle = workbook.CreateCellStyle();
                cellStyle.CloneStyleFrom(cell.CellStyle);
                cellStyle.SetFont(workbook, x =>
                {
                    x.FontName = font.Name;
                    x.IsBold = font.Bold;
                    x.IsItalic = font.Italic;
                    if (font.Underline)
                    {
                        x.Underline = FontUnderlineType.Single;
                    }
                });

                cell.CellStyle = cellStyle;
            }
        }

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetBackgroundColor(Color color)
        {
            var workbook = _sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_sheet.GetColumnStyle(_columnIndex));
            cellStyle.FillPattern = FillPattern.SolidForeground;
            if (workbook is HSSFWorkbook hssfWorkbook)
            {
                cellStyle.FillBackgroundColor = hssfWorkbook.GetXlsColour(color);
            }
            else
            {
                var xssfcolor = new XSSFColor(color);
                cellStyle.FillBackgroundColor = xssfcolor.Indexed;
            }

            _sheet.SetDefaultColumnStyle(_columnIndex, cellStyle);
        }

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetFontColor(Color color)
        {
            var workbook = _sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_sheet.GetColumnStyle(_columnIndex));
            cellStyle.FillPattern = FillPattern.SolidForeground;
            if (workbook is HSSFWorkbook hssfWorkbook)
            {
                cellStyle.SetFont(workbook, x => { x.Color = hssfWorkbook.GetXlsColour(color); });
            }
            else
            {
                var xssfcolor = new XSSFColor(color);
                cellStyle.SetFont(workbook, x => { x.Color = xssfcolor.Indexed; });
            }

            _sheet.SetDefaultColumnStyle(_columnIndex, cellStyle);
        }

        /// <summary>
        /// 设置宽度
        /// </summary>
        /// <param name="width">宽度</param>
        public void SetWidth(int width)
        {
            _sheet.SetColumnWidth(_columnIndex, width);
        }

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public Bing.Offices.Excels.Abstractions.ICell GetCell(int rowIndex)
        {
            ICell cell = _sheet.GetRow(rowIndex).GetCell(_columnIndex);
            return new NpoiCell(cell);
        }
    }
}
