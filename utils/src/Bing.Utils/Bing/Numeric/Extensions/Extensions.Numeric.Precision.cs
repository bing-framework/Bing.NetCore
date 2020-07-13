using System;
using System.Diagnostics.Contracts;
using System.Globalization;

// ReSharper disable once CheckNamespace
namespace Bing.Numeric
{
    /// <summary>
    /// 数值 扩展
    /// </summary>
    public static partial class NumericExtensions
    {
        /// <summary>
        /// 是否为0
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsZero(this float value) => IsPreciseEqual(value, 0f);

        /// <summary>
        /// 是否为0
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsZero(this double value) => IsPreciseEqual(value, 0d);

        /// <summary>
        /// 是否接近零。用于验证双公差浮点值是否被视为零
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="precision">精度</param>
        public static bool IsNearZero(this double value, double precision = 0.001) => IsNearEqual(value, 0d, precision);

        /// <summary>
        /// 是否接近相等。使用给定的公差来验证潜在浮点值是否足够接近另外一个值
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="tolerance">公差</param>
        public static bool IsNearEqual(this float a, float b, float tolerance) =>
            a.Equals(b) || float.IsNaN(a) && float.IsNaN(b) || Math.Abs(a - b) < tolerance;

        /// <summary>
        /// 是否接近相等。使用给定的公差来验证潜在浮点值是否足够接近另外一个值
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="tolerance">公差</param>
        public static bool IsNearEqual(this double a, double b, double tolerance) =>
            a.Equals(b) || double.IsNaN(a) && double.IsNaN(b) || Math.Abs(a - b) < tolerance;

        /// <summary>
        /// 是否接近相等。使用给定的公差来验证潜在浮点值是否足够接近另外一个值
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="tolerance">公差</param>
        public static bool IsNearEqual(this decimal a, decimal b, decimal tolerance) => a.Equals(b) || Math.Abs(a - b) < tolerance;

        /// <summary>
        /// 是否相对接近相等。使用给定的公差来验证十进制的值是否接近另外一个值
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="minDecimalPlaces">最小小数位数</param>
        public static bool IsRelativeNearEqual(this double a, double b, uint minDecimalPlaces)
        {
            var tolerance = 1 / Math.Pow(10, minDecimalPlaces);
            if (a.IsNearEqual(b, tolerance))
                return true;
            if (double.IsNaN(a) || double.IsNaN(b))
                return false;
            var d = Math.Min(a.DecimalPlaces(), b.DecimalPlaces());
            var divisor = Math.Pow(0, minDecimalPlaces - d);
            a /= divisor;
            b /= divisor;
            return a.IsNearEqual(b, tolerance);
        }

        /// <summary>
        /// 是否精确相等。验证值是否在ε公差范围内相等
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="stringValidate">是否验证字符串</param>
        public static bool IsPreciseEqual(this double a, double b, bool stringValidate = false) =>
            IsNearEqual(a, b, double.Epsilon) || (stringValidate && !double.IsNaN(a) && !double.IsNaN(b) &&
                                                 a.ToString(CultureInfo.InvariantCulture) ==
                                                 b.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 是否精确相等。验证值是否在ε公差范围内相等
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="stringValidate">是否验证字符串</param>
        public static bool IsPreciseEqual(this double? a, double? b, bool stringValidate = false) =>
            !a.HasValue && !b.HasValue ||
            a.HasValue && b.HasValue && a.Value.IsPreciseEqual(b.Value, stringValidate);

        /// <summary>
        /// 是否精确相等。验证值是否在ε公差范围内相等
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="stringValidate">是否验证字符串</param>
        public static bool IsPreciseEqual(this float a, float b, bool stringValidate = false) =>
            IsNearEqual(a, b, float.Epsilon) || (stringValidate && !float.IsNaN(a) && !float.IsNaN(b) &&
                                                 a.ToString(CultureInfo.InvariantCulture) ==
                                                 b.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 是否精确相等。验证值是否在ε公差范围内相等
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="stringValidate">是否验证字符串</param>
        public static bool IsPreciseEqual(this float? a, float? b, bool stringValidate = false) =>
            !a.HasValue && !b.HasValue ||
            a.HasValue && b.HasValue && a.Value.IsPreciseEqual(b.Value, stringValidate);

        /// <summary>
        /// 是否接近相等。使用给定的公差来验证潜在浮点值是否足够接近另外一个值
        /// </summary>
        /// <param name="a">比较的值</param>
        /// <param name="b">比较的值</param>
        /// <param name="tolerance">公差</param>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool IsNearEqual(this IComparable a, IComparable b, IComparable tolerance)
        {
            if (a == null)
                throw new NullReferenceException();
            if (b == null)
                throw new ArgumentNullException(nameof(b));
            Contract.EndContractBlock();
            if (a.Equals(b))
                return true;
            return a switch
            {
                float f => IsNearEqual(f, (float)b, (float)tolerance),
                double d => IsNearEqual(d, (double)b, (double)tolerance),
                decimal @decimal => IsNearEqual(@decimal, (decimal)b, (decimal)tolerance),
                _ => throw new InvalidCastException(),
            };
        }
    }
}
