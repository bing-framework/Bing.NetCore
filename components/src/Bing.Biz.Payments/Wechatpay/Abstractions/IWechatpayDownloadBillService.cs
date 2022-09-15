using System.Threading.Tasks;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;

namespace Bing.Biz.Payments.Wechatpay.Abstractions
{
    /// <summary>
    /// 微信支付下载交易账单服务
    /// </summary>
    public interface IWechatpayDownloadBillService
    {
        /// <summary>
        /// 下载对账单
        /// </summary>
        /// <param name="request">下载对账单参数</param>
        Task<WechatpayDownloadBillResult> DownloadAsync(WechatpayDownloadBillRequest request);
    }
}
