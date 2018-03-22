using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bing.MockData.Generators.Banks
{
    /// <summary>
    /// 银行信息
    /// </summary>
    public class BankInfo
    {
        /// <summary>
        /// 银行名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 银行简称
        /// </summary>
        public string AbbrName { get; set; }

        /// <summary>
        /// 银行编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 所有卡规则
        /// </summary>
        public List<CardRuleItem> AllRules { get; }=new List<CardRuleItem>();

        /// <summary>
        /// 初始化一个<see cref="BankInfo"/>类型的实例
        /// </summary>
        /// <param name="code">银行编码</param>
        /// <param name="name">银行名称</param>
        public BankInfo(string code, string name)
        {
            Code = code;
            Name = name;
        }

        /// <summary>
        /// 初始化一个<see cref="BankInfo"/>类型的实例
        /// </summary>
        /// <param name="code">银行编码</param>
        /// <param name="name">银行名称</param>
        /// <param name="abbrName">简称</param>
        public BankInfo(string code, string name, string abbrName) : this(code, name)
        {
            AbbrName = abbrName;
        }

        /// <summary>
        /// 添加卡规则
        /// </summary>
        /// <param name="type">银行卡类型</param>
        /// <param name="length">长度</param>
        /// <param name="prefix">银行卡前缀</param>
        /// <returns></returns>
        public BankInfo AddRule(CardType type, int length, string prefix)
        {
            if (AllRules.Exists(x => x.CardPrefix == prefix && x.Length == length))
            {
                return this;
            }

            AllRules.Add(new CardRuleItem() {CardPrefix = prefix, Length = length, Type = type});
            return this;
        }

        /// <summary>
        /// 添加卡规则
        /// </summary>
        /// <param name="type">银行卡类型</param>
        /// <param name="length">长度</param>
        /// <param name="prefixs">银行卡前缀</param>
        /// <returns></returns>
        public BankInfo AddRules(CardType type, int length, List<string> prefixs)
        {
            prefixs.ForEach(x => { AddRule(type, length, x); });
            return this;
        }

        /// <summary>
        /// 添加卡规则
        /// </summary>
        /// <param name="type">银行卡类型</param>
        /// <param name="length">长度</param>
        /// <param name="prefixs">银行卡前缀</param>
        /// <returns></returns>
        public BankInfo AddRules(CardType type, int length, string prefixs)
        {
            List<string> list = prefixs.Split('|').ToList();
            return AddRules(type, length, list);
        }
    }
}
