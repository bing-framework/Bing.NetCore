using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.MeiliShuo.Configs
{
    /// <summary>
    /// 美丽说授权配置提供程序
    /// </summary>
    public class MeiliShuoAuthorizationConfigProvider : AuthorizationConfigProviderBase<MeiliShuoAuthorizationConfig>, IMeiliShuoAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="MeiliShuoAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">美丽说授权配置</param>
        public MeiliShuoAuthorizationConfigProvider(MeiliShuoAuthorizationConfig config) : base(config)
        {
        }
    }
}
