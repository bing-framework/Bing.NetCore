using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Core;
using Bing.MockData.Utils;

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
            return BatchGenerate(1).First();
        }

        /// <summary>
        /// 批量生成银行卡号
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public override List<string> BatchGenerate(int maxLength)
        {
            var bankInfo = GetBankInfo();
            var cardRule = GetCardRule(bankInfo);
            List<string> list=new List<string>();
            for (int i = 0; i < maxLength; i++)
            {
                var cardNum = cardRule.CardPrefix + Builder.GenerateNumber(cardRule.Length - 1);
                cardNum += GetCheckCode(cardNum);
                list.Add(cardNum);
            }

            return list;
        }

        /// <summary>
        /// 获取银行信息
        /// </summary>
        /// <returns></returns>
        private BankInfo GetBankInfo()
        {
            var length = BankConfig.GetBankInfoList().Count;
            var bankInfo = BankConfig.GetBankInfoList()[Builder.GenerateInt(0,length)];
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
            var cardRule = bankInfo.AllRules[Builder.GenerateInt(0, length)];
            return cardRule;
        }

        /// <summary>
        /// 获取银行卡校验码
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <returns></returns>
        private string GetCheckCode(string cardNo)
        {
            int luhnSum = LuhnUtil.GetLuhnSum(cardNo.Trim().ToCharArray());
            char checkCode = luhnSum % 10 == 0 ? '0' : (char) (10 - luhnSum % 10 + '0');
            return checkCode.ToString();
        }
    }
}
