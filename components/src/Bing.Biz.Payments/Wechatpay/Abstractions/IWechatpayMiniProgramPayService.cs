using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;

namespace Bing.Biz.Payments.Wechatpay.Abstractions;

/// <summary>
/// 微信小程序支付服务
/// </summary>
public interface IWechatpayMiniProgramPayService
{
    /// <summary>
    /// 支付
    /// </summary>
    /// <param name="request">支付参数</param>
    Task<PayResult> PayAsync(WechatpayMiniProgramPayRequest request);
}