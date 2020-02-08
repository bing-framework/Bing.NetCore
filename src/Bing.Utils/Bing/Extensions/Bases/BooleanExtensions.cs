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

        #region ToYestNoString(将布尔值转换为等效的字符串表示形式)

        /// <summary>
        /// 将布尔值转换为等效的字符串表示形式（Yes、No）
        /// </summary>
        /// <param name="value">值</param>
        public static string ToYesNoString(this bool value) => value ? "Yes" : "No";

        /// <summary>
        /// 将布尔值转换为等效的字符串表示形式（Yes、No）
        /// </summary>
        /// <param name="value">值</param>
        public static string ToYesNoString(this bool? value) => ToYesNoString(value ?? false);

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

        #region ToChineseString(将布尔值转换为等效中文字符串表示形式)

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        public static string ToChineseString(this bool value) => ToChineseString(value, "是", "否");

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="trueStr">为True时的中文</param>
        /// <param name="falseStr">为False时的中文</param>
        public static string ToChineseString(this bool value, string trueStr, string falseStr) => value ? trueStr : falseStr;

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        public static string ToChineseString(this bool? value) => ToChineseString(value, "是", "否");

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="trueStr">为True时的中文</param>
        /// <param name="falseStr">为False时的中文</param>
        public static string ToChineseString(this bool? value, string trueStr, string falseStr) => value ?? false ? trueStr : falseStr;

        #endregion

        #region IfTrue(结果为true时，输出参数)

        /// <summary>
        /// 结果为true时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        public static T IfTrue<T>(this bool value, T t) => value ? t : default;

        /// <summary>
        /// 结果为true时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        public static T IfTrue<T>(this bool? value, T t) => value.GetValueOrDefault() ? t : default;

        #endregion

        #region IfTrue(结果为true时，执行方法)

        /// <summary>
        /// 结果为true时，执行方法
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="action">执行方法</param>
        public static void IfTrue(this bool value, Action action)
        {
            if (value)
                action();
        }

        /// <summary>
        /// 结果为true时，执行方法
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="action">执行方法</param>
        public static void IfTrue(this bool? value, Action action)
        {
            if (value.GetValueOrDefault())
                action();
        }

        #endregion

        #region IfFalse(结果为false时，输出参数)

        /// <summary>
        /// 结果为false时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        public static T IfFalse<T>(this bool value, T t) => !value ? t : default;

        /// <summary>
        /// 结果为false时，输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="t">输出参数</param>
        public static T IfFalse<T>(this bool? value, T t) => !value.GetValueOrDefault() ? t : default;

        #endregion

        #region IfFalse(结果为false时，执行方法)

        /// <summary>
        /// 结果为false时，执行方法
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="action">执行方法</param>
        public static void IfFalse(this bool value, Action action)
        {
            if (!value)
                action();
        }

        /// <summary>
        /// 结果为false时，执行方法
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="action">执行方法</param>
        public static void IfFalse(this bool? value, Action action)
        {
            if (!value.GetValueOrDefault())
                action();
        }

        #endregion
    }
}
