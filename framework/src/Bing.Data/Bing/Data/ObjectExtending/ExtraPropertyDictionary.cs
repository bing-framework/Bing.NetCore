using System;
using System.Collections.Generic;

namespace Bing.Data.ObjectExtending
{
    /// <summary>
    /// 扩展属性字典
    /// </summary>
    [Serializable]
    public class ExtraPropertyDictionary : Dictionary<string, object>
    {
        /// <summary>
        /// 初始化一个<see cref="ExtraPropertyDictionary"/>类型的实例
        /// </summary>
        public ExtraPropertyDictionary()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ExtraPropertyDictionary"/>类型的实例
        /// </summary>
        /// <param name="dictionary">字典</param>
        public ExtraPropertyDictionary(IDictionary<string, object> dictionary) : base(dictionary)
        {
        }
    }
}
