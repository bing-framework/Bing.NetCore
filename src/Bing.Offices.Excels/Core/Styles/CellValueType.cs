namespace Bing.Offices.Excels.Core.Styles
{
    /// <summary>
    /// 单元格值类型
    /// </summary>
    public enum CellValueType
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown = -1,
        /// <summary>
        /// 数值
        /// </summary>
        Number = 0,
        /// <summary>
        /// 字符串
        /// </summary>
        String = 1,
        /// <summary>
        /// 计算公式
        /// </summary>
        Formula = 2,
        /// <summary>
        /// 空白
        /// </summary>
        Empty = 3,
        /// <summary>
        /// 布尔值
        /// </summary>
        Boolean = 4,
        /// <summary>
        /// 错误
        /// </summary>
        Error = 5,
        /// <summary>
        /// 日期
        /// </summary>
        Date = 6,        
    }
}
