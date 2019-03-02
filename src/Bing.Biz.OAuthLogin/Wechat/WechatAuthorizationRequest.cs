using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信授权请求
    /// </summary>
    public class WechatAuthorizationRequest:AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; } = "snsapi_login";
    }
}
