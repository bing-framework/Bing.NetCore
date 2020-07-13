using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Microsoft.Configs
{
    /// <summary>
    /// Microsoft 授权配置
    /// </summary>
    public class MicrosoftAuthorizationConfig : AuthorizationConfigBase
    {
        /// <summary>
        /// 初始化一个<see cref="MicrosoftAuthorizationConfig"/>类型的实例
        /// </summary>
        public MicrosoftAuthorizationConfig()
        {
            AuthorizationUrl = "https://login.live.com/oauth20_authorize.srf";
            AccessTokenUrl = "https://login.live.com/oauth20_token.srf";
        }
    }
}
