using System.Drawing;
using Bing.Offices.Npoi.Extensions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的单元格
    /// </summary>
    public class NpoiCell: Bing.Offices.Excels.Abstractions.ICell
    {
        /// <summary>
        /// 单元格
        /// </summary>
        private ICell _cell;

        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        public bool Bold
        {
            get => _cell.CellStyle.GetFont(_cell.Sheet.Workbook).IsBold;
            set => _cell.CellStyle.GetFont(_cell.Sheet.Workbook).IsBold = value;
        }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        public bool Italic
        {
            get => _cell.CellStyle.GetFont(_cell.Sheet.Workbook).IsItalic;
            set => _cell.CellStyle.GetFont(_cell.Sheet.Workbook).IsItalic = value;
        }

        /// <summary>
        /// 初始化一个<see cref="NpoiCell"/>类型的实例
        /// </summary>
        /// <param name="cell">单元格</param>
        public NpoiCell(ICell cell)
        {
            _cell = cell;
        }

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <returns></returns>
        public string GetValue()
        {
            return _cell.GetCellValue();
        }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        public void SetValue(string value)
        {
            _cell.SetValue(value);
        }

        /// <summary>
        /// 设置单元格的计算公式
        /// </summary>
        /// <param name="formula">计算公式</param>
        public void SetFormula(string formula)
        {
            _cell.SetCellFormula(formula);
        }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        public void SetFontStyle(Font font)
        {
            var workbook = _cell.Sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_cell.CellStyle);
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

            _cell.CellStyle = cellStyle;
        }

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetBackgroundColor(Color color)
        {
            var workbook = _cell.Sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_cell.CellStyle);
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

            _cell.CellStyle = cellStyle;
        }

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetFontColor(Color color)
        {
            var workbook = _cell.Sheet.Workbook;
            ICellStyle cellStyle = workbook.CreateCellStyle();
            cellStyle.CloneStyleFrom(_cell.CellStyle);
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

            _cell.CellStyle = cellStyle;
        }

        /// <summary>
        /// 是否合并单元格
        /// </summary>
        /// <returns></returns>
        public bool IsMerged()
        {
            return _cell.IsMergedCell;
        }
    }
}
