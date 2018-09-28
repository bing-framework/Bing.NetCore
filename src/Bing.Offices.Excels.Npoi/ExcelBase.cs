using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Npoi
{
    /// <summary>
    /// Excel 操作基类
    /// </summary>
    public abstract class ExcelBase:IExcel
    {
        public abstract IExcel CreateWorkbook();
        public abstract IExcel CreateWorkbook(string fileName);
        public abstract IExcel CreateWorkbook(Stream stream);
        public abstract IExcel CreatSheet(string sheetName = "");
        public abstract IExcel CreateRow(int rowIndex);
        public abstract IExcel CreateCell(ICell cell);
        public abstract IExcel Write(Stream stream);
        public abstract IExcel DateFormat(string format = "yyyy-mm-dd");
        public abstract IExcel MergeCell(int startRowIndex, int endRowIndex, int startColumnIndex, int endColumnIndex);
        public abstract IExcel Merge(ICellRange range);
        public abstract IExcel MergeCell(ICell cell);
        public abstract IExcel TitleStyle(IWorkSheet sheet, CellStyle style);
        public abstract IExcel HeadStyle(IWorkSheet sheet, CellStyle style);
        public abstract IExcel BodyStyle(IWorkSheet sheet, CellStyle style);
        public abstract IExcel FootStyle(IWorkSheet sheet, CellStyle style);
        public abstract IExcel ColumnWidth(int columnIndex, int width);
        public abstract IExcel AutoColumnWidth(int columnIndex);
        public abstract IExcel AutoColumnWidth();
        public abstract IExcel RowHeight(int rowIndex, int height);
        public abstract IExcel AutoRowHeight(int rowIndex);
        public abstract IExcel AutoRowHeight();
        public abstract IWorkbook GetWorkbook(Stream stream);
        public abstract IWorkbook GetWorkbook(string fileName);
    }
}
