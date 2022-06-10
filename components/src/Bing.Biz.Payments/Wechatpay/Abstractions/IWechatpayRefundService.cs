using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;

namespace Bing.Biz.Payments.Wechatpay.Abstractions
{
    /// <summary>
    /// 微信退款服务
    /// </summary>
    public interface IWechatpayRefundService
    {
        /// <summary>
        /// 退款
        /// </summary>
        /// <param name="request">退款参数</param>
        Task<RefundResult> RefundAsync(WechatRefundRequest request);
    }
}
