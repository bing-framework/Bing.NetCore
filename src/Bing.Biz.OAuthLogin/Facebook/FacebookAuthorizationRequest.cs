using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Facebook
{
    /// <summary>
    /// Facebook 授权请求
    /// </summary>
    public class FacebookAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }
    }
}
