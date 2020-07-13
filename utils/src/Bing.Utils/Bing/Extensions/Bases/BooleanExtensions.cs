using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 布尔值(<see cref="Boolean"/>) 扩展
    /// </summary>
    public static class BooleanExtensions
    {
        #region ToLower(将布尔值转换为小写字符串)

        /// <summary>
        /// 将布尔值转换为小写字符串
        /// </summary>
        /// <param name="value">值</param>
        public static string ToLower(this bool value) => value.ToString().ToLower();

        /// <summary>
        /// 将布尔值转换为小写字符串
        /// </summary>
        /// <param name="value">值</param>
        public static string ToLower(this bool? value) => ToLower(value ?? false);

        #endregion

        #region ToBinaryTypeNumber(将布尔值转换为二进制数字类型)

        /// <summary>
        /// 将布尔值转换为二进制数字类型（true:1、false:0）
        /// </summary>
        /// <param name="value">值</param>
        public static int ToBinaryTypeNumber(this bool value) => value ? 1 : 0;

        /// <summary>
        /// 将布尔值转换为二进制数字类型（true:1、false:0）
        /// </summary>
        /// <param name="value">值</param>
        public static int ToBinaryTypeNumber(this bool? value) => ToBinaryTypeNumber(value ?? false);

        #endregion

    }
}
