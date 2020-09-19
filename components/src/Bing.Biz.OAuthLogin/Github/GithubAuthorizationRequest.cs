using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Github
{
    /// <summary>
    /// Github授权请求
    /// </summary>
    public class GithubAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// 是否为未经身份验证的用户提供在OAuth流程期间注册Github的选项
        /// </summary>
        public bool AllowSignup { get; set; } = true;
    }
}
