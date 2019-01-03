using System;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Services;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Services;
using Bing.Utils.Extensions;

namespace Bing.Biz.Payments.Factories
{
    /// <summary>
    /// 支付工厂
    /// </summary>
    public class PayFactory:IPayFactory
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
        /// <returns></returns>
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
                case PayWay.WechatpayAppPay:
                    return new WechatpayAppPayService(_wechatpayConfigProvider);
                case PayWay.WechatpayMiniProgramPay:
                    return new WechatpayMiniProgramPayService(_wechatpayConfigProvider);
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
        /// 创建微信回调通知服务
        /// </summary>
        /// <returns></returns>
        public IWechatpayNotifyService CreateWechatpayNotifyService()
        {
            return new WechatpayNotifyService(_wechatpayConfigProvider);
        }

        /// <summary>
        /// 创建微信App支付服务
        /// </summary>
        /// <returns></returns>
        public IWechatpayAppPayService CreateWechatpayAppPayService()
        {
            return new WechatpayAppPayService(_wechatpayConfigProvider);
        }

        /// <summary>
        /// 创建微信小程序支付服务
        /// </summary>
        /// <returns></returns>
        public IWechatpayMiniProgramPayService CreateWechatpayMiniProgramPayService()
        {
            return new WechatpayMiniProgramPayService(_wechatpayConfigProvider);
        }
    }
}
