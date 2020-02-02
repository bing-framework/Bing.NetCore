using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Exceptions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信小程序支付服务
    /// </summary>
    public class WechatpayMiniProgramPayService:WechatpayServiceBase,IWechatpayMiniProgramPayService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayMiniProgramPayService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        public WechatpayMiniProgramPayService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 获取交易类型
        /// </summary>
        /// <returns></returns>
        protected override string GetTradeType()
        {
            return "JSAPI";
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.WechatpayMiniProgramPay;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">支付参数</param>
        protected override void ValidateParam(PayParam param)
        {
            if (param.OpenId.IsEmpty())
            {
                throw new Warning("小程序支付必须设置OpenId");
            }
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">微信小程序支付参数</param>
        /// <returns></returns>
        public async Task<PayResult> PayAsync(WechatpayMiniProgramPayRequest request)
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
                .Add("appId", config.AppId)
                .Add("timeStamp", Time.GetUnixTimestamp().SafeString())
                .Add("nonceStr", Id.Guid())
                .Package($"prepay_id={result.GetPrepayId()}")
                .Add("signType", config.SignType.Description())
                .ToJson();
        }
    }
}
