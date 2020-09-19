using System.Threading.Tasks;
using Bing.Utils.Parameters;

namespace Bing.Biz.Payments.Wechatpay.Configs
{
    /// <summary>
    /// 微信支付配置提供器
    /// </summary>
    public class WechatpayConfigProvider : IWechatpayConfigProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly WechatpayConfig _config;

        /// <summary>
        /// 初始化一个<see cref="WechatpayConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">微信支付配置</param>
        public WechatpayConfigProvider(WechatpayConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <param name="parameterManager">参数管理器</param>
        /// <returns></returns>
        public Task<WechatpayConfig> GetConfigAsync(IParameterManager parameterManager = null)
        {
            return Task.FromResult(_config);
        }
    }
}
