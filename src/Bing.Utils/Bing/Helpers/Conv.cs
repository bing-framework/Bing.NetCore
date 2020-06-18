using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bing.Extensions;

namespace Bing.Helpers
{
    /// <summary>
    /// 类型转换 操作
    /// </summary>
    public static partial class Conv
    {
        #region ToByte(转换为byte)

        /// <summary>
        /// 转换为8位整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static byte ToByte(object input) => ToByte(input, default);

        /// <summary>
        /// 转换为8位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static byte ToByte(object input, byte defaultValue) => ToByteOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为8位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static byte? ToByteOrNull(object input)
        {
            var success = byte.TryParse(input.SafeString(), out var result);
            if (success)
                return result;
            try
            {
                var temp = ToDoubleOrNull(input, 0);
                if (temp == null)
                    return null;
                return Convert.ToByte(temp);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region ToChar(转换为char)

        /// <summary>
        /// 转换为字符
        /// </summary>
        /// <param name="input">输入值</param>
        public static char ToChar(object input) => ToChar(input, default);

        /// <summary>
        /// 转换为字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static char ToChar(object input, char defaultValue) => ToCharOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为可空字符
        /// </summary>
        /// <param name="input">输入值</param>
        public static char? ToCharOrNull(object input)
        {
            var success = char.TryParse(input.SafeString(), out var result);
            if (success)
                return result;
            return null;
        }

        #endregion

        #region ToShort(转换为short)

        /// <summary>
        /// 转换为16位整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static short ToShort(object input) => ToShort(input, default);

        /// <summary>
        /// 转换为16位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static short ToShort(object input, short defaultValue) => ToShortOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为16位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static short? ToShortOrNull(object input)
        {
            var success = short.TryParse(input.SafeString(), out var result);
            if (success)
                return result;
            try
            {
                var temp = ToDoubleOrNull(input, 0);
                if (temp == null)
                    return null;
                return Convert.ToInt16(temp);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region ToInt(转换为int)

        /// <summary>
        /// 转换为32位整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static int ToInt(object input) => ToInt(input, default);

        /// <summary>
        /// 转换为32位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static int ToInt(object input, int defaultValue) => ToIntOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为32位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static int? ToIntOrNull(object input)
        {
            var success = int.TryParse(input.SafeString(), out var result);
            if (success)
                return result;
            try
            {
                var temp = ToDoubleOrNull(input, 0);
                if (temp == null)
                    return null;
                return System.Convert.ToInt32(temp);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region ToLong(转换为long)

        /// <summary>
        /// 转换为64位整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static long ToLong(object input) => ToLong(input, default);

        /// <summary>
        /// 转换为64位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static long ToLong(object input, long defaultValue) => ToLongOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为64位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        public static long? ToLongOrNull(object input)
        {
            var success = long.TryParse(input.SafeString(), out var result);
            if (success)
                return result;
            try
            {
                var temp = ToDecimalOrNull(input, 0);
                if (temp == null)
                    return null;
                return System.Convert.ToInt64(temp);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region ToFloat(转换为float)

        /// <summary>
        /// 转换为32位浮点型，并按指定小数位舍入
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static float ToFloat(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToFloat(input, default, digits, mode);

        /// <summary>
        /// 转换为32位浮点型，并按指定小数位舍入
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static float ToFloat(object input, float defaultValue, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToFloatOrNull(input, digits, mode) ?? defaultValue;

        /// <summary>
        /// 转换为32位可空浮点型，并按指定小数位舍入
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static float? ToFloatOrNull(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            var success = float.TryParse(input.SafeString(), out var result);
            if (!success)
                return null;
            if (digits == null)
                return result;
            return (float)Math.Round(result, digits.Value, mode);
        }

        #endregion

        #region ToDouble(转换为double)

        /// <summary>
        /// 转换为64位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static double ToDouble(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToDouble(input, default, digits, mode);

        /// <summary>
        /// 转换为64位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static double ToDouble(object input, double defaultValue, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToDoubleOrNull(input, digits, mode) ?? defaultValue;

        /// <summary>
        /// 转换为64位可空浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static double? ToDoubleOrNull(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            var success = double.TryParse(input.SafeString(), out var result);
            if (!success)
                return null;
            return digits == null ? result : Math.Round(result, digits.Value, mode);
        }

        #endregion

        #region ToDecimal(转换为decimal)

        /// <summary>
        /// 转换为128位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static decimal ToDecimal(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToDecimal(input, default, digits, mode);

        /// <summary>
        /// 转换为128位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static decimal ToDecimal(object input, decimal defaultValue, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero) => ToDecimalOrNull(input, digits, mode) ?? defaultValue;

        /// <summary>
        /// 转换为128位可空浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// <para>采用Banker's rounding（银行家算法），即：四舍六入五取偶。事实上这也是IEEE的规范。</para>
        /// <para>备注：<see cref="MidpointRounding.AwayFromZero"/>可以用来实现传统意义上的"四舍五入"。</para>
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <param name="mode">可选择模式</param>
        public static decimal? ToDecimalOrNull(object input, int? digits = null, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            var success = decimal.TryParse(input.SafeString(), out var result);
            if (!success)
                return null;
            return digits == null ? result : Math.Round(result, digits.Value, mode);
        }

        #endregion

        #region ToBool(转换为bool)

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        public static bool ToBool(object input) => ToBool(input, default);

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static bool ToBool(object input, bool defaultValue) => ToBoolOrNull(input) ?? defaultValue;

        /// <summary>
        /// 转换为可空布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        public static bool? ToBoolOrNull(object input)
        {
            bool? value = GetBool(input);
            if (value != null)
                return value.Value;
            return bool.TryParse(input.SafeString(), out var result) ? (bool?)result : null;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        private static bool? GetBool(object input)
        {
            switch (input.SafeString().ToLower())
            {
                case "0":
                case "否":
                case "不":
                case "no":
                case "fail":
                    return false;

                case "1":
                case "是":
                case "ok":
                case "yes":
                    return true;

                default:
                    return null;
            }
        }

        #endregion

        #region ToDate(转换为DateTime)

        /// <summary>
        /// 转换为日期
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static DateTime ToDate(object input, DateTime defaultValue = default) => ToDateOrNull(input) ?? DateTime.MinValue;

        /// <summary>
        /// 转换为可空日期
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static DateTime? ToDateOrNull(object input, DateTime? defaultValue = null)
        {
            if (input == null)
                return defaultValue;
            return DateTime.TryParse(input.SafeString(), out var result) ? result : defaultValue;
        }

        #endregion

        #region ToGuid(转换为Guid)

        /// <summary>
        /// 转换为Guid
        /// </summary>
        /// <param name="input">输入值</param>
        public static Guid ToGuid(object input) => ToGuidOrNull(input) ?? Guid.Empty;

        /// <summary>
        /// 转换为可空Guid
        /// </summary>
        /// <param name="input">输入值</param>
        public static Guid? ToGuidOrNull(object input) => Guid.TryParse(input.SafeString(), out var result) ? (Guid?)result : null;

        /// <summary>
        /// 转换为Guid集合
        /// </summary>
        /// <param name="input">输入值，以逗号分隔的Guid集合字符串，范例：83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A</param>
        public static List<Guid> ToGuidList(string input) => ToList<Guid>(input);

        #endregion

        #region ToList(泛型集合转换)

        /// <summary>
        /// 泛型集合转换
        /// </summary>
        /// <typeparam name="T">目标元素类型</typeparam>
        /// <param name="input">输入值，以逗号分隔的元素集合字符串，范例：83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A</param>
        public static List<T> ToList<T>(string input)
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(input))
                return result;
            var array = input.Split(',');
            result.AddRange(from each in array where !string.IsNullOrWhiteSpace(each) select To<T>(each));
            return result;
        }

        #endregion

        #region ToEnum(转换为枚举)

        /// <summary>
        /// 转换为枚举
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="input">输入值</param>
        public static T ToEnum<T>(object input) where T : struct => ToEnum<T>(input, default);

        /// <summary>
        /// 转换为枚举
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        public static T ToEnum<T>(object input, T defaultValue) where T : struct => ToEnumOrNull<T>(input) ?? defaultValue;

        /// <summary>
        /// 转换为可空枚举
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="input">输入值</param>
        public static T? ToEnumOrNull<T>(object input) where T : struct
        {
            var success = System.Enum.TryParse(input.SafeString(), true, out T result);
            if (success)
                return result;
            return null;
        }

        #endregion

        #region To(通用泛型转换)

        /// <summary>
        /// 通用泛型转换
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="input">输入值</param>
        public static T To<T>(object input)
        {
            if (input == null)
                return default;
            if (input is string && string.IsNullOrWhiteSpace(input.ToString()))
                return default;

            var type = Common.GetType<T>();
            var typeName = type.Name.ToLower();
            try
            {
                if (typeName == "string")
                    return (T)(object)input.ToString();
                if (typeName == "guid")
                    return (T)(object)new Guid(input.ToString());
                if (type.IsEnum)
                    return Enum.Parse<T>(input);
                if (input is IConvertible)
                    return (T)System.Convert.ChangeType(input, type);
                return (T)input;
            }
            catch
            {
                return default;
            }
        }

        #endregion

        #region ToAmountInWords(转换为大写金额)

        /// <summary>
        /// 转换为大写金额
        /// </summary>
        /// <param name="input">输入值</param>
        public static string ToAmountInWords(object input)
        {
            string tempValue;
            if (input is string valueStr)
                tempValue = valueStr;
            else
                tempValue = input.ToString();
            if (!decimal.TryParse(tempValue, out var decValue))
                return tempValue;
            tempValue = decValue.ToString("#L#E#D#C#K#E#D#C#J#E#D#C#I#E#D#C#H#E#D#C#G#E#D#C#F#E#D#C#.0B0A");
            var temp = Regex.Replace(tempValue,
                @"((?<=-|^)[^1-9]*)|((?'z'0)[0A-E]*((?=[1-9])|(?'-z'(?=[F-L\.]|$))))|((?'b'[F-L])(?'z'0)[0A-L]*((?=[1-9])|(?'-z'(?=[\.]|$))))",
                "${b}${z}");
            var result = Regex.Replace(temp, ".", m => "负元空零壹贰叁肆伍陆柒捌玖空空空空空空空分角拾佰仟万亿兆京垓秭穰"[m.Value[0] - '-'].ToString());
            return result;
        }

        #endregion
    }
}
