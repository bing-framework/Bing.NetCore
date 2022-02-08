using System;
using Bing.Helpers;

namespace Bing.Data.ObjectExtending
{
    /// <summary>
    /// 扩展属性字典(<see cref="ExtraPropertyDictionary"/>) 扩展
    /// </summary>
    public static class ExtraPropertyDictionaryExtensions
    {
        /// <summary>
        /// 获取属性
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="source">扩展属性字典</param>
        /// <param name="name">属性名</param>
        public static TProperty GetProperty<TProperty>(this ExtraPropertyDictionary source, string name)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.ContainsKey(name) == false)
                return default;
            return Conv.To<TProperty>(source[name]);
        }

        /// <summary>
        /// 设置属性，如果存在则先移除旧属性
        /// </summary>
        /// <param name="source">扩展属性字典</param>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        public static ExtraPropertyDictionary SetProperty(this ExtraPropertyDictionary source, string name, object value)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            source.RemoveProperty(name);
            source[name] = value;
            return source;
        }

        /// <summary>
        /// 移除属性
        /// </summary>
        /// <param name="source">扩展属性字典</param>
        /// <param name="name">属性名</param>
        public static ExtraPropertyDictionary RemoveProperty(this ExtraPropertyDictionary source, string name)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.ContainsKey(name))
                source.Remove(name);
            return source;
        }
    }
}
