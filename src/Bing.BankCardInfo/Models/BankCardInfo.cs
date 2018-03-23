using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.BankCardInfo.Models
{
    /// <summary>
    /// 银行卡信息
    /// </summary>
    public class BankCardInfo
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool Validated { get; set; }

        /// <summary>
        /// 银行代码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 银行名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 银行Logo
        /// </summary>
        public string Logo { get; set; }

        /// <summary>
        /// 银行卡类型
        /// </summary>
        public string CardType { get; set; }

        /// <summary>
        /// 银行卡类型名称
        /// </summary>
        public string CarTypeName { get; set; }
    }
}
