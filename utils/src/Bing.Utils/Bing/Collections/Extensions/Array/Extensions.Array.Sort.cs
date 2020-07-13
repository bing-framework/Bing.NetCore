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
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        public static void Sort(this Array array) => Array.Sort(array);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        public static void Sort(this Array array, Array items) => Array.Sort(array, items);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Sort(this Array array, int index, int length) => Array.Sort(array, index, length);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Sort(this Array array, Array items, int index, int length) =>
            Array.Sort(array, items, index, length);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, IComparer comparer) => Array.Sort(array, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, Array items, IComparer comparer) =>
            Array.Sort(array, items, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, int index, int length, IComparer comparer) =>
            Array.Sort(array, index, length, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, Array items, int index, int length, IComparer comparer) =>
            Array.Sort(array, items, index, length, comparer);
    }
}
