using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Wechatpay.Configs;

namespace Bing.Biz.Payments.Extensions;

/// <summary>
/// 支付配置
/// </summary>
public class PayOptions
{
    /// <summary>
    /// 支付宝配置
    /// </summary>
    public AlipayConfig AlipayOptions { get; set; } = new AlipayConfig();

    /// <summary>
    /// 微信支付配置
    /// </summary>
    public WechatpayConfig WechatpayOptions { get; set; } = new WechatpayConfig();
}