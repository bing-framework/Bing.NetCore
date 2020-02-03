using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Properties;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Extensions;
using Bing.Utils.Json;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信条码支付服务
    /// </summary>
    public class WechatpayBarcodePayService : WechatpayServiceBase, IWechatpayBarcodePayService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayBarcodePayService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        public WechatpayBarcodePayService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">支付参数</param>
        /// <returns></returns>
        public async Task<PayResult> PayAsync(WechatpayBarcodePayRequest request)
        {
            return await PayAsync(request.ToParam());
        }

        /// <summary>
        /// 获取交易类型
        /// </summary>
        /// <returns></returns>
        protected override string GetTradeType()
        {
            return string.Empty;
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.WechatpayBarcodePay;
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
        protected override void InitBuilder(WechatpayParameterBuilder builder, PayParam param)
        {
            builder.AuthCode(param.AuthCode);
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
            return result.GetParams().ToJson();
        }
    }
}
