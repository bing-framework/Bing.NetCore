using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信JsApi支付服务
    /// </summary>
    public class WechatpayJsApiPayService : WechatpayPayServiceBase, IWechatpayJsApiPayService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayJsApiPayService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        public WechatpayJsApiPayService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">支付参数</param>
        public async Task<PayResult> PayAsync(WechatpayJsApiPayRequest request) => await PayAsync(request.ToParam());

        /// <summary>
        /// 获取交易类型
        /// </summary>
        protected override string GetTradeType() => "JSAPI";

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">请求参数</param>
        protected override void ValidateParam(PayParam param)
        {
            if (param.OpenId.IsEmpty())
                throw new Warning("必须设置OpenId");
        }

        /// <summary>
        /// 初始化参数生成器
        /// </summary>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="param">支付参数</param>
        protected override void InitBuilder(WechatpayParameterBuilder builder, PayParam param)
        {
            builder.OpenId(param.OpenId);
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="result">微信支付结果</param>
        protected override string GetResult(WechatpayResult result)
        {
            return new WechatpayParameterBuilder(result.Config)
                .Add("appId", result.Config.AppId)
                .Add("timeStamp", Time.GetUnixTimestamp().SafeString())
                .Add("nonceStr", Id.Guid())
                .Package($"prepay_id{result.GetPrepayId()}")
                .Add("signType", result.Config.SignType.Description())
                .Add("paySign", result.GetSign())
                .ToJson();
        }
    }
}
