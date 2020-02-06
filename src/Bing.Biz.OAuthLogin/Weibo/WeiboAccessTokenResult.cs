using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博访问令牌结果
    /// </summary>
    public class WeiboAccessTokenResult : AccessTokenResult
    {
        /// <summary>
        /// 授权用户的UID
        /// </summary>
        [JsonProperty("uid")]
        public string UnionId { get; set; }
    }
}
