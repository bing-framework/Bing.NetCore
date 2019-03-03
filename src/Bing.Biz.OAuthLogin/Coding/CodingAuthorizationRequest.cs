using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Coding
{
    /// <summary>
    /// Coding.NET 授权请求
    /// </summary>
    public class CodingAuthorizationRequest: AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }
    }
}
