using Bing.Offices.Excels.Npoi.Extensions;
using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的工作表
    /// </summary>
    public class WorkSheet: Bing.Offices.Excels.Core.WorkSheetBase
    {
        /// <summary>
        /// 工作表
        /// </summary>
        private NPOI.SS.UserModel.ISheet _sheet;

        public WorkSheet(Bing.Offices.Excels.Abstractions.IWorkbook workbook, NPOI.SS.UserModel.ISheet sheet,
            int sheetIndex) : base(workbook, sheetIndex)
        {
            _sheet = sheet;
            SheetName = sheet.SheetName;
            Header = new Range();
        }

        /// <summary>
        /// 创建单元行
        /// </summary>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.IRow CreateRow()
        {
            var row = new Row(this, _sheet, RowIndex);
            RowIndex++;
            Rows.Add(row);
            return row;
        }

        /// <summary>
        /// 获取或创建单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.IRow GetOrCreateRow(int rowIndex)
        {
            var row = GetRow(rowIndex);
            if (row == null)
            {
                row = new Row(this, _sheet, RowIndex);
                Rows.Add(row);
            }
            return row;
        }

        protected override Bing.Offices.Excels.Abstractions.ICell CreateCell(object value)
        {
            return null;
        }

        protected override Bing.Offices.Excels.Abstractions.IRange GetBodyRange()
        {
            throw new System.NotImplementedException();
        }

        protected override Bing.Offices.Excels.Abstractions.IRange GetFootRange()
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IColumn Column(int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IColumn Column(string columnName)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IRow Row(int rowIndex)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell Cell(string cellPos)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell Cell(int x, int y)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell Cell(string x, int y)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell Cell(int x1, int y1, int x2, int y2)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell Cell(Bing.Offices.Excels.Abstractions.ICellRange range)
        {
            throw new System.NotImplementedException();
        }

        public override object GetCellValue(string cellPos)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override object GetCelLValue(int rowIndex, int columnIndex)
        {
            if (rowIndex > _sheet.LastRowNum)
            {
                return string.Empty;
            }

            if (columnIndex > _sheet.GetRow(rowIndex).LastCellNum)
            {
                return string.Empty;
            }

            var cell = _sheet.GetRow(rowIndex).GetCell(columnIndex);
            return cell.GetCellValue();
        }

    }
}
