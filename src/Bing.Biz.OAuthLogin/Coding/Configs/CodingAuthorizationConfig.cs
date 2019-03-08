using System.ComponentModel.DataAnnotations;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Coding.Configs
{
    /// <summary>
    /// Coding.NET 授权配置
    /// </summary>
    public class CodingAuthorizationConfig: AuthorizationConfigBase
    {
        /// <summary>
        /// 应用名称
        /// </summary>
        [Required(ErrorMessage = "应用名称[ApplicationName]不能为空")]
        public string ApplicationName { get; set; }

        /// <summary>
        /// 初始化一个<see cref="CodingAuthorizationConfig"/>类型的实例
        /// </summary>
        public CodingAuthorizationConfig()
        {
            AuthorizationUrl = "https://coding.net/oauth_authorize.html";
            AccessTokenUrl = "https://coding.net/api/oauth/access_token";
        }
    }
}
