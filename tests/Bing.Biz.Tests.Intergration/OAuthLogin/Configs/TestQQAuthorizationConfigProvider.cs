using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.QQ.Configs;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Configs
{
    /// <summary>
    /// QQ测试授权配置提供提供程序
    /// </summary>
    public class TestQQAuthorizationConfigProvider: IQQAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<QQAuthorizationConfig> GetConfigAsync()
        {
            var config=new QQAuthorizationConfig()
            {
                AppId = TestSampleConfig.QQAppId,
                AppKey = TestSampleConfig.QQAppKey,
                CallbackUrl = TestSampleConfig.QQCallbackUrl
            };
            return Task.FromResult(config);
        }
    }
}
