using System.Threading.Tasks;

namespace Bing.Biz.Payments.Alipay.Configs
{
    /// <summary>
    /// 支付宝配置提供器
    /// </summary>
    public class AlipayConfigProvider : IAlipayConfigProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly AlipayConfig _config;

        /// <summary>
        /// 初始化一个<see cref="AlipayConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">支付宝配置</param>
        public AlipayConfigProvider(AlipayConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<AlipayConfig> GetConfigAsync()
        {
            return Task.FromResult(_config);
        }
    }
}
