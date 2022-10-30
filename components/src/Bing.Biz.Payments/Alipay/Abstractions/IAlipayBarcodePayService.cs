using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Alipay.Abstractions;

/// <summary>
/// 支付宝条码支付服务
/// </summary>
public interface IAlipayBarcodePayService
{
    /// <summary>
    /// 支付
    /// </summary>
    /// <param name="request">条码支付参数</param>
    /// <returns></returns>
    Task<PayResult> PayAsync(AlipayBarcodePayRequest request);
}