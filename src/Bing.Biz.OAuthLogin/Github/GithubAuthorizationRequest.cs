using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Github
{
    /// <summary>
    /// Github授权请求
    /// </summary>
    public class GithubAuthorizationRequest: AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }
    }
}
