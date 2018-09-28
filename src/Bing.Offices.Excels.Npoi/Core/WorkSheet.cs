using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using NPOI.SS.UserModel;
using IRow = Bing.Offices.Excels.Abstractions.IRow;

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
        private ISheet _sheet;

        public WorkSheet(ISheet sheet)
        {
            _sheet = sheet;
            SheetName = sheet.SheetName;
            Rows = new List<IRow>();
            RowIndex = 0;
            Header = new Range();
        }

        protected override Bing.Offices.Excels.Abstractions.ICell CreateCell(object value)
        {
            throw new System.NotImplementedException();
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

        public override object GetCelLValue(int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
