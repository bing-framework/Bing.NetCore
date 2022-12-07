using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Parameters.Requests;

namespace Bing.Biz.Payments.Alipay.Abstractions;

/// <summary>
/// 支付宝二维码支付服务
/// </summary>
public interface IAlipayQrCodePayService
{
    /// <summary>
    /// 支付
    /// </summary>
    /// <param name="request">支付参数</param>
    /// <returns></returns>
    Task<string> PayAsync(AlipayPrecreateRequest request);
}