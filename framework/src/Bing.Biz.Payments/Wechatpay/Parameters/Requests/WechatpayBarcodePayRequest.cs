using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Wechatpay.Parameters.Requests
{
    /// <summary>
    /// 微信条码支付参数
    /// </summary>
    public class WechatpayBarcodePayRequest : PayParamBase
    {
        /// <summary>
        /// 用户付款授权码
        /// </summary>
        public string AuthCode { get; set; }
    }
}
