namespace Bing.Offices.Core.Properties
{
    /// <summary>
    /// 列标识
    /// </summary>
    public enum ColumnFlags
    {
        /// <summary>
        /// 可选列
        /// </summary>
        ColumnOptional = 1,
        /// <summary>
        /// 必选列
        /// </summary>
        ColumnRequired = 2,
        /// <summary>
        /// 必填值
        /// </summary>
        ValueRequired = 4,
        /// <summary>
        /// 不可为空或空字符
        /// </summary>
        NotNullOrWhiteSpace = 8
    }
}
