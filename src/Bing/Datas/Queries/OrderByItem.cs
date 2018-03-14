using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.Datas.Queries
{
    /// <summary>
    /// 排序项
    /// </summary>
    public class OrderByItem
    {
        /// <summary>
        /// 排序属性
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否降序
        /// </summary>
        public bool Desc { get; set; }

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
        /// 创建排序字符串
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            if (Desc)
            {
                return $"{Name} desc";
            }
            return Name;
        }
    }
}
