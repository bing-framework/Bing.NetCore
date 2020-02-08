using System.ComponentModel.DataAnnotations;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Gitee.Configs
{
    /// <summary>
    /// Gitee 授权配置
    /// </summary>
    public class GiteeAuthorizationConfig : AuthorizationConfigBase
    {
        /// <summary>
        /// 应用名称
        /// </summary>
        [Required(ErrorMessage = "应用名称[ApplicationName]不能为空")]
        public string ApplicationName { get; set; }

        /// <summary>
        /// 初始化一个<see cref="GiteeAuthorizationConfig"/>类型的实例
        /// </summary>
        public GiteeAuthorizationConfig()
        {
            AuthorizationUrl = "https://gitee.com/oauth/authorize";
            AccessTokenUrl = "https://gitee.com/oauth/token";
        }
    }
}
