using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Github.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// Github测试授权配置提供程序
    /// </summary>
    public class TestGithubAuthorizationConfigProvider : IGithubAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<GithubAuthorizationConfig> GetConfigAsync()
        {
            var config = new GithubAuthorizationConfig()
            {
                AppId = TestSampleConfig.GithubAppId,
                AppKey = TestSampleConfig.GithubAppKey,
                CallbackUrl = TestSampleConfig.GithubCallbackUrl,
                ApplicationName = TestSampleConfig.GithubApplicationName
            };
            return Task.FromResult(config);
        }
    }
}
