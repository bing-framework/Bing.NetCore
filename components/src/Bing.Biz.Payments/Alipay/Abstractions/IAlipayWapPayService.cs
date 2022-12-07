using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Parameters.Requests;

namespace Bing.Biz.Payments.Alipay.Abstractions;

/// <summary>
/// 支付宝手机网站支付服务
/// </summary>
public interface IAlipayWapPayService
{
    /// <summary>
    /// 支付，返回跳转地址
    /// </summary>
    /// <param name="request">支付宝手机网站支付参数</param>
    /// <returns></returns>
    Task<string> PayAsync(AlipayWapPayRequest request);

    /// <summary>
    /// 支付，跳转到支付宝收银台
    /// </summary>
    /// <param name="request">支付宝手机网站支付参数</param>
    /// <returns></returns>
    Task RedirectAsync(AlipayWapPayRequest request);
}