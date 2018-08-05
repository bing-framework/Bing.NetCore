namespace Bing.Offices.Core.Errors
{
    /// <summary>
    /// Excel 错误信息
    /// </summary>
    public class ExcelErrorInfo
    {
        /// <summary>
        /// 行索引
        /// </summary>
        public int? RowIndex { get; set; }

        /// <summary>
        /// 列索引
        /// </summary>
        public int? ColumnIndex { get; set; }

        /// <summary>
        /// Excel 错误集合
        /// </summary>
        public ExcelErrorCollection Errors { get; }=new ExcelErrorCollection();
    }
}
