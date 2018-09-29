using Bing.Offices.Excels.Npoi.Extensions;
using Bing.Offices.Excels.Npoi.Resolvers;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的单元格
    /// </summary>
    public class Cell : Bing.Offices.Excels.Core.CellBase
    {
        /// <summary>
        /// 单元格
        /// </summary>
        private readonly NPOI.SS.UserModel.ICell _cell;

        /// <summary>
        /// 初始化一个<see cref="Cell"/>类型的实例
        /// </summary>
        /// <param name="row">单元行</param>
        /// <param name="cell">单元格</param>
        public Cell(Bing.Offices.Excels.Abstractions.IRow row, NPOI.SS.UserModel.ICell cell) : base(row)
        {
            _cell = cell;
            ColumnIndex = cell.ColumnIndex;
            RowSpan = ColumnSpan = 0;
            if (cell.IsMergedCell)
            {
                RowSpan = cell.ArrayFormulaRange.LastRow - cell.ArrayFormulaRange.FirstRow + 1;
                ColumnSpan = cell.ArrayFormulaRange.LastColumn - cell.ArrayFormulaRange.FirstColumn + 1;
            }
            else
            {
                RowSpan = 1;
                ColumnSpan = 1;
            }
        }

        /// <summary>
        /// 单元格范围
        /// </summary>
        public override Bing.Offices.Excels.Abstractions.ICellRange Range => new Bing.Offices.Excels.Core.CellRange(
            _cell.ArrayFormulaRange.FirstRow, _cell.ArrayFormulaRange.LastRow, _cell.ArrayFormulaRange.FirstColumn,
            _cell.ArrayFormulaRange.LastColumn);

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        public override void SetValue(object value)
        {
            _cell.SetValue(value);
        }

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <returns></returns>
        public override string GetValue()
        {
            return _cell.GetCellValue();
        }

        /// <summary>
        /// 设置单元格的样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        public override void SetStyle(Bing.Offices.Excels.Core.Styles.CellStyle style)
        {
            _cell.CellStyle = CellStyleResolver.Resolve(_cell.Sheet.Workbook, style);
        }

        /// <summary>
        /// 设置单元格的值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        public override void SetValueType(Bing.Offices.Excels.Core.Styles.CellValueType cellValueType)
        {
            _cell.SetCellType(CellValueTypeResolver.Resolve(cellValueType));
        }

        /// <summary>
        /// 设置单元格的公式
        /// </summary>
        /// <param name="formula">公式</param>
        public override void SetFormula(string formula)
        {
            _cell.SetCellFormula(formula);
        }
    }
}
