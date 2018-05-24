using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Utils.Extensions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 类型转换操作
    /// </summary>
    public static class Conv
    {
        #region ToByte(转换为byte)

        /// <summary>
        /// 转换为8位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static byte ToByte(object input)
        {
            return ToByte(input, default(byte));
        }

        /// <summary>
        /// 转换为8位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static byte ToByte(object input, byte defaultValue)
        {
            return ToByteOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为8位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static byte? ToByteOrNull(object input)
        {
            byte result;
            var success = byte.TryParse(input.SafeString(), out result);
            if (success)
            {
                return result;
            }
            return null;
        }

        #endregion

        #region ToChar(转换为char)

        /// <summary>
        /// 转换为字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static char ToChar(object input)
        {
            return ToChar(input, default(char));
        }

        /// <summary>
        /// 转换为字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static char ToChar(object input, char defaultValue)
        {
            return ToCharOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为可空字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static char? ToCharOrNull(object input)
        {
            char result;
            var success = char.TryParse(input.SafeString(), out result);
            if (success)
            {
                return result;
            }
            return null;
        }

        #endregion

        #region ToShort(转换为short)

        /// <summary>
        /// 转换为16位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static short ToShort(object input)
        {
            return ToShort(input, default(short));
        }

        /// <summary>
        /// 转换为16位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static short ToShort(object input, short defaultValue)
        {
            return ToShortOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为16位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static short? ToShortOrNull(object input)
        {
            short result;
            var success = short.TryParse(input.SafeString(), out result);
            if (success)
            {
                return result;
            }
            return null;
        }

        #endregion

        #region ToInt(转换为int)
        /// <summary>
        /// 转换为32位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static int ToInt(object input)
        {
            return ToInt(input, default(int));
        }

        /// <summary>
        /// 转换为32位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static int ToInt(object input, int defaultValue)
        {
            return ToIntOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为32位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static int? ToIntOrNull(object input)
        {
            int result;
            var success = int.TryParse(input.SafeString(), out result);
            if (success)
            {
                return result;
            }
            try
            {
                var temp = ToDoubleOrNull(input, 0);
                if (temp == null)
                {
                    return null;
                }
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
        /// <returns></returns>
        public static long ToLong(object input)
        {
            return ToLong(input, default(long));
        }

        /// <summary>
        /// 转换为64位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static long ToLong(object input, long defaultValue)
        {
            return ToLongOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为64位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static long? ToLongOrNull(object input)
        {
            long result;
            var success = long.TryParse(input.SafeString(), out result);
            if (success)
            {
                return result;
            }
            try
            {
                var temp = ToDecimalOrNull(input, 0);
                if (temp == null)
                {
                    return null;
                }
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
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static float ToFloat(object input, int? digits = null)
        {
            return ToFloat(input, default(float), digits);
        }

        /// <summary>
        /// 转换为32位浮点型，并按指定小数位舍入
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static float ToFloat(object input, float defaultValue, int? digits = null)
        {
            return ToFloatOrNull(input, digits) ?? defaultValue;
        }

        /// <summary>
        /// 转换为32位可空浮点型，并按指定小数位舍入
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static float? ToFloatOrNull(object input, int? digits = null)
        {
            float result;
            var success = float.TryParse(input.SafeString(), out result);
            if (!success)
            {
                return null;
            }
            if (digits == null)
            {
                return result;
            }
            return (float)Math.Round(result, digits.Value);
        }
        #endregion

        #region ToDouble(转换为double)
        /// <summary>
        /// 转换为64位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static double ToDouble(object input, int? digits = null)
        {
            return ToDouble(input, default(double), digits);
        }

        /// <summary>
        /// 转换为64位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static double ToDouble(object input, double defaultValue, int? digits = null)
        {
            return ToDoubleOrNull(input, digits) ?? defaultValue;
        }

        /// <summary>
        /// 转换为64位可空浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static double? ToDoubleOrNull(object input, int? digits = null)
        {
            double result;
            var success = double.TryParse(input.SafeString(), out result);
            if (!success)
            {
                return null;
            }
            return digits == null ? result : Math.Round(result, digits.Value);
        }
        #endregion

        #region ToDecimal(转换为decimal)
        /// <summary>
        /// 转换为128位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static decimal ToDecimal(object input, int? digits = null)
        {
            return ToDecimal(input, default(decimal), digits);
        }

        /// <summary>
        /// 转换为128位浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static decimal ToDecimal(object input, decimal defaultValue, int? digits = null)
        {
            return ToDecimalOrNull(input, digits) ?? defaultValue;
        }

        /// <summary>
        /// 转换为128位可空浮点型，并按指定小数位舍入，温馨提示：4舍6入5成双
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        /// <returns></returns>
        public static decimal? ToDecimalOrNull(object input, int? digits = null)
        {
            decimal result;
            var success = decimal.TryParse(input.SafeString(), out result);
            if (!success)
            {
                return null;
            }
            if (digits == null)
            {
                return result;
            }
            return Math.Round(result, digits.Value);
        }
        #endregion

        #region ToBool(转换为bool)
        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static bool ToBool(object input)
        {
            return ToBool(input, default(bool));
        }

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static bool ToBool(object input, bool defaultValue)
        {
            return ToBoolOrNull(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为可空布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static bool? ToBoolOrNull(object input)
        {
            bool? value = GetBool(input);
            if (value != null)
            {
                return value.Value;
            }
            bool result;
            return bool.TryParse(input.SafeString(), out result) ? (bool?)result : null;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
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
        /// <returns></returns>
        public static DateTime ToDate(object input)
        {
            return ToDateOrNull(input) ?? DateTime.MinValue;
        }

        /// <summary>
        /// 转换为可空日期
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static DateTime? ToDateOrNull(object input)
        {
            DateTime result;
            return DateTime.TryParse(input.SafeString(), out result) ? (DateTime?)result : null;
        }
        #endregion

        #region ToGuid(转换为Guid)
        /// <summary>
        /// 转换为Guid
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static Guid ToGuid(object input)
        {
            return ToGuidOrNull(input) ?? Guid.Empty;
        }

        /// <summary>
        /// 转换为可空Guid
        /// </summary>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static Guid? ToGuidOrNull(object input)
        {
            Guid result;
            return Guid.TryParse(input.SafeString(), out result) ? (Guid?)result : null;
        }

        /// <summary>
        /// 转换为Guid集合
        /// </summary>
        /// <param name="input">输入值，以逗号分隔的Guid集合字符串，范例：83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A</param>
        /// <returns></returns>
        public static List<Guid> ToGuidList(string input)
        {
            return ToList<Guid>(input);
        }
        #endregion

        #region ToList(泛型集合转换)
        /// <summary>
        /// 泛型集合转换
        /// </summary>
        /// <typeparam name="T">目标元素类型</typeparam>
        /// <param name="input">输入值，以逗号分隔的元素集合字符串，范例：83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A</param>
        /// <returns></returns>
        public static List<T> ToList<T>(string input)
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(input))
            {
                return result;
            }
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
        /// <returns></returns>
        public static T ToEnum<T>(object input) where T : struct
        {
            return ToEnum<T>(input, default(T));
        }

        /// <summary>
        /// 转换为枚举
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="input">输入值</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static T ToEnum<T>(object input, T defaultValue) where T : struct
        {
            return ToEnumOrNull<T>(input) ?? defaultValue;
        }

        /// <summary>
        /// 转换为可空枚举
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static T? ToEnumOrNull<T>(object input) where T : struct
        {
            T result;
            var success = System.Enum.TryParse(input.SafeString(), true, out result);
            if (success)
            {
                return result;
            }
            return null;
        }

        #endregion

        #region To(通用泛型转换)
        /// <summary>
        /// 通用泛型转换
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="input">输入值</param>
        /// <returns></returns>
        public static T To<T>(object input)
        {
            if (input == null)
            {
                return default(T);
            }
            if (input is string && string.IsNullOrWhiteSpace(input.ToString()))
            {
                return default(T);
            }
            Type type = Common.GetType<T>();
            var typeName = type.Name.ToLower();
            try
            {
                if (typeName == "string")
                {
                    return (T)(object)input.ToString();
                }
                if (typeName == "guid")
                {
                    return (T)(object)new Guid(input.ToString());
                }
                if (type.IsEnum)
                {
                    return Enum.Parse<T>(input);
                }
                if (input is IConvertible)
                {
                    return (T)System.Convert.ChangeType(input, type);
                }
                return (T)input;
            }
            catch
            {
                return default(T);
            }
        }
        #endregion
    }
}
