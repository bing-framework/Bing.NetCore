using System.ComponentModel.DataAnnotations;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Github.Configs
{
    /// <summary>
    /// Github授权配置
    /// </summary>
    public class GithubAuthorizationConfig:AuthorizationConfigBase
    {
        /// <summary>
        /// 应用名称
        /// </summary>
        [Required(ErrorMessage = "应用名称[ApplicationName]不能为空")]
        public string ApplicationName { get; set; }

        /// <summary>
        /// 初始化一个<see cref="GithubAuthorizationConfig"/>类型的实例
        /// </summary>
        public GithubAuthorizationConfig()
        {
            AuthorizationUrl = "https://github.com/login/oauth/authorize";
            AccessTokenUrl = "https://github.com/login/oauth/access_token";
        }
    }
}
