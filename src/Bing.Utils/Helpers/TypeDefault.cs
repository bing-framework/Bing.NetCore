using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils
{
    /// <summary>
    /// 类型默认值
    /// </summary>
    public static class TypeDefault
    {
        /// <summary>
        /// 获取指定类型默认值
        /// </summary>
        /// <typeparam name="TValue">值类型</typeparam>
        public static TValue Of<TValue>() => default;

        /// <summary>
        /// int
        /// </summary>
        public static int Int => default;

        /// <summary>
        /// long
        /// </summary>
        public static long Long => default;

        /// <summary>
        /// float
        /// </summary>
        public static float Float => default;

        /// <summary>
        /// double
        /// </summary>
        public static double Double => default;

        /// <summary>
        /// decimal
        /// </summary>
        public static decimal Decimal => default;

        /// <summary>
        /// char
        /// </summary>
        public static char Char => default;

        /// <summary>
        /// string
        /// </summary>
        public static string String => default;

        /// <summary>
        /// empty
        /// </summary>
        public static string StringEmpty => string.Empty;

        /// <summary>
        /// DateTime
        /// </summary>
        public static DateTime DateTime => default;

        /// <summary>
        /// TimeSpan
        /// </summary>
        public static TimeSpan TimeSpan => default;
    }
}
