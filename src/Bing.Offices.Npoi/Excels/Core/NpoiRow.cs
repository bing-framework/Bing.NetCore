
using System.Drawing;
using Bing.Offices.Npoi.Extensions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的单元行
    /// </summary>
    public class NpoiRow: Bing.Offices.Excels.Abstractions.IRow
    {
        /// <summary>
        /// 单元行
        /// </summary>
        private IRow _row;

        /// <summary>
        /// 行索引
        /// </summary>
        private int _rowIndex;

        /// <summary>
        /// 工作表
        /// </summary>
        private ISheet _sheet;

        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        public bool Bold
        {
            get => _row.RowStyle.GetFont(_sheet.Workbook).IsBold;
            set => _row.RowStyle.GetFont(_sheet.Workbook).IsBold = value;
        }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        public bool Italic
        {
            get => _row.RowStyle.GetFont(_sheet.Workbook).IsItalic;
            set => _row.RowStyle.GetFont(_sheet.Workbook).IsItalic = value;
        }

        /// <summary>
        /// 初始化一个<see cref="NpoiRow"/>类型的实例
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        public NpoiRow(ISheet sheet, int rowIndex)
        {
            _row = sheet.GetRow(rowIndex);
            _rowIndex = rowIndex;
            _sheet = sheet;
        }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        public void SetFontStyle(Font font)
        {
            var workbook = _sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_row.RowStyle);
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

            _row.RowStyle = cellStyle;
        }

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetBackgroundColor(Color color)
        {
            var workbook = _sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_row.RowStyle);
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

            _row.RowStyle = cellStyle;
        }

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetFontColor(Color color)
        {
            var workbook = _sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_row.RowStyle);
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

            _row.RowStyle = cellStyle;
        }

        /// <summary>
        /// 设置高度
        /// </summary>
        /// <param name="height">高度</param>
        public void SetHeight(int height)
        {
            _row.Height = (short)height;
        }

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public Bing.Offices.Excels.Abstractions.ICell GetCell(int columnIndex)
        {
            ICell cell = _row.GetCell(columnIndex);
            return new NpoiCell(cell);
        }
    }
}
