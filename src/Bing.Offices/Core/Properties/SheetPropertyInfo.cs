namespace Bing.Offices.Core.Properties
{
    /// <summary>
    /// 工作表属性信息
    /// </summary>
    public class SheetPropertyInfo
    {
        /// <summary>
        /// 是否自动索引，<see cref="AutoIndex"/>设置为true，将尝试通过其标题查找列索引
        /// </summary>
        public bool AutoIndex { get; set; }

        /// <summary>
        /// 标题行索引
        /// </summary>
        public int TitleRowIndex { get; set; } = 0;

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 工作表索引
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// 工作表名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 冻结属性
        /// </summary>
        public FrezzePropertyInfo Frezze { get; set; }

        /// <summary>
        /// 初始化一个<see cref="SheetPropertyInfo"/>类型的实例
        /// </summary>
        /// <param name="name">工作表名称</param>
        /// <param name="titleRowIndex">标题行索引</param>
        public SheetPropertyInfo(string name, int titleRowIndex)
        {
            Name = name;
            TitleRowIndex = titleRowIndex;
        }
    }
}
