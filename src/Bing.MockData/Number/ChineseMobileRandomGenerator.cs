using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Core;

namespace Bing.MockData.Number
{
    /// <summary>
    /// 中国手机号码生成器
    /// </summary>
    public class ChineseMobileRandomGenerator:RandomGeneratorBase,IRandomGenerator
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static readonly ChineseMobileRandomGenerator Instance =new ChineseMobileRandomGenerator();

        /// <summary>
        /// 手机号码前缀
        /// </summary>
        private static readonly int[] MobilePrefix = new int[]
        {
            133, 153, 177, 180,
            181, 189, 134, 135, 136, 137, 138, 139, 150, 151, 152, 157, 158, 159,
            178, 182, 183, 184, 187, 188, 130, 131, 132, 155, 156, 176, 185, 186,
            145, 147, 170
        };

        /// <summary>
        /// 初始化一个<see cref="ChineseMobileRandomGenerator"/>类型的实例
        /// </summary>
        private ChineseMobileRandomGenerator() { }

        /// <summary>
        /// 生成中国手机号码
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return GetMobilePre() + string.Format("{0}", GetRandom().Next(0, 99999999 + 1)).PadLeft(8, '0');
        }

        /// <summary>
        /// 获取手机号码前缀
        /// </summary>
        /// <returns></returns>
        private string GetMobilePre()
        {
            return MobilePrefix[GetRandom().Next(0, MobilePrefix.Length)].ToString();
        }

        /// <summary>
        /// 生成假的手机号码，以19开头
        /// </summary>
        /// <returns></returns>
        public string GenerateFake()
        {
            return "19" + string.Format("{0}", GetRandom().Next(0, 999999999 + 1)).PadLeft(9, '0');
        }
    }
}
