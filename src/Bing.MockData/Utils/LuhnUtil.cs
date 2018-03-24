using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.MockData.Utils
{
    /// <summary>
    /// Luhn 算法工具
    /// </summary>
    public class LuhnUtil
    {
        /// <summary>
        /// 获取 Luhn 校验位。
        /// 从不含校验位的银行卡卡号采用 Luhn 校验算法获得校验位
        /// 该校验的过程：
        /// 1、从卡号最后一位数字开始，逆向将奇数位（1、3、5等等）相加。
        /// 2、从卡号最后一位数字开始，逆向将偶数位数字，先乘以2（如果乘积为两位数，则将其减去9），再就和。
        /// 3、将奇数位总和加上偶数位总和，结果应该可以被10整除。
        /// </summary>
        /// <param name="chs"></param>
        /// <returns></returns>
        public static int GetLuhnSum(char[] chs)
        {
            int luhnSum = 0;
            for (int i = chs.Length - 1, j = 0; i >= 0; i--, j++)
            {
                int k = chs[i] - '0';
                if (j % 2 == 0)
                {
                    k *= 2;
                    k = k / 10 + k % 10;
                }

                luhnSum += k;
            }

            return luhnSum;
        }
    }
}
