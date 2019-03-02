using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.QQ.Results
{
    /// <summary>
    /// QQ用户信息结果
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class QQUserInfoResult
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
        /// QQ头像
        /// </summary>
        [JsonProperty("figureurl_qq_1")]
        // ReSharper disable once InconsistentNaming
        public string QQAvatar { get; set; }

        /// <summary>
        /// QQ空间头像
        /// </summary>
        [JsonProperty("figureurl_1")]
        // ReSharper disable once InconsistentNaming
        public string QQZoneAvatar { get; set; }
    }
}
