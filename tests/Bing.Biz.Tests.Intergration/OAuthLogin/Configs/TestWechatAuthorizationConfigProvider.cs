using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Wechat.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// 微信测试授权配置提供程序
    /// </summary>
    public class TestWechatAuthorizationConfigProvider:IWechatAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<WechatAuthorizationConfig> GetConfigAsync()
        {
            var config = new WechatAuthorizationConfig()
            {
                AppId = TestSampleConfig.WechatAppId,
                AppKey = TestSampleConfig.WechatAppKey,
                CallbackUrl = TestSampleConfig.WechatCallbackUrl
            };
            return Task.FromResult(config);
        }
    }
}
