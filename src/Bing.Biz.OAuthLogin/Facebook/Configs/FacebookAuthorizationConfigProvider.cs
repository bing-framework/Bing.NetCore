using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Facebook.Configs
{
    /// <summary>
    /// Facebook 授权配置提供程序
    /// </summary>
    public class FacebookAuthorizationConfigProvider: AuthorizationConfigProviderBase<FacebookAuthorizationConfig>,IFacebookAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="FacebookAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">Facebook 授权配置</param>
        public FacebookAuthorizationConfigProvider(FacebookAuthorizationConfig config) : base(config)
        {
        }
    }
}
