using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Microsoft
{
    /// <summary>
    /// Microsoft 授权请求
    /// </summary>
    public class MicrosoftAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }
    }
}
