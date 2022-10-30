using System;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Services;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Services;
using Bing.Extensions;

namespace Bing.Biz.Payments.Factories;

/// <summary>
/// 支付工厂
/// </summary>
public class PayFactory : IPayFactory
{
    /// <summary>
    /// 支付宝配置提供器
    /// </summary>
    private readonly IAlipayConfigProvider _alipayConfigProvider;

    /// <summary>
    /// 微信支付配置提供器
    /// </summary>
    private readonly IWechatpayConfigProvider _wechatpayConfigProvider;

    /// <summary>
    /// 初始化一个<see cref="PayFactory"/>类型的实例
    /// </summary>
    /// <param name="alipayConfigProvider">支付宝配置提供器</param>
    /// <param name="wechatpayConfigProvider">微信支付配置提供器</param>
    public PayFactory(IAlipayConfigProvider alipayConfigProvider, IWechatpayConfigProvider wechatpayConfigProvider)
    {
        _alipayConfigProvider = alipayConfigProvider;
        _wechatpayConfigProvider = wechatpayConfigProvider;
    }

    /// <summary>
    /// 创建支付服务
    /// </summary>
    /// <param name="way">支付方式</param>
    public IPayService CreatePayService(PayWay way)
    {
        switch (way)
        {
            case PayWay.AlipayBarcodePay:
                return new AlipayBarcodePayService(_alipayConfigProvider);

            case PayWay.AlipayQrCodePay:
                return new AlipayQrCodePayService(_alipayConfigProvider);

            case PayWay.AlipayAppPay:
                return new AlipayAppPayService(_alipayConfigProvider);

            case PayWay.AlipayPagePay:
                return new AlipayPagePayService(_alipayConfigProvider);

            case PayWay.AlipayWapPay:
                return new AlipayWapPayService(_alipayConfigProvider);

            case PayWay.WechatpayPaymentCodePay:
                return new WechatpayPaymentCodePayService(_wechatpayConfigProvider);

            case PayWay.WechatpayJsApiPay:
                return new WechatpayJsApiPayService(_wechatpayConfigProvider);

            case PayWay.WechatpayNativePay:
                return new WechatpayNativePayService(_wechatpayConfigProvider);

            case PayWay.WechatpayAppPay:
                return new WechatpayAppPayService(_wechatpayConfigProvider);

            case PayWay.WechatpayMiniProgramPay:
                return new WechatpayMiniProgramPayService(_wechatpayConfigProvider);

            case PayWay.WechatpayH5Pay:
                return new WechatpayH5PayService(_wechatpayConfigProvider);
        }

        throw new NotImplementedException(way.Description());
    }

    /// <summary>
    /// 创建支付宝回调通知服务
    /// </summary>
    /// <returns></returns>
    public IAlipayNotifyService CreateAlipayNotifyService()
    {
        return new AlipayNotifyService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝返回服务
    /// </summary>
    /// <returns></returns>
    public IAlipayReturnService CreateAlipayReturnService()
    {
        return new AlipayReturnService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝条码支付服务
    /// </summary>
    /// <returns></returns>
    public IAlipayBarcodePayService CreateAlipayBarcodePayService()
    {
        return new AlipayBarcodePayService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝二维码支付服务
    /// </summary>
    /// <returns></returns>
    public IAlipayQrCodePayService CreateAlipayQrCodePayService()
    {
        return new AlipayQrCodePayService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝App支付服务
    /// </summary>
    /// <returns></returns>
    public IAlipayAppPayService CreateAlipayAppPayService()
    {
        return new AlipayAppPayService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝电脑网站支付服务
    /// </summary>
    /// <returns></returns>
    public IAlipayPagePayService CreateAlipayPagePayService()
    {
        return new AlipayPagePayService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建支付宝手机网站支付服务
    /// </summary>
    /// <returns></returns>
    public IAlipayWapPayService CreateAlipayWapPayService()
    {
        return new AlipayWapPayService(_alipayConfigProvider);
    }

    /// <summary>
    /// 创建微信回调通知服务
    /// </summary>
    public IWechatpayNotifyService CreateWechatpayNotifyService() => new WechatpayNotifyService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信下载交易账单服务
    /// </summary>
    public IWechatpayDownloadBillService CreateWechatpayDownloadBillService() => new WechatpayDownloadBillService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信付款码支付服务
    /// </summary>
    public IWechatpayPaymentCodePayService CreateWechatpayBarcodePayService() => new WechatpayPaymentCodePayService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信JsApi支付服务
    /// </summary>
    public IWechatpayJsApiPayService CreateWechatpayJsApiPayService() => new WechatpayJsApiPayService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信App支付服务
    /// </summary>
    public IWechatpayAppPayService CreateWechatpayAppPayService() => new WechatpayAppPayService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信H5支付服务
    /// </summary>
    public IWechatpayH5PayService CreateH5PayService() => new WechatpayH5PayService(_wechatpayConfigProvider);

    /// <summary>
    /// 创建微信小程序支付服务
    /// </summary>
    public IWechatpayMiniProgramPayService CreateWechatpayMiniProgramPayService() => new WechatpayMiniProgramPayService(_wechatpayConfigProvider);
}