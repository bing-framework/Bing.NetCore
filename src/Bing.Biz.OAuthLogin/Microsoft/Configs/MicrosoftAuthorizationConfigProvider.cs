using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Microsoft.Configs
{
    /// <summary>
    /// Microsoft 授权配置提供程序
    /// </summary>
    public class MicrosoftAuthorizationConfigProvider: AuthorizationConfigProviderBase<MicrosoftAuthorizationConfig>,IMicrosoftAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="MicrosoftAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">Microsoft 授权配置</param>
        public MicrosoftAuthorizationConfigProvider(MicrosoftAuthorizationConfig config) : base(config)
        {
        }
    }
}
