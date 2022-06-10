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
        /// 微信付款码支付
        /// </summary>
        /// <remarks>
        /// 用户打开微信钱包-付款码的界面，商户扫码后提交完成支付
        /// </remarks>
        [Description("微信付款码支付")]
        WechatpayPaymentCodePay,

        /// <summary>
        /// 微信JsApi支付
        /// </summary>
        /// <remarks>
        /// 用户通过微信扫码，关注公众号等方式进入商家H5页面，并在微信内调用JsSdk完成支付
        /// </remarks>
        [Description("微信JsApi支付")]
        WechatpayJsApiPay,

        /// <summary>
        /// 微信Native支付
        /// </summary>
        /// <remarks>
        /// 用户打开“微信扫一扫”，扫描商户的二维码后完成付款
        /// </remarks>
        [Description("微信Native支付")]
        WechatpayNativePay,

        /// <summary>
        /// 微信App支付
        /// </summary>
        /// <remarks>
        /// 商户APP中集成微信SDK，用户点击后跳转到微信内完成支付
        /// </remarks>
        [Description("微信App支付")]
        WechatpayAppPay,

        /// <summary>
        /// 微信H5支付
        /// </summary>
        /// <remarks>
        /// 用户在微信以外的手机浏览器请求微信支付的场景唤醒微信支付
        /// </remarks>
        [Description("微信H5支付")]
        WechatpayH5Pay,

        /// <summary>
        /// 微信小程序支付
        /// </summary>
        /// <remarks>
        /// 用户在微信小程序中使用微信支付的场景
        /// </remarks>
        [Description("微信小程序支付")]
        WechatpayMiniProgramPay,
    }
}
