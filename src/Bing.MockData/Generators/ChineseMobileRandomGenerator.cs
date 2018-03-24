using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Core;

namespace Bing.MockData.Generators
{
    /// <summary>
    /// 手机号码生成器
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
        /// 生成手机号码
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成手机号码
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            List<string> list=new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                list.Add(GetMobilePre() + string.Format("{0}", Builder.GenerateNumber(8)).PadLeft(8, '0'));
            }

            return list;
        }

        /// <summary>
        /// 获取手机号码前缀
        /// </summary>
        /// <returns></returns>
        private string GetMobilePre()
        {
            return MobilePrefix[Builder.GenerateInt(0, MobilePrefix.Length)].ToString();
        }

        /// <summary>
        /// 生成假的手机号码，以19开头
        /// </summary>
        /// <returns></returns>
        public string GenerateFake()
        {
            return "19" + string.Format("{0}", Builder.GenerateNumber(9)).PadLeft(9, '0');
        }
    }
}
