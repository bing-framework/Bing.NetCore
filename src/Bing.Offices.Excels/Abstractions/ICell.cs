using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元格
    /// </summary>
    public interface ICell
    {
        /// <summary>
        /// 值
        /// </summary>
        object Value { get; }

        /// <summary>
        /// 单元行
        /// </summary>
        IRow Row { get; set; }

        /// <summary>
        /// 是否合并单元格
        /// </summary>
        bool MergeCell { get; }

        /// <summary>
        /// 行跨度。合并单元格的行跨度
        /// </summary>
        int RowSpan { get; set; }

        /// <summary>
        /// 列跨度。合并单元格的列跨度
        /// </summary>
        int ColumnSpan { get; set; }

        /// <summary>
        /// 当前行索引
        /// </summary>
        int RowIndex { get; }

        /// <summary>
        /// 当前列索引
        /// </summary>
        int ColumnIndex { get; set; }

        /// <summary>
        /// 单元格范围
        /// </summary>
        ICellRange Range { get; }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        void SetValue(object value);

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <returns></returns>
        string GetValue();

        /// <summary>
        /// 设置单元格的样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        ICell SetStyle(CellStyle style);

        /// <summary>
        /// 设置单元格的值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        /// <returns></returns>
        ICell SetValueType(CellValueType cellValueType);

        /// <summary>
        /// 设置单元格的公式
        /// </summary>
        /// <param name="formula">公式</param>
        /// <returns></returns>
        ICell SetFormula(string formula);

        /// <summary>
        /// 设置单元格的范围
        /// </summary>
        /// <param name="range">单元格范围</param>
        /// <returns></returns>
        void SetRange(ICellRange range);
    }
}
