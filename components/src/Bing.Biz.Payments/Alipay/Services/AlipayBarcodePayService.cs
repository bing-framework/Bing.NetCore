using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Biz.Payments.Alipay.Services.Base;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Properties;
using Bing.Exceptions;
using Bing.Extensions;

namespace Bing.Biz.Payments.Alipay.Services;

/// <summary>
/// 支付宝条码支付服务
/// </summary>
public class AlipayBarcodePayService : AlipayServiceBase, IAlipayBarcodePayService
{
    /// <summary>
    /// 初始化一个<see cref="AlipayBarcodePayService"/>类型的实例
    /// </summary>
    /// <param name="provider">支付宝配置提供器</param>
    public AlipayBarcodePayService(IAlipayConfigProvider provider) : base(provider)
    {
    }

    /// <summary>
    /// 支付
    /// </summary>
    /// <param name="request">支付参数</param>
    /// <returns></returns>
    public async Task<PayResult> PayAsync(AlipayBarcodePayRequest request)
    {
        return await PayAsync(request.ToParam());
    }

    /// <summary>
    /// 获取场景
    /// </summary>
    /// <returns></returns>
    protected override string GetScene()
    {
        return "bar_code";
    }

    /// <summary>
    /// 获取请求方法
    /// </summary>
    /// <returns></returns>
    protected override string GetMethod()
    {
        return "alipay.trade.pay";
    }

    /// <summary>
    /// 获取支付方式
    /// </summary>
    /// <returns></returns>
    protected override PayWay GetPayWay()
    {
        return PayWay.AlipayBarcodePay;
    }

    /// <summary>
    /// 验证参数
    /// </summary>
    /// <param name="param">支付参数</param>
    protected override void ValidateParam(PayParam param)
    {
        if (param.AuthCode.IsEmpty())
        {
            throw new Warning(PayResource.AuthCodeIsEmpty);
        }
    }

    /// <summary>
    /// 初始化内容生成器
    /// </summary>
    /// <param name="builder">内容参数生成器</param>
    /// <param name="param">支付参数</param>
    protected override void InitContentBuilder(AlipayContentBuilder builder, PayParam param)
    {
        builder.AuthCode(param.AuthCode);
    }
}