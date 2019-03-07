using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Gitee.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// Gitee测试授权配置提供程序
    /// </summary>
    public class TestGiteeAuthorizationConfigProvider:IGiteeAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<GiteeAuthorizationConfig> GetConfigAsync()
        {
            var config = new GiteeAuthorizationConfig()
            {
                AppId = TestSampleConfig.GiteeAppId,
                AppKey = TestSampleConfig.GiteeAppKey,
                CallbackUrl = TestSampleConfig.GiteeCallbackUrl,
                ApplicationName = TestSampleConfig.GiteeApplicationName
            };
            return Task.FromResult(config);
        }
    }
}
