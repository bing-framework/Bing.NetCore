using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Baidu.Configs
{
    /// <summary>
    /// 百度授权配置
    /// </summary>
    public class BaiduAuthorizationConfig : AuthorizationConfigBase
    {
        /// <summary>
        /// 初始化一个<see cref="BaiduAuthorizationConfig"/>类型的实例
        /// </summary>
        public BaiduAuthorizationConfig()
        {
            AuthorizationUrl = "https://openapi.baidu.com/oauth/2.0/authorize";
            AccessTokenUrl = "https://openapi.baidu.com/oauth/2.0/token";
        }
    }
}
