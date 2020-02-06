using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信App支付服务
    /// </summary>
    public class WechatpayAppPayService : WechatpayServiceBase, IWechatpayAppPayService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayAppPayService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        public WechatpayAppPayService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 获取交易类型
        /// </summary>
        /// <returns></returns>
        protected override string GetTradeType()
        {
            return "APP";
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.WechatpayAppPay;
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">支付参数</param>
        /// <returns></returns>
        public async Task<PayResult> PayAsync(WechatpayAppPayRequest request)
        {
            return await PayAsync(request.ToParam());
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="result">微信支付结果</param>
        /// <returns></returns>
        protected override string GetResult(WechatpayConfig config, WechatpayParameterBuilder builder, WechatpayResult result)
        {
            return new WechatpayParameterBuilder(config)
                .AppId(config.AppId)
                .PartnerId(config.MerchantId)
                .Add("prepayid", result.GetPrepayId())
                .Add("noncestr", Id.Guid())
                .Timestamp()
                .Package()
                .ToJson();
        }
    }
}
