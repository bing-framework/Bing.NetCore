using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Microsoft.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// Microsoft 测试授权配置提供程序
    /// </summary>
    public class TestMicrosoftAuthorizationConfigProvider : IMicrosoftAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<MicrosoftAuthorizationConfig> GetConfigAsync()
        {
            var config = new MicrosoftAuthorizationConfig()
            {
                AppId = TestSampleConfig.MicrosoftAppId,
                AppKey = TestSampleConfig.MicrosoftAppKey,
                CallbackUrl = TestSampleConfig.MicrosoftCallbackUrl
            };
            return Task.FromResult(config);
        }
    }
}
