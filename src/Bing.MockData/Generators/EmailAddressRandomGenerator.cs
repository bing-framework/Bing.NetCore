using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.MockData.Core;

namespace Bing.MockData.Generators
{
    /// <summary>
    /// 邮箱地址生成器
    /// </summary>
    public class EmailAddressRandomGenerator:RandomGeneratorBase,IRandomGenerator
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static readonly EmailAddressRandomGenerator Instance = new EmailAddressRandomGenerator();

        /// <summary>
        /// 初始化一个<see cref="EmailAddressRandomGenerator"/>类型的实例
        /// </summary>
        private EmailAddressRandomGenerator() { }

        /// <summary>
        /// 生成邮箱地址
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成邮箱地址
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            List<string> list=new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                list.Add(GetEmailAddress());
            }
            return list;
        }

        /// <summary>
        /// 获取邮箱地址
        /// </summary>
        /// <returns></returns>
        private string GetEmailAddress()
        {
            StringBuilder sb=new StringBuilder();
            sb.Append(Builder.GenerateAlphanumeric(10));
            sb.Append("@");
            sb.Append(Builder.GenerateAlphanumeric(5));
            sb.Append(".");
            sb.Append(Builder.GenerateAlphanumeric(3));
            return sb.ToString();
        }
    }
}
