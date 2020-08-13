using System.Collections.Generic;
using System.Collections.Specialized;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 名称-值集合(<see cref="NameValueCollection"/>) 扩展
    /// </summary>
    public static partial class NameValueCollectionExtensions
    {
        /// <summary>
        /// 转换为键值对集合
        /// </summary>
        /// <param name="this">名称-值集合</param>
        public static IEnumerable<KeyValuePair<string, string>> ToKeyValuePair(this NameValueCollection @this)
        {
            if (@this == null || @this.Count == 0)
                yield break;
            foreach (var key in @this.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                yield return new KeyValuePair<string, string>(key, @this[key]);
            }
        }
    }
}
