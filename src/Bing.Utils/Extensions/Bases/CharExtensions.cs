using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字符(<see cref="Char"/>) 扩展
    /// </summary>
    public static class CharExtensions
    {
        #region In(判断当前字符是否在目标字符数组中)

        /// <summary>
        /// 判断当前字符是否在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        /// <returns></returns>
        public static bool In(this char @this, params char[] values)
        {
            return Array.IndexOf(values, @this) != -1;
        }

        #endregion

        #region NotIn(判断当前字符是否不在目标字符数组中)

        /// <summary>
        /// 判断当前字符是否不在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        /// <returns></returns>
        public static bool NotIn(this char @this, params char[] values)
        {
            return Array.IndexOf(values, @this) == -1;
        }

        #endregion

        #region Repeat(指定次数重复一个字符)

        /// <summary>
        /// 指定次数重复一个字符
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="repeatCount">重复数</param>
        /// <returns></returns>
        public static string Repeat(this char @this, int repeatCount)
        {
            return new string(@this, repeatCount);
        }

        #endregion
    }
}
