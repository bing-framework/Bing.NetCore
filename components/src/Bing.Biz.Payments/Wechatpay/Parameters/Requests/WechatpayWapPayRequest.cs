using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Wechatpay.Parameters.Requests
{
    /// <summary>
    /// 微信手机网站支付参数
    /// </summary>
    public class WechatpayWapPayRequest : PayParamBase
    {
        /// <summary>
        /// 附加数据，通知原样返回
        /// </summary>
        public string Attach { get; set; }
    }
}
