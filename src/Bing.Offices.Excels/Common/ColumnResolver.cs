using System;

namespace Bing.Offices.Excels.Common
{
    /// <summary>
    /// 单元列解析器
    /// </summary>
    public class ColumnResolver
    {
        /// <summary>
        /// 获取单元列索引，根据字母
        /// </summary>
        /// <param name="columnAddress">列地址</param>
        /// <returns></returns>
        public static int GetColumnIndexByAlpha(string columnAddress)
        {
            int[] digits = new int[columnAddress.Length];
            for (var i = 0; i < columnAddress.Length; ++i)
            {
                digits[i] = Convert.ToInt32(columnAddress[i]) - 64;
            }

            int mul = 1;
            int res = 0;
            for (var pos = digits.Length - 1; pos >= 0; --pos)
            {
                res += digits[pos] * mul;
                mul *= 26;
            }
            return res - 1;
        }
    }
}
