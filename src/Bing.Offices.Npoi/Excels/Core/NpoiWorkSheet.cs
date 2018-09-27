using System.Collections.Generic;
using System.Data;
using System.Drawing;
using Bing.Offices.Excels.Abstractions;
using NPOI.SS.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的工作表
    /// </summary>
    public class NpoiWorkSheet: Bing.Offices.Excels.Core.WorkSheetBase
    {
        /// <summary>
        /// 工作表
        /// </summary>
        private ISheet _sheet;

        public override int ColumnNum { get; }
        public override int RowNum => _sheet.LastRowNum;
        public override string GetCellValue(int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override DataTable GetTableContent(bool hasHeader = false)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IRange GetRange(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.ICell GetCell(int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override string GetCellFormula(int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IRow GetRow(int rowIndex)
        {
            throw new System.NotImplementedException();
        }

        public override Bing.Offices.Excels.Abstractions.IColumn GetColumn(int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override void InsertRow(int rowIndex)
        {
            throw new System.NotImplementedException();
        }

        public override void InsertColumn(int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override void SetCellValue(string value, int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override void SetCellFormula(string formula, int rowIndex, int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override void SetRangeColor(Bing.Offices.Excels.Abstractions.IRange range, Color color)
        {
            throw new System.NotImplementedException();
        }

        public override void SetCellColor(int rowIndex, int columnIndex, Color color)
        {
            throw new System.NotImplementedException();
        }

        public override void MergeCell(IRange range)
        {
            throw new System.NotImplementedException();
        }

        public override void MergeCell(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex)
        {
            throw new System.NotImplementedException();
        }

        public override List<string> GetDataFromRow(int rowIndex)
        {
            throw new System.NotImplementedException();
        }

        public override List<string> GetDataFromColumn(int columnIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
