using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Bing.MockData.Generators.Banks
{
    /// <summary>
    /// 银行卡类型
    /// </summary>
    public enum CardType
    {
        /// <summary>
        /// 无法识别
        /// </summary>
        [Description("无法识别")]
        Unknown = 0,
        /// <summary>
        /// 借记卡（储蓄卡）
        /// </summary>
        [Description("借记卡")]
        DebitCard = 1,
        /// <summary>
        /// 信用卡
        /// </summary>
        [Description("信用卡")]
        CreditCard = 2,
        /// <summary>
        /// 准贷记卡
        /// </summary>
        [Description("准贷记卡")]
        QuasiCreditCard = 3,
        /// <summary>
        /// 预付费卡
        /// </summary>
        [Description("预付费卡")]
        PrepaidCard = 4

    }
}
