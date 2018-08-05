namespace Bing.Offices.Core.Properties
{
    /// <summary>
    /// 列属性信息
    /// </summary>
    public class ColumnPropertyInfo
    {
        /// <summary>
        /// 名称。对应字段
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 标题。用于输出显示文本
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 列标识
        /// </summary>
        public ColumnFlags ColumnFlags { get; set; }

        /// <summary>
        /// 列索引
        /// </summary>
        public int Index { get; set; }
    }
}
