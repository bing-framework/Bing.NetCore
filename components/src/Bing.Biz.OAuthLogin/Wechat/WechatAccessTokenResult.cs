using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信访问令牌结果
    /// </summary>
    public class WechatAccessTokenResult : AccessTokenResult
    {
        /// <summary>
        /// 授权用户唯一标识
        /// </summary>
        [JsonProperty("openid")]
        public string OpenId { get; set; }

        /// <summary>
        /// 用户授权的作用域，使用逗号(,)分隔
        /// </summary>
        [JsonProperty("scope")]
        public string Scope { get; set; }

        /// <summary>
        /// 唯一标识。当且仅当该网站应用已获得该用户的userinfo授权时，才会出现该字段。
        /// </summary>
        [JsonProperty("unionid")]
        public string UnionId { get; set; }
    }
}
