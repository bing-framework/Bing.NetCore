using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Core;

namespace Bing.MockData.Generators.Banks
{
    /// <summary>
    /// 银行卡号生成器
    /// </summary>
    public class BankCardRandomGenerator:RandomGeneratorBase,IRandomGenerator
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static readonly BankCardRandomGenerator Instance = new BankCardRandomGenerator();
        
        /// <summary>
        /// 初始化一个<see cref="BankCardRandomGenerator"/>类型的实例
        /// </summary>
        private BankCardRandomGenerator() { }

        /// <summary>
        /// 生成银行卡号
        /// </summary>
        /// <returns></returns>
        public override string Generate()
        {
            var bankInfo = GetBankInfo();
            var cardRule = GetCardRule(bankInfo);
            var cardNum = cardRule.CardPrefix + GenerateNumber(cardRule.Length-1);
            cardNum += GetCheckCode(cardNum);
            return cardNum;
        }

        /// <summary>
        /// 获取银行信息
        /// </summary>
        /// <returns></returns>
        private BankInfo GetBankInfo()
        {
            var length = BankConfig.GetBankInfoList().Count;
            var bankInfo = BankConfig.GetBankInfoList()[GetRandom().Next(0, length)];
            return bankInfo;
        }

        /// <summary>
        /// 获取银行卡规则
        /// </summary>
        /// <param name="bankInfo">银行信息</param>
        /// <returns></returns>
        private CardRuleItem GetCardRule(BankInfo bankInfo)
        {
            var length = bankInfo.AllRules.Count;
            var cardRule = bankInfo.AllRules[GetRandom().Next(0, length)];
            return cardRule;
        }

        /// <summary>
        /// 获取银行卡校验码
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <returns></returns>
        private string GetCheckCode(string cardNo)
        {
            int luhnSum = 0;
            char[] chs = cardNo.Trim().ToCharArray();
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

            char checkCode = luhnSum % 10 == 0 ? '0' : (char) (10 - luhnSum % 10 + '0');
            return checkCode.ToString();
        }

        /// <summary>
        /// 生成随机数字
        /// </summary>
        /// <param name="maxLength">最大长度</param>
        /// <returns></returns>
        private string GenerateNumber(int maxLength)
        {
            StringBuilder sb=new StringBuilder();
            for (int i = 0; i < maxLength; i++)
            {
                int num = GetRandom().Next();
                string temp= ((char)(0x30 + (ushort)(num % 10))).ToString();
                sb.Append(temp);
            }
            return sb.ToString();
        }
    }
}
