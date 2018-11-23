using System;
using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Results;
using Bing.Biz.Payments.Core;
using Bing.Logs;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Biz.Payments.Alipay.Services.Base
{
    /// <summary>
    /// 支付宝支付服务基类
    /// </summary>
    public abstract class AlipayServiceBase:IPayService
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
        /// <returns></returns>
        public virtual async Task<PayResult> PayAsync(PayParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AlipayParameterBuilder(config);
            Config(builder, param);
            return null;
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
        /// <returns></returns>
        protected abstract string GetMethod();

        /// <summary>
        /// 获取场景
        /// </summary>
        /// <returns></returns>
        protected virtual string GetScene()
        {
            return string.Empty;
        }

        /// <summary>
        /// 初始化内容生成器
        /// </summary>
        /// <param name="builder">内容参数生成器</param>
        /// <param name="param">支付参数</param>
        protected virtual void InitContentBuilder(AlipayContentBuilder builder,PayParam param) { }

        //protected virtual async Task<PayResult> RequestResult(AlipayConfig config, AlipayParameterBuilder builder)
        //{
            
        //}

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        /// <returns></returns>
        protected virtual async Task<string> Request(AlipayConfig config, AlipayParameterBuilder builder)
        {
            if (IsSend == false)
            {
                return string.Empty;
            }

            return await Web.Client()
                .Post(config.GetGatewayUrl())
                .Data(builder.GetDictionary())
                .ResultAsync();
        }

        protected void WriteLog(AlipayConfig config, AlipayParameterBuilder builder, AlipayResult result)
        {
            
        }

        protected void WriteLog(AlipayConfig config, AlipayParameterBuilder builder, string content)
        {

        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(AlipayConst.TraceLogName);
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

        //protected virtual PayResult CreateResult(AlipayParameterBuilder builder, AlipayResult result)
        //{
        //    return new PayResult(result.Raw);
        //}
    }
}
