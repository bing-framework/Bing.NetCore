using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;

namespace Bing.Biz.Payments
{
    /// <summary>
    /// 支付工厂
    /// </summary>
    public interface IPayFactory
    {
        /// <summary>
        /// 创建支付服务
        /// </summary>
        /// <param name="way">支付方式</param>
        /// <returns></returns>
        IPayService CreatePayService(PayWay way);

        /// <summary>
        /// 创建支付宝回调通知服务
        /// </summary>
        /// <returns></returns>
        IAlipayNotifyService CreateAlipayNotifyService();

        /// <summary>
        /// 创建支付宝返回服务
        /// </summary>
        /// <returns></returns>
        IAlipayReturnService CreateAlipayReturnService();

        /// <summary>
        /// 创建支付宝条码支付服务
        /// </summary>
        /// <returns></returns>
        IAlipayBarcodePayService CreateAlipayBarcodePayService();

        /// <summary>
        /// 创建支付宝二维码支付服务
        /// </summary>
        /// <returns></returns>
        IAlipayQrCodePayService CreateAlipayQrCodePayService();

        /// <summary>
        /// 创建支付宝App支付服务
        /// </summary>
        /// <returns></returns>
        IAlipayAppPayService CreateAlipayAppPayService();

        /// <summary>
        /// 创建支付宝电脑网站支付服务
        /// </summary>
        /// <returns></returns>
        IAlipayPagePayService CreateAlipayPagePayService();

        /// <summary>
        /// 创建支付宝手机网站支付服务
        /// </summary>
        /// <returns></returns>
        IAlipayWapPayService CreateAlipayWapPayService();

        /// <summary>
        /// 创建微信回调通知服务
        /// </summary>
        /// <returns></returns>
        IWechatpayNotifyService CreateWechatpayNotifyService();

        /// <summary>
        /// 创建微信App支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayAppPayService CreateWechatpayAppPayService();

        /// <summary>
        /// 创建微信小程序支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayMiniProgramPayService CreateWechatpayMiniProgramPayService();

        /// <summary>
        /// 创建微信电脑网站支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayPagePayService CreateWechatpayPagePayService();

        /// <summary>
        /// 创建微信手机网站支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayWapPayService CreateWechatpayWapPayService();

        /// <summary>
        /// 创建微信公众号支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayPublicPayService CreateWechatpayPublicPayService();

        /// <summary>
        /// 创建微信条码支付服务
        /// </summary>
        /// <returns></returns>
        IWechatpayBarcodePayService CreateWechatpayBarcodePayService();
    }
}
