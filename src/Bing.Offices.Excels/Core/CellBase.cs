using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 单元格基类
    /// </summary>
    public abstract class CellBase:ICell
    {
        /// <summary>
        /// 列跨度
        /// </summary>
        private int _columnSpan;

        /// <summary>
        /// 行跨度
        /// </summary>
        private int _rowSpan;

        /// <summary>
        /// 值
        /// </summary>
        public object Value { get; protected set; }

        /// <summary>
        /// 单元行
        /// </summary>
        public IRow Row { get; set; }

        /// <summary>
        /// 是否合并单元格
        /// </summary>
        public bool MergeCell => ColumnSpan > 1 || RowSpan > 1;

        /// <summary>
        /// 行跨度。合并单元格的行跨度
        /// </summary>
        public int RowSpan
        {
            get => _rowSpan;
            set
            {
                if (value < 1)
                {
                    value = 1;
                }
                _rowSpan = value;
            }
        }

        /// <summary>
        /// 列跨度。合并单元格的列跨度
        /// </summary>
        public int ColumnSpan
        {
            get => _columnSpan;
            set
            {
                if (value < 1)
                {
                    value = 1;
                }
                _columnSpan = value;
            }
        }

        /// <summary>
        /// 当前行索引
        /// </summary>
        public int RowIndex => Row.RowIndex;

        /// <summary>
        /// 当前列索引
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// 单元格范围
        /// </summary>
        public abstract ICellRange Range { get; internal set; }

        /// <summary>
        /// 初始化一个<see cref="CellBase"/>类型的实例
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="columnSpan">列跨度</param>
        /// <param name="rowSpan">行跨度</param>
        protected CellBase(object value, int columnSpan = 1, int rowSpan = 1)
        {
            Value = value;
            ColumnSpan = columnSpan;
            RowSpan = rowSpan;
        }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        public void SetValue(object value)
        {
            Value = value;
        }

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <returns></returns>
        public abstract string GetValue();

        /// <summary>
        /// 设置单元格的样式
        /// </summary>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public abstract ICell SetStyle(CellStyle style);

        /// <summary>
        /// 设置单元格的值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        /// <returns></returns>
        public abstract ICell SetValueType(CellValueType cellValueType);

        /// <summary>
        /// 设置单元格的公式
        /// </summary>
        /// <param name="formula">公式</param>
        /// <returns></returns>
        public abstract ICell SetFormula(string formula);

        /// <summary>
        /// 设置单元格的范围
        /// </summary>
        /// <param name="range">单元格范围</param>
        public void SetRange(ICellRange range)
        {
            Range = range;
        }
    }
}
