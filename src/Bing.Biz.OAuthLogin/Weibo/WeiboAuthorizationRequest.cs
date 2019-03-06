using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博授权请求
    /// </summary>
    public class WeiboAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// 授权页面的终端类型
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// 是否强制用户重新登录
        /// </summary>
        public bool Forcelogin { get; set; }

        /// <summary>
        /// 授权页语言
        /// </summary>
        public string Language { get; set; }
    }
}
