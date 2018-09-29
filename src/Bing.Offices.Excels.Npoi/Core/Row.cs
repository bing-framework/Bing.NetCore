using Bing.Offices.Excels.Npoi.Extensions;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的单元行
    /// </summary>
    public class Row: Bing.Offices.Excels.Core.RowBase
    {
        /// <summary>
        /// 单元行
        /// </summary>
        private NPOI.SS.UserModel.IRow _row;

        /// <summary>
        /// 工作表
        /// </summary>
        private NPOI.SS.UserModel.ISheet _sheet;

        /// <summary>
        /// 列索引
        /// </summary>
        private int _columnIndex;

        public Row(Bing.Offices.Excels.Abstractions.IWorkSheet sheet, NPOI.SS.UserModel.ISheet npoiSheet,
            int rowIndex) : base(sheet, rowIndex)
        {
            _sheet = npoiSheet;
            _row = npoiSheet.GetOrCreateRow(rowIndex);
            _columnIndex = 0;
        }

        /// <summary>
        /// 创建单元格
        /// </summary>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.ICell CreateCell()
        {
            var cell = new Cell(this, _row.GetOrCreateCell(_columnIndex));
            _columnIndex++;
            Cells.Add(cell);
            return cell;
        }

        /// <summary>
        /// 获取或创建单元格
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.ICell GetOrCreateCell(int columnIndex)
        {
            var cell = GetCell(columnIndex);
            if (cell == null)
            {
                cell = new Cell(this, _row.GetOrCreateCell(columnIndex));
                Cells.Add(cell);
            }
            return cell;
        }

        /// <summary>
        /// 设置行高
        /// </summary>
        /// <param name="height">高度</param>
        protected override void SetHeight(short height)
        {
            _row.Height = height;
        }

        /// <summary>
        /// 获取行高
        /// </summary>
        /// <returns></returns>
        protected override short GetHeight()
        {
            return _row.Height;
        }
    }
}
