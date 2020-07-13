using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 系统扩展 - 数字
    /// </summary>
    public static partial class BingExtensions
    {
        #region KeepDigits(保留小数位数)

        /// <summary>
        /// 保留小数位数。四舍五入
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="digits">小数位数</param>
        public static float KeepDigits(this float value, int digits) =>
            (float)Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero);

        /// <summary>
        /// 保留小数位数。四舍五入
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="digits">小数位数</param>
        public static double KeepDigits(this double value, int digits) =>
            (double)Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero);

        /// <summary>
        /// 保留小数位数。四舍五入
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="digits">小数位数</param>
        public static decimal KeepDigits(this decimal value, int digits) =>
            Math.Round(value, digits, MidpointRounding.AwayFromZero);

        #endregion

        #region IsIn(是否在给定闭区间)

        /// <summary>
        /// 判断 byte 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this byte value, byte min, byte max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 short 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this short value, short min, short max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 int 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this int value, int min, int max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 long 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this long value, long min, long max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 float 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this float value, float min, float max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 double 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this double value, double min, double max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        /// <summary>
        /// 判断 decimal 是否在给定闭区间
        /// </summary>
        /// <param name="value">数据</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static bool IsIn(this decimal value, decimal min, decimal max)
        {
            if (min > max)
                throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
            return value >= min && value <= max;
        }

        #endregion
    }
}
