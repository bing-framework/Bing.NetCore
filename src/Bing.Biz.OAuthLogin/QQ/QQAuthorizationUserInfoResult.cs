using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权用户信息结果
    /// </summary>
    public class QQAuthorizationUserInfoResult: AuthorizationUserInfoResult
    {
        /// <summary>
        /// 返回状态码
        /// </summary>
        [JsonProperty("ret")]
        public string Code { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [JsonProperty("msg")]
        public string Message { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        [JsonProperty("gender")]
        public string Gender { get; set; }

        /// <summary>
        /// 是否黄钻VIP。0:不是,1:是
        /// </summary>
        [JsonProperty("is_yellow_vip")]
        public string IsYellowVip { get; set; }

        /// <summary>
        /// 是否年费黄钻VIP。0:不是,1:是
        /// </summary>
        [JsonProperty("is_yellow_year_vip")]
        public string IsYellowYearVip { get; set; }

        /// <summary>
        /// 黄钻等级
        /// </summary>
        [JsonProperty("yellow_vip_level")]
        public string YellowVipLevel { get; set; }

        /// <summary>
        /// QQ头像。40 x 40像素
        /// </summary>
        [JsonProperty("figureurl_qq_1")]
        // ReSharper disable once InconsistentNaming
        public string QQAvatarx40 { get; set; }

        /// <summary>
        /// QQ头像。100 x 100像素
        /// </summary>
        [JsonProperty("figureurl_qq_2")]
        // ReSharper disable once InconsistentNaming
        public string QQAvatarx100 { get; set; }

        /// <summary>
        /// QQ空间头像。30 x 30像素
        /// </summary>
        [JsonProperty("figureurl")]
        // ReSharper disable once InconsistentNaming
        public string QQZoneAvatarx30 { get; set; }

        /// <summary>
        /// QQ空间头像。50 x 50像素
        /// </summary>
        [JsonProperty("figureurl_1")]
        // ReSharper disable once InconsistentNaming
        public string QQZoneAvatarx50 { get; set; }

        /// <summary>
        /// QQ空间头像。100 x 100像素
        /// </summary>
        [JsonProperty("figureurl_2")]
        // ReSharper disable once InconsistentNaming
        public string QQZoneAvatarx100 { get; set; }
    }
}
