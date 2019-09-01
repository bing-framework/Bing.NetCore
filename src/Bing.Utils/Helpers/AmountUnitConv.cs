using System;
using System.Globalization;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 金额单位转换操作
    /// </summary>
    public static class AmountUnitConv
    {
        /// <summary>
        /// 分转元
        /// </summary>
        /// <param name="fen">分</param>
        public static decimal ToYuan(int fen) => Conv.ToDecimal((decimal)fen / 100, 2);

        /// <summary>
        /// 分转元
        /// </summary>
        /// <param name="fen">分</param>
        public static decimal ToYuan(int? fen) => fen == null ? 0 : Conv.ToDecimal((decimal)fen / 100, 2);

        /// <summary>
        /// 元转分
        /// </summary>
        /// <param name="yuan">元</param>
        public static int ToFen(decimal yuan) => Conv.ToInt(CutDecimalWithN(yuan, 2) * 100, 0);

        /// <summary>
        /// 元转分
        /// </summary>
        /// <param name="yuan">元</param>
        public static int ToFen(decimal? yuan)=>yuan==null?0 : Conv.ToInt(CutDecimalWithN(yuan.Value, 2) * 100, 0);

        /// <summary>
        /// 截取保留N位小数且不进行四舍五入
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="digits">小数位数</param>
        private static decimal CutDecimalWithN(decimal input, int digits)
        {
            var decimalStr = input.ToString(CultureInfo.InvariantCulture);
            var index = decimalStr.IndexOf(".", StringComparison.Ordinal);
            if (index == -1 || decimalStr.Length < index + digits + 1)
            {
                decimalStr = string.Format("{0:F" + digits + "}", input);
            }
            else
            {
                var length = index;
                if (digits != 0)
                    length = index + digits + 1;
                decimalStr = decimalStr.Substring(0, length);
            }
            return decimal.Parse(decimalStr);
        }

        /// <summary>
        /// 保留两位小数
        /// </summary>
        /// <param name="input">输入值</param>
        public static string ToN2String(decimal input) => $"{input:N2}";
    }
}
