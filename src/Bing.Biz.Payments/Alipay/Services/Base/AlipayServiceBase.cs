using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Core;
using Bing.Utils.Extensions;

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
            throw new System.NotImplementedException();
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

    }
}
