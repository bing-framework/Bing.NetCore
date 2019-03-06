using System.ComponentModel.DataAnnotations;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Weibo.Configs
{
    /// <summary>
    /// 微博授权配置
    /// </summary>
    public class WeiboAuthorizationConfig: AuthorizationConfigBase
    {
        /// <summary>
        /// 初始化一个<see cref="WeiboAuthorizationConfig"/>类型的实例
        /// </summary>
        public WeiboAuthorizationConfig()
        {
            AuthorizationUrl = "https://api.weibo.com/oauth2/authorize";
            AccessTokenUrl = "https://api.weibo.com/oauth2/access_token";
        }
    }
}
