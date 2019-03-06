using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博授权用户请求
    /// </summary>
    public class WeiboAuthorizationUserRequest: AuthorizationUserParamBase
    {
        /// <summary>
        /// 授权用户的UID
        /// </summary>
        public string UnionId { get; set; }

        /// <summary>
        /// 用户昵称
        /// </summary>
        public string ScreenName { get; set; }
    }
}
