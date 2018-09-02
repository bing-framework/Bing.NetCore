using System;
using System.Collections.Generic;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 授权选择器标签
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    public class PolicySelectorWithLabel<T> where T:Attribute
    {
        /// <summary>
        /// 选择器
        /// </summary>
        public Func<IEnumerable<T>,IEnumerable<string>> Selector { get; set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Label { get; set; }
    }
}
