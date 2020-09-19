using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs;

namespace Bing.Biz.Payments.Wechatpay.Services.Base
{
    /// <summary>
    /// 微信支付服务
    /// </summary>
    public abstract class WechatpayServiceBase : IPayService
    {
        /// <summary>
        /// 配置提供器
        /// </summary>
        protected readonly IWechatpayConfigProvider ConfigProvider;

        /// <summary>
        /// 是否发送请求
        /// </summary>
        public bool IsSend { get; set; } = true;

        /// <summary>
        /// 初始化一个<see cref="WechatpayServiceBase"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供器</param>
        protected WechatpayServiceBase(IWechatpayConfigProvider configProvider)
        {
            configProvider.CheckNotNull(nameof(configProvider));
            ConfigProvider = configProvider;
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="param">支付参数</param>
        /// <returns></returns>
        public virtual async Task<PayResult> PayAsync(PayParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new WechatpayParameterBuilder(config);
            Config(builder, param);
            return await RequestResult(config, builder);
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="param">支付参数</param>
        protected void Validate(WechatpayConfig config, PayParam param)
        {
            config.CheckNotNull(nameof(config));
            param.CheckNotNull(nameof(param));
            config.Validate();
            param.Validate();
            ValidateParam(param);
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">支付参数</param>
        protected virtual void ValidateParam(PayParam param) { }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="param">支付参数</param>
        protected void Config(WechatpayParameterBuilder builder, PayParam param)
        {
            builder.Init(param);
            builder.TradeType(GetTradeType());
            InitBuilder(builder, param);
        }

        /// <summary>
        /// 获取交易类型
        /// </summary>
        /// <returns></returns>
        protected abstract string GetTradeType();

        /// <summary>
        /// 初始化参数生成器
        /// </summary>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="param">支付参数</param>
        protected virtual void InitBuilder(WechatpayParameterBuilder builder, PayParam param) { }

        /// <summary>
        /// 请求结果
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生成器</param>
        /// <returns></returns>
        protected virtual async Task<PayResult> RequestResult(WechatpayConfig config, WechatpayParameterBuilder builder)
        {
            var result = new WechatpayResult(ConfigProvider, await Request(config, builder));
            WriteLog(config, builder, result);
            return await CreateResult(config, builder, result);
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生辰器</param>
        /// <returns></returns>
        protected virtual async Task<string> Request(WechatpayConfig config, WechatpayParameterBuilder builder)
        {
            if (IsSend == false)
            {
                return string.Empty;
            }

            return await Web.Client()
                .Post(string.IsNullOrWhiteSpace(GetOrderUrl()) ? config.GetOrderUrl() : GetOrderUrl())
                .XmlData(builder.ToXml())
                .ResultAsync();
        }

        /// <summary>
        /// 获取统一下单地址
        /// </summary>
        /// <returns></returns>
        protected virtual string GetOrderUrl()
        {
            return string.Empty;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="result">微信支付结果</param>
        protected void WriteLog(WechatpayConfig config, WechatpayParameterBuilder builder, WechatpayResult result)
        {
            var log = GetLog();
            if (log.IsTraceEnabled == false)
            {
                return;
            }
            log.Class(GetType().FullName)
                .Caption("微信支付")
                .Content($"支付方式 : {GetPayWay().Description()}")
                .Content($"支付网关 : {config.GetOrderUrl()}")
                .Content("请求参数:")
                .Content(builder.ToXml())
                .Content()
                .Content("返回结果:")
                .Content(result.GetParams())
                .Content()
                .Content("原始响应: ")
                .Content(result.Raw)
                .Trace();
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(WechatpayConst.TraceLogName);
            }
            catch
            {
                return Log.Null;
            }
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected abstract PayWay GetPayWay();

        /// <summary>
        /// 创建支付结果
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="result">微信支付结果</param>
        /// <returns></returns>
        protected virtual async Task<PayResult> CreateResult(WechatpayConfig config, WechatpayParameterBuilder builder,
            WechatpayResult result)
        {
            var success = (await result.ValidateAsync()).IsValid;
            return new PayResult(success, result.GetPrepayId(), result.Raw)
            {
                Parameter = builder.ToString(),
                Message = result.GetReturnMessage(),
                Result = success ? GetResult(config, builder, result) : null
            };
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="config">微信支付配置</param>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="result">微信支付结果</param>
        /// <returns></returns>
        protected virtual string GetResult(WechatpayConfig config, WechatpayParameterBuilder builder,
            WechatpayResult result)
        {
            return result.GetPrepayId();
        }
    }
}
