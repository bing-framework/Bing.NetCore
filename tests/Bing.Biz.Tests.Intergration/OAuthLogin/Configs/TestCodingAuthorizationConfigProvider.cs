using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Coding.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// Coding.NET测试授权配置提供程序
    /// </summary>
    public class TestCodingAuthorizationConfigProvider : ICodingAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<CodingAuthorizationConfig> GetConfigAsync()
        {
            var config = new CodingAuthorizationConfig()
            {
                AppId = TestSampleConfig.CodingAppId,
                AppKey = TestSampleConfig.CodingAppKey,
                CallbackUrl = TestSampleConfig.CodingCallbackUrl,
                ApplicationName = TestSampleConfig.CodingApplicationName
            };
            return Task.FromResult(config);
        }
    }
}