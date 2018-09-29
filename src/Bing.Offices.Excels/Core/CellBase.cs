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
        public object Value
        {
            get => GetValue();
            set => SetValue(value);
        }

        /// <summary>
        /// 单元行
        /// </summary>
        public IRow Row { get; }

        /// <summary>
        /// 工作表
        /// </summary>
        public IWorkSheet Sheet { get; }

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
        public int ColumnIndex { get; protected set; }

        /// <summary>
        /// 单元格范围
        /// </summary>
        public abstract ICellRange Range { get; }

        protected CellBase(IRow row)
        {
            Row = row;
            Sheet = row.Sheet;
        }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        public abstract void SetValue(object value);

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
        public abstract void SetStyle(CellStyle style);

        /// <summary>
        /// 设置单元格的值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        public abstract void SetValueType(CellValueType cellValueType);

        /// <summary>
        /// 设置单元格的公式
        /// </summary>
        /// <param name="formula">公式</param>
        public abstract void SetFormula(string formula);
    }
}
