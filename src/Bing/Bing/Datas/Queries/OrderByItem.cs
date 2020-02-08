namespace Bing.Datas.Queries
{
    /// <summary>
    /// 排序项
    /// </summary>
    public class OrderByItem
    {
        /// <summary>
        /// 初始化一个<see cref="OrderByItem"/>类型的实例
        /// </summary>
        /// <param name="name">排序属性</param>
        /// <param name="desc">是否降序</param>
        public OrderByItem(string name, bool desc)
        {
            Name = name;
            Desc = desc;
        }

        /// <summary>
        /// 排序属性
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否降序
        /// </summary>
        public bool Desc { get; set; }

        /// <summary>
        /// 创建排序字符串
        /// </summary>
        public string Generate() => Desc ? $"{Name} desc" : Name;
    }
}
