using System.Collections.Generic;
using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信授权用户信息结果
    /// </summary>
    public class WechatAuthorizationUserInfoResult: AuthorizationUserInfoResult
    {
        /// <summary>
        /// 用户OpenId。对当前开发者账号唯一
        /// </summary>
        [JsonProperty("openid")]
        public string OpenId { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        /// <summary>
        /// 性别。1:男性,2:女性
        /// </summary>
        [JsonProperty("sex")]
        public string Sex { get; set; }

        /// <summary>
        /// 省份
        /// </summary>
        [JsonProperty("province")]
        public string Province { get; set; }

        /// <summary>
        /// 城市
        /// </summary>
        [JsonProperty("city")]
        public string City { get; set; }

        /// <summary>
        /// 国家
        /// </summary>
        [JsonProperty("country")]
        public string Country { get; set; }

        /// <summary>
        /// 头像。最后一个数值代表正方形头像大小（有0、46、64、96、132数值可选，0代表640*640正方形头像），用户没有头像时该项为空
        /// </summary>
        [JsonProperty("headimgurl")]
        public string Avatar { get; set; }

        /// <summary>
        /// 用户特权信息。Json数组
        /// </summary>
        [JsonProperty("privilege")]
        public List<string> Privilege { get; set; }

        /// <summary>
        /// 用户唯一标识。针对一个微信开放平台帐号下的应用，同一用户的unionid是唯一的
        /// </summary>
        [JsonProperty("unionid")]
        public string UnionId { get; set; }
    }
}
