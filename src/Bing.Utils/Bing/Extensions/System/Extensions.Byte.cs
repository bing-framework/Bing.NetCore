using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 基础类型扩展
    /// </summary>
    public static partial class BaseTypeExtensions
    {
        /// <summary>
        /// 获取最大值
        /// </summary>
        /// <param name="val1">值1</param>
        /// <param name="val2">值2</param>
        public static byte Max(this byte val1, byte val2) => Math.Max(val1, val2);

        /// <summary>
        /// 获取最小值
        /// </summary>
        /// <param name="val1">值1</param>
        /// <param name="val2">值2</param>
        public static byte Min(this byte val1, byte val2) => Math.Min(val1, val2);
    }
}
