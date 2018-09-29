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
        object Value { get; set; }

        /// <summary>
        /// 单元行
        /// </summary>
        IRow Row { get; }

        /// <summary>
        /// 工作表
        /// </summary>
        IWorkSheet Sheet { get; }

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
        int ColumnIndex { get;}

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
        void SetStyle(CellStyle style);

        /// <summary>
        /// 设置单元格的值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        void SetValueType(CellValueType cellValueType);

        /// <summary>
        /// 设置单元格的公式
        /// </summary>
        /// <param name="formula">公式</param>
        void SetFormula(string formula);
    }
}
