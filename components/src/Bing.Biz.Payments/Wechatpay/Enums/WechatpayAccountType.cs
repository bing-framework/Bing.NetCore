using System.ComponentModel;

namespace Bing.Biz.Payments.Wechatpay.Enums;

/// <summary>
/// 微信支付资金账户类型
/// </summary>
public enum WechatpayAccountType
{
    /// <summary>
    /// 基本账户
    /// </summary>
    [Description("Basic")]
    Basic,
        
    /// <summary>
    /// 运营账户
    /// </summary>
    [Description("Operation")]
    Operation,
        
    /// <summary>
    /// 手续费账户
    /// </summary>
    [Description("Fees")]
    Fees,
}