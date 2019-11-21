using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 类型转换
    /// </summary>
    public static partial class Extensions
    {
        #region SafeString(安全转换为字符串)

        /// <summary>
        /// 安全转换为字符串，去除两端空格，当值为null时返回""
        /// </summary>
        /// <param name="input">输入值</param>
        public static string SafeString(this object input) => input == null ? string.Empty : input.ToString().Trim();

        #endregion

        #region ToBool(转换为bool)

        /// <summary>
        /// 转换为bool
        /// </summary>
        /// <param name="obj">数据</param>
        public static bool ToBool(this string obj) => Utils.Helpers.Conv.ToBool(obj);

        /// <summary>
        /// 转换为可空bool
        /// </summary>
        /// <param name="obj">数据</param>
        public static bool? ToBoolOrNull(this string obj) => Utils.Helpers.Conv.ToBoolOrNull(obj);

        #endregion

        #region ToInt(转换为int)

        /// <summary>
        /// 转换为int
        /// </summary>
        /// <param name="obj">数据</param>
        public static int ToInt(this string obj) => Utils.Helpers.Conv.ToInt(obj);

        /// <summary>
        /// 转换为可空int
        /// </summary>
        /// <param name="obj">数据</param>
        public static int? ToIntOrNull(this string obj) => Utils.Helpers.Conv.ToIntOrNull(obj);

        #endregion

        #region ToLong(转换为long)

        /// <summary>
        /// 转换为long
        /// </summary>
        /// <param name="obj">数据</param>
        public static long ToLong(this string obj) => Utils.Helpers.Conv.ToLong(obj);

        /// <summary>
        /// 转换为可空long
        /// </summary>
        /// <param name="obj">数据</param>
        public static long? ToLongOrNull(this string obj) => Utils.Helpers.Conv.ToLongOrNull(obj);

        #endregion

        #region ToDouble(转换为double)

        /// <summary>
        /// 转换为double
        /// </summary>
        /// <param name="obj">数据</param>
        public static double ToDouble(this string obj) => Utils.Helpers.Conv.ToDouble(obj);

        /// <summary>
        /// 转换为可空double
        /// </summary>
        /// <param name="obj">数据</param>
        public static double? ToDoubleOrNull(this string obj) => Utils.Helpers.Conv.ToDoubleOrNull(obj);

        #endregion

        #region ToDecimal(转换为decimal)

        /// <summary>
        /// 转换为decimal
        /// </summary>
        /// <param name="obj">数据</param>
        public static decimal ToDecimal(this string obj) => Utils.Helpers.Conv.ToDecimal(obj);

        /// <summary>
        /// 转换为可空decimal
        /// </summary>
        /// <param name="obj">数据</param>
        public static decimal? ToDecimalOrNull(this string obj) => Utils.Helpers.Conv.ToDecimalOrNull(obj);

        #endregion

        #region ToDate(转换为日期)

        /// <summary>
        /// 转换为日期
        /// </summary>
        /// <param name="obj">数据</param>
        public static DateTime ToDate(this string obj) => Utils.Helpers.Conv.ToDate(obj);

        /// <summary>
        /// 转换为可空日期
        /// </summary>
        /// <param name="obj">数据</param>
        public static DateTime? ToDateOrNull(this string obj) => Utils.Helpers.Conv.ToDateOrNull(obj);

        #endregion

        #region ToGuid(转换为Guid)

        /// <summary>
        /// 转化为Guid
        /// </summary>
        /// <param name="obj">数据</param>
        public static Guid ToGuid(this string obj) => Utils.Helpers.Conv.ToGuid(obj);

        /// <summary>
        /// 转换为可空Guid
        /// </summary>
        /// <param name="obj">数据</param>
        public static Guid? ToGuidOrNull(this string obj) => Utils.Helpers.Conv.ToGuidOrNull(obj);

        /// <summary>
        /// 转换为Guid集合
        /// </summary>
        /// <param name="obj">数据，范例："83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A"</param>
        public static List<Guid> ToGuidList(this string obj) => Utils.Helpers.Conv.ToGuidList(obj);

        /// <summary>
        /// 转换为Guid集合
        /// </summary>
        /// <param name="obj">字符串集合</param>
        public static List<Guid> ToGuidList(this IList<string> obj) => obj == null ? new List<Guid>() : obj.Select(t => t.ToGuid()).ToList();

        #endregion

        #region ToSnakeCase(将字符串转换为蛇形策略)

        /// <summary>
        /// 将字符串转换为蛇形策略
        /// </summary>
        /// <param name="str">字符串</param>
        public static string ToSnakeCase(this string str) => Bing.Utils.Helpers.Str.ToSnakeCase(str);

        #endregion

        #region ToCamelCase(将字符串转换为骆驼策略)

        /// <summary>
        /// 将字符串转换为骆驼策略
        /// </summary>
        /// <param name="str">字符串</param>
        public static string ToCamelCase(this string str) => Bing.Utils.Helpers.Str.ToCamelCase(str);

        #endregion
    }
}
