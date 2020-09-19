using System.ComponentModel;

namespace Bing.Biz.Payments.Core
{
    /// <summary>
    /// 支付方式
    /// </summary>
    public enum PayWay
    {
        /// <summary>
        /// 支付宝条码支付
        /// </summary>
        [Description("支付宝条码支付")]
        AlipayBarcodePay,

        /// <summary>
        /// 支付宝二维码支付
        /// </summary>
        [Description("支付宝二维码支付")]
        AlipayQrCodePay,

        /// <summary>
        /// 支付宝电脑网站支付
        /// </summary>
        [Description("支付宝电脑网站支付")]
        AlipayPagePay,

        /// <summary>
        /// 支付宝手机网站支付
        /// </summary>
        [Description("支付宝手机网站支付")]
        AlipayWapPay,

        /// <summary>
        /// 支付宝App支付
        /// </summary>
        [Description("支付宝App支付")]
        AlipayAppPay,

        /// <summary>
        /// 微信App支付
        /// </summary>
        [Description("微信App支付")]
        WechatpayAppPay,

        /// <summary>
        /// 微信小程序支付
        /// </summary>
        [Description("微信小程序支付")]
        WechatpayMiniProgramPay,

        /// <summary>
        /// 微信电脑网站支付
        /// </summary>
        [Description("微信电脑网站支付")]
        WechatpayPagePay,

        /// <summary>
        /// 微信手机网站支付
        /// </summary>
        [Description("微信手机网站支付")]
        WechatpayWapPay,

        /// <summary>
        /// 微信公众号支付
        /// </summary>
        [Description("微信公众号支付")]
        WechatpayPublicPay,

        /// <summary>
        /// 微信条码支付
        /// </summary>
        [Description("微信条码支付")]
        WechatpayBarcodePay,
    }
}
