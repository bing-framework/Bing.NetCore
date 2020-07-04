using System;
using System.Collections;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 数组(<see cref="Array"/>) 扩展
    /// </summary>
    public static partial class ArrayExtensions
    {
        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        public static int BinarySearch(this Array array, object value) => Array.BinarySearch(array, value);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="value">值</param>
        public static int BinarySearch(this Array array, int index, int length, object value) =>
            Array.BinarySearch(array, index, length, value);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="comparer">比较器</param>
        public static int BinarySearch(this Array array, object value, IComparer comparer) =>
            Array.BinarySearch(array, value, comparer);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="value">值</param>
        /// <param name="comparer">比较器</param>
        public static int BinarySearch(this Array array, int index, int length, object value, IComparer comparer) =>
            Array.BinarySearch(array, index, length, value, comparer);
    }
}
