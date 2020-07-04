using System;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 数组(<see cref="Array"/>) 扩展
    /// </summary>
    public static partial class ArrayExtensions
    {
        /// <summary>
        /// 清空。清空指定索引以及长度的数组
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Clear(this Array array, int index, int length) => Array.Clear(array, index, length);

        /// <summary>
        /// 清空所有数据
        /// </summary>
        /// <param name="array">数组</param>
        public static void ClearAll(this Array array) => Array.Clear(array, 0, array.Length);
    }
}
