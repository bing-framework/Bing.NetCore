using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Npoi.Extensions;
using NPOI.SS.UserModel;

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

        public Row(NPOI.SS.UserModel.ISheet sheet, int rowIndex) : base(rowIndex)
        {
            _row = sheet.GetRow(rowIndex);
            _sheet = sheet;
            _columnIndex = 0;
        }

        /// <summary>
        /// 添加单元格
        /// </summary>
        /// <param name="value">值</param>
        public override void Add(object value)
        {
            var npoiCell = _row.GetOrCreateCell(_columnIndex);
            _columnIndex++;
            var cell = new Cell(npoiCell);
            cell.SetValue(value);
            Add(cell);
        }

        /// <summary>
        /// 设置行高
        /// </summary>
        /// <param name="height">高度</param>
        public override void SetHeight(int height)
        {
            _row.Height = (short) height;
        }

        /// <summary>
        /// 获取行高
        /// </summary>
        /// <returns></returns>
        public override int GetHeight()
        {
            return _row.Height;
        }
    }
}
