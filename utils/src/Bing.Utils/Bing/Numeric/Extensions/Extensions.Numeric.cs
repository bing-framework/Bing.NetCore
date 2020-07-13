using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using Bing.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Numeric
{
    /// <summary>
    /// 数值 扩展
    /// </summary>
    public static partial class NumericExtensions
    {
        /// <summary>
        /// 是否NaN
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsNaN(this double value) => double.IsNaN(value);

        /// <summary>
        /// 是否NaN
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsNaN(this float value) => float.IsNaN(value);

        /// <summary>
        /// 是否默认值
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsDefault(this double value) => value.Equals(default);

        /// <summary>
        /// 转换为decimal。通过先转换为字符串将浮点数转换为小数的准确方法，避免公差问题
        /// </summary>
        /// <param name="value">值</param>
        public static decimal ToDecimal(this float value) => Conv.ToDecimal(value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 转换为double。通过将有限值四舍五入到小数点公差级别来将 float 转换为 double 的准确方法。
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="precision">精度</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static double ToDouble(this float value, int precision)
        {
            if (precision < 0 || precision > 15)
                throw new ArgumentOutOfRangeException(nameof(precision), precision, "Must be between 0 and 15.");
            Contract.EndContractBlock();
            var result = value.ReturnZeroIfFinite();
            return result.IsZero() ? Math.Round(value, precision) : result;
        }

        /// <summary>
        /// 转换为double。 通过首先转换为字符串将 float 转换为 double 的准确方法。 避免公差问题。
        /// </summary>
        /// <param name="value">值</param>
        public static double ToDouble(this float value)
        {
            var result = value.ReturnZeroIfFinite();
            return result.IsZero() ? Conv.ToDouble(value.ToString(CultureInfo.InvariantCulture)) : result;
        }

        /// <summary>
        /// 转换为double。通过首先转换为字符串来将可能的 float 转换为 double 的准确方法。 避免公差问题。
        /// </summary>
        /// <param name="value">值</param>
        public static double ToDouble(this float? value) => value?.ToDouble() ?? double.NaN;

        /// <summary>
        /// 转换为double。通过将有限值四舍五入到小数点公差级别，将可能的浮点数转换为双精度值的准确方法。
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="precision">精度</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static double ToDouble(this float? value, int precision)
        {
            if (precision < 0 || precision > 15)
                throw new ArgumentOutOfRangeException(nameof(precision), precision, "Must be between 0 and 15.");
            Contract.EndContractBlock();
            return value?.ToDouble(precision) ?? double.NaN;
        }

        /// <summary>
        /// 修正零。如果将双公差浮点值视为零（在ε公差内），则返回真零。
        /// </summary>
        /// <param name="value">值</param>
        public static double FixZero(this double value) => !value.Equals(0) && value.IsZero() ? 0 : value;

        /// <summary>
        /// 如果有限小数则返回零
        /// </summary>
        /// <param name="value">值</param>
        private static double ReturnZeroIfFinite(this float value)
        {
            if (float.IsNegativeInfinity(value))
                return double.NegativeInfinity;
            if (float.IsPositiveInfinity(value))
                return double.PositiveInfinity;
            return float.IsNaN(value) ? double.NaN : 0D;
        }

        /// <summary>
        /// 返回最后零位之前的小数位数
        /// </summary>
        /// <param name="value">值</param>
        public static int DecimalPlaces(this double value)
        {
            if (value.IsNaN())
                return 0;
            var valueString = value.ToString(CultureInfo.InvariantCulture);
            var index = valueString.IndexOf('.');
            return index == -1 ? 0 : valueString.Length - index - 1;
        }

        /// <summary>
        /// 精确求和。通过消除意外的不准确性来确保附加公差
        /// </summary>
        /// <param name="source">源值</param>
        /// <param name="value">值</param>
        public static double SumAccurate(this double source, double value)
        {
            var result = source + value;
            var vp = source.DecimalPlaces();
            if (vp > 15)
                return result;
            var ap = value.DecimalPlaces();
            if (ap > 15)
                return result;
            var digits = Math.Max(vp, ap);
            return Math.Round(result, digits);
        }

        /// <summary>
        /// 精确乘积。通过消除意外的不准确性来确保附加公差
        /// </summary>
        /// <param name="source">源值</param>
        /// <param name="value">值</param>
        public static double ProductAccurate(this double source, double value)
        {
            var result = source * value;
            var vp = source.DecimalPlaces();
            if (vp > 15)
                return result;
            var ap = value.DecimalPlaces();
            if (ap > 15)
                return result;
            var digits = Math.Max(vp, ap);
            return Math.Round(result, digits);
        }

        /// <summary>
        /// 求和。通过使用整数来确保加法公差
        /// </summary>
        /// <param name="source">源值</param>
        /// <param name="value">值</param>
        public static double SumUsingIntegers(this double source, double value)
        {
            var x = Math.Pow(10, Math.Max(source.DecimalPlaces(), value.DecimalPlaces()));
            var v = (long)(source * x);
            var a = (long)(value * x);
            var result = v + a;
            return result / x;
        }
    }
}
