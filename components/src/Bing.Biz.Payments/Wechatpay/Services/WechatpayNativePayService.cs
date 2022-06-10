using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信扫码支付服务
    /// </summary>
    public class WechatpayNativePayService : WechatpayPayServiceBase, IWechatpayNativePayService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayNativePayService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        public WechatpayNativePayService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">支付参数</param>
        public async Task<PayResult> PayAsync(WechatpayNativePayRequest request) => await PayAsync(request.ToParam());

        /// <summary>
        /// 获取交易类型
        /// </summary>
        protected override string GetTradeType() => "NATIVE";

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="result">微信支付结果</param>
        protected override string GetResult(WechatpayResult result)
        {
            return result.GetCodeUrl();
        }
    }
}
