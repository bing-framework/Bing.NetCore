using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.DingTalk
{
    /// <summary>
    /// 钉钉授权请求
    /// </summary>
    public class DingTalkAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; } = "snsapi_auth";
    }
}
