using System.Threading.Tasks;
using Bing.Biz.Payments.Wechatpay.Configs;

namespace Bing.Biz.Payments.Tests.Wechatpay.Configs
{
    /// <summary>
    /// 微信支付测试配置提供器
    /// </summary>
    public class TestConfigProvider : IWechatpayConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <param name="parameter">参数</param>
        public Task<WechatpayConfig> GetConfigAsync(object parameter = null)
        {
            var config = new WechatpayConfig { AppId = "", MerchantId = "", PrivateKey = "", NotifyUrl = "" };
            return Task.FromResult(config);
        }
    }
}
