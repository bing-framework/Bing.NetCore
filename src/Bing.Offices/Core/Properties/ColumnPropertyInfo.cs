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

        /// <summary>
        /// 是否自动索引，如果<see cref="Index"/>未设置，并且<see cref="AutoIndex"/>设置为true，则将尝试通过其标题查找列索引
        /// </summary>
        public bool AutoIndex { get; set; }

        /// <summary>
        /// 是否统计
        /// </summary>
        public bool Statistics { get; set; }

        /// <summary>
        /// 是否隐藏列
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// 是否忽略当前属性
        /// </summary>
        public bool Ignored { get; set; }

        /// <summary>
        /// 格式化文本
        /// </summary>
        public string Formatter { get; set; }

        /// <summary>
        /// 是否允许合并相同值的单元格
        /// </summary>
        public bool AllowMerge { get; set; }        
    }
}
