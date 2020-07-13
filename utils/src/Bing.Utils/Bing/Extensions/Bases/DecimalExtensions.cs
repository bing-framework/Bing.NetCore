using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Extensions.Bases
{
    /// <summary>
    /// <see cref="Decimal"/> 扩展
    /// </summary>
    public static class DecimalExtensions
    {
        #region Rounding(将数值四舍五入，保留指定小数位数)

        /// <summary>
        /// 将数值四舍五入，保留两位小数
        /// </summary>
        /// <param name="value">值</param>
        public static decimal Rounding(this decimal value) => Math.Round(value, 2);

        /// <summary>
        /// 将数值四舍五入，保留指定小数位数
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="decimals">小数位数</param>
        public static decimal Rounding(this decimal value, int decimals) => Math.Round(value, decimals);

        #endregion

        #region Abs(返回数字的绝对值)

        /// <summary>
        /// 返回数字的绝对值
        /// </summary>
        /// <param name="value">值</param>
        public static decimal Abs(this decimal value) => Math.Abs(value);

        /// <summary>
        /// 返回数字的绝对值
        /// </summary>
        /// <param name="values">值</param>
        public static IEnumerable<decimal> Abs(this IEnumerable<decimal> values) => values.Select(x => x.Abs());

        #endregion
    }
}
