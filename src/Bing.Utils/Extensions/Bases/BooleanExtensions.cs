using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
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
        /// <returns></returns>
        public static string ToLower(this bool value)
        {
            return value.ToString().ToLower();
        }

        #endregion

        #region ToYestNoString(将布尔值转换为等效的字符串表示形式)

        /// <summary>
        /// 将布尔值转换为等效的字符串表示形式（Yes、No）
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToYesNoString(this bool value)
        {
            return value ? "Yes" : "No";
        }

        #endregion

        #region ToBinaryTypeNumber(将布尔值转换为二进制数字类型)

        /// <summary>
        /// 将布尔值转换为二进制数字类型（true:1、false:0）
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static int ToBinaryTypeNumber(this bool value)
        {
            return value ? 1 : 0;
        }

        #endregion

        #region ToChineseString(将布尔值转换为等效中文字符串表示形式)

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToChineseString(this bool value)
        {
            return ToChineseString(value, "是", "否");
        }

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="trueStr">为True时的中文</param>
        /// <param name="falseStr">为False时的中文</param>
        /// <returns></returns>
        public static string ToChineseString(this bool value, string trueStr, string falseStr)
        {
            return value ? trueStr : falseStr;
        }

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToChineseString(this bool? value)
        {
            return ToChineseString(value, "是", "否");
        }

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="trueStr">为True时的中文</param>
        /// <param name="falseStr">为False时的中文</param>
        /// <returns></returns>
        public static string ToChineseString(this bool? value, string trueStr, string falseStr)
        {
            return value.GetValueOrDefault() ? trueStr : falseStr;
        }

        #endregion

        #region IsTrue(结果为true时，输出参数)

        /// <summary>
        /// 结果为true时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        /// <returns></returns>
        public static T IsTrue<T>(this bool value, T t)
        {
            return value ? t : default(T);
        }

        /// <summary>
        /// 结果为true时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        /// <returns></returns>
        public static T IsTrue<T>(this bool? value, T t)
        {
            return value.GetValueOrDefault() ? t : default(T);
        }

        #endregion

        #region IsFalse(结果为false时，输出参数)

        /// <summary>
        /// 结果为false时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        /// <returns></returns>
        public static T IsFalse<T>(this bool value, T t)
        {
            return !value ? t : default(T);
        }

        /// <summary>
        /// 结果为false时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        /// <returns></returns>
        public static T IsFalse<T>(this bool? value, T t)
        {
            return !value.GetValueOrDefault() ? t : default(T);
        }

        #endregion
    }
}
