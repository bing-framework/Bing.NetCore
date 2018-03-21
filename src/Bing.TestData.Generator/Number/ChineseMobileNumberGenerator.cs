using System;
using System.Collections.Generic;
using System.Text;
using Bing.TestData.Generator.Core;

namespace Bing.TestData.Generator.Number
{
    /// <summary>
    /// 中国手机号码生成器
    /// </summary>
    public class ChineseMobileNumberGenerator:GeneratorBase
    {
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
        /// 实例
        /// </summary>
        private static readonly ChineseMobileNumberGenerator Instance=new ChineseMobileNumberGenerator();

        /// <summary>
        /// 初始化一个<see cref="ChineseMobileNumberGenerator"/>类型的实例
        /// </summary>
        private ChineseMobileNumberGenerator() { }

        /// <summary>
        /// 获取生成器实例
        /// </summary>
        /// <returns></returns>
        public static ChineseMobileNumberGenerator GetInstance()
        {
            return Instance;
        }

        /// <summary>
        /// 生成模拟手机号码
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return GetMobilePre()+ string.Format("{0}", GetRandomInstance().Next(0, 99999999 + 1)).PadLeft(8, '0');
        }

        /// <summary>
        /// 获取手机号码前缀
        /// </summary>
        /// <returns></returns>
        private string GetMobilePre()
        {
            return MobilePrefix[GetRandomInstance().Next(0, MobilePrefix.Length)].ToString();
        }

        /// <summary>
        /// 生成假的手机号码，以19开头
        /// </summary>
        /// <returns></returns>
        public string GenerateFake()
        {
            return "19" + string.Format("{0}",GetRandomInstance().Next(0, 999999999 + 1)).PadLeft(9, '0');
        }        
    }
}
