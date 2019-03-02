using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Weibo.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// 微博测试授权配置提供程序
    /// </summary>
    public class TestWeiboAuthorizationConfigProvider:IWeiboAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<WeiboAuthorizationConfig> GetConfigAsync()
        {
            var config = new WeiboAuthorizationConfig()
            {
                AppId = TestSampleConfig.WeiboAppId,
                AppKey = TestSampleConfig.WeiboAppKey,
                CallbackUrl = TestSampleConfig.WeiboCallbackUrl
            };
            return Task.FromResult(config);
        }
    }
}
