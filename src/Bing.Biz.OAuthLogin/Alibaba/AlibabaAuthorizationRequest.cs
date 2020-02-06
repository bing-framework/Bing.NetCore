using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Alibaba
{
    /// <summary>
    /// 阿里巴巴授权请求
    /// </summary>
    public class AlibabaAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 授权站点
        /// </summary>
        public string Site { get; set; }
    }
}
