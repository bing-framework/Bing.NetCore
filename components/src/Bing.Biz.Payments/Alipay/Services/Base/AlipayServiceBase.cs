using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Results;
using Bing.Biz.Payments.Core;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs;

namespace Bing.Biz.Payments.Alipay.Services.Base
{
    /// <summary>
    /// 支付宝支付服务基类
    /// </summary>
    public abstract class AlipayServiceBase : IPayService
    {
        /// <summary>
        /// 配置提供器
        /// </summary>
        protected readonly IAlipayConfigProvider ConfigProvider;

        /// <summary>
        /// 是否发送请求
        /// </summary>
        public bool IsSend { get; set; } = true;

        /// <summary>
        /// 初始化一个<see cref="AlipayServiceBase"/>类型的实例
        /// </summary>
        /// <param name="provider">支付宝配置提供器</param>
        protected AlipayServiceBase(IAlipayConfigProvider provider)
        {
            provider.CheckNotNull(nameof(provider));
            ConfigProvider = provider;
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="param">支付参数</param>
        public virtual async Task<PayResult> PayAsync(PayParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AlipayParameterBuilder(config);
            Config(builder, param);
            return await RequestResult(config, builder);
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="param">支付参数</param>
        protected void Validate(AlipayConfig config, PayParam param)
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
        /// <param name="builder">支付宝参数生成器</param>
        /// <param name="param">支付参数</param>
        protected void Config(AlipayParameterBuilder builder, PayParam param)
        {
            builder.Init(param);
            builder.Method(GetMethod());
            builder.Content.Scene(GetScene());
            InitContentBuilder(builder.Content, param);
        }

        /// <summary>
        /// 获取请求方法
        /// </summary>
        protected abstract string GetMethod();

        /// <summary>
        /// 获取场景
        /// </summary>
        protected virtual string GetScene() => string.Empty;

        /// <summary>
        /// 初始化内容生成器
        /// </summary>
        /// <param name="builder">内容参数生成器</param>
        /// <param name="param">支付参数</param>
        protected virtual void InitContentBuilder(AlipayContentBuilder builder, PayParam param)
        {
        }

        /// <summary>
        /// 请求结果
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        protected virtual async Task<PayResult> RequestResult(AlipayConfig config, AlipayParameterBuilder builder)
        {
            var result = new AlipayResult(await Request(config, builder));
            WriteLog(config, builder, result);
            return CreateResult(builder, result);
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        protected virtual async Task<string> Request(AlipayConfig config, AlipayParameterBuilder builder)
        {
            if (IsSend == false)
                return string.Empty;
            return await Web.Client()
                .Post(config.GetGatewayUrl())
                .Data(builder.GetDictionary())
                .ResultAsync();
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        /// <param name="result">支付宝结果</param>
        protected void WriteLog(AlipayConfig config, AlipayParameterBuilder builder, AlipayResult result)
        {
            var log = GetLog();
            if (log.IsTraceEnabled == false)
                return;
            log.Class(GetType().FullName)
                .Caption("支付宝支付")
                .Content($"支付宝方式 : {GetPayWay().Description()}")
                .Content($"支付网关 : {config.GetGatewayUrl()}")
                .Content("请求参数:")
                .Content(builder.GetDictionary())
                .Content()
                .Content("返回结果:")
                .Content(result.GetDictionary())
                .Content()
                .Content("原始请求:")
                .Content(builder.ToString())
                .Content()
                .Content("原始响应:")
                .Content(result.Raw)
                .Trace();
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        /// <param name="content">内容</param>
        protected void WriteLog(AlipayConfig config, AlipayParameterBuilder builder, string content)
        {
            var log = GetLog();
            if (log.IsTraceEnabled == false)
                return;
            log.Class(GetType().FullName)
                .Content($"支付方式 : {GetPayWay().Description()}")
                .Content($"支付网关 : {config.GetGatewayUrl()}")
                .Content("请求参数:")
                .Content(builder.GetDictionary())
                .Content()
                .Content("原始请求:")
                .Content(builder.ToString())
                .Content()
                .Content("内容:")
                .Content(content)
                .Trace();
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        private ILog GetLog() => Log.GetLog(AlipayConst.TraceLogName);

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected abstract PayWay GetPayWay();

        /// <summary>
        /// 创建结果
        /// </summary>
        /// <param name="builder">支付宝参数生成器</param>
        /// <param name="result">支付宝结果</param>
        protected virtual PayResult CreateResult(AlipayParameterBuilder builder, AlipayResult result) =>
            new PayResult(result.Success, result.GetTradeNo(), result.Raw)
            {
                Parameter = builder.ToString(),
                Message = result.GetMessage()
            };
    }
}
