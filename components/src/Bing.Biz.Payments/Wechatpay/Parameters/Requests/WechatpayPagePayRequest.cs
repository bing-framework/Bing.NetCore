using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Wechatpay.Parameters.Requests
{
    /// <summary>
    /// 微信电脑网站支付参数
    /// </summary>
    public class WechatpayPagePayRequest : PayParamBase
    {
        /// <summary>
        /// 附加数据，通知时原样返回
        /// </summary>
        public string Attach { get; set; }
    }
}
