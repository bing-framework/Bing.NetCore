namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元格
    /// </summary>
    public interface ICell
    {
        /// <summary>
        /// 单元格值
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// 单元行
        /// </summary>
        IRow Row { get; set; }

        /// <summary>
        /// 列跨度
        /// </summary>
        int ColumnSpan { get; set; }

        /// <summary>
        /// 行跨度
        /// </summary>
        int RowSpan { get; set; }

        /// <summary>
        /// 是否需要合并单元格。true:是,false:否
        /// </summary>
        bool NeedMerge { get; }

        /// <summary>
        /// 行索引
        /// </summary>
        int RowIndex { get; }

        /// <summary>
        /// 列索引
        /// </summary>
        int ColumnIndex { get; set; }

        /// <summary>
        /// 结束行索引
        /// </summary>
        int EndRowIndex { get; }

        /// <summary>
        /// 结束列索引
        /// </summary>
        int EndColumnIndex { get; }

        /// <summary>
        /// 设置单元格值
        /// </summary>
        /// <param name="value">值</param>
        void SetValue(object value);
    }
}
