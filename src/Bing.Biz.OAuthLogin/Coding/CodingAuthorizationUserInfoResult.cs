using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Coding
{
    /// <summary>
    /// Coding.NET 授权用户信息结果
    /// </summary>
    public class CodingAuthorizationUserInfoResult : AuthorizationUserInfoResult
    {
        /// <summary>
        /// 标签
        /// </summary>
        [JsonProperty("tags")]
        public string Tags { get; set; }

        /// <summary>
        /// 标签显示文本
        /// </summary>
        [JsonProperty("tags_str")]
        public string TagsStr { get; set; }

        /// <summary>
        /// 工作
        /// </summary>
        [JsonProperty("job")]
        public int Job { get; set; }

        /// <summary>
        /// 工作显示文本
        /// </summary>
        [JsonProperty("job_str")]
        public string JobStr { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        [JsonProperty("sex")]
        public int Sex { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        [JsonProperty("phone")]
        public string Phone { get; set; }

        /// <summary>
        /// 生日
        /// </summary>
        [JsonProperty("birthday")]
        public string Birthday { get; set; }

        /// <summary>
        /// 位置
        /// </summary>
        [JsonProperty("location")]
        public string Location { get; set; }

        /// <summary>
        /// 公司
        /// </summary>
        [JsonProperty("company")]
        public string Company { get; set; }

        /// <summary>
        /// 口号
        /// </summary>
        [JsonProperty("slogan")]
        public string Slogan { get; set; }

        /// <summary>
        /// 站点
        /// </summary>
        [JsonProperty("website")]
        public string Website { get; set; }

        /// <summary>
        /// 介绍
        /// </summary>
        [JsonProperty("introduction")]
        public string Introduction { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        /// <summary>
        /// 大头像
        /// </summary>
        [JsonProperty("gravatar")]
        public string Gravatar { get; set; }

        /// <summary>
        /// 小头像
        /// </summary>
        [JsonProperty("lavatar")]
        public string Lavatar { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty("created_at")]
        public long CreatedAt { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        [JsonProperty("last_logined_at")]
        public long LastLoginedAt { get; set; }

        /// <summary>
        /// 最后活跃时间
        /// </summary>
        [JsonProperty("last_activity_at")]
        public long LastActivityAt { get; set; }

        /// <summary>
        /// 全局键
        /// </summary>
        [JsonProperty("global_key")]
        public string GlobalKey { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// 名称拼音
        /// </summary>
        [JsonProperty("name_pinyin")]
        public string NamePinyin { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonProperty("updated_at")]
        public long UpdatedAt { get; set; }

        /// <summary>
        /// 路径
        /// </summary>
        [JsonProperty("path")]
        public string Path { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        [JsonProperty("status")]
        public int Status { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>
        /// 是否会员
        /// </summary>
        [JsonProperty("is_member")]
        public int IsMember { get; set; }

        /// <summary>
        /// 系统编号
        /// </summary>
        [JsonProperty("id")]
        public long Id { get; set; }

        /// <summary>
        /// 积分剩余
        /// </summary>
        [JsonProperty("points_left")]
        public decimal PointsLeft { get; set; }

        /// <summary>
        /// VIP等级
        /// </summary>
        [JsonProperty("vip")]
        public int Vip { get; set; }

        /// <summary>
        /// VIP有效期
        /// </summary>
        [JsonProperty("vip_expired_at")]
        public long VipExpiredAt { get; set; }

        /// <summary>
        /// 学历
        /// </summary>
        [JsonProperty("degree")]
        public int Degree { get; set; }

        /// <summary>
        /// 学校
        /// </summary>
        [JsonProperty("school")]
        public string School { get; set; }

        /// <summary>
        /// 关注数量
        /// </summary>
        [JsonProperty("follows_count")]
        public int FollowsCount { get; set; }

        /// <summary>
        /// 粉丝数
        /// </summary>
        [JsonProperty("fans_count")]
        public int FansCount { get; set; }

        /// <summary>
        /// 推广数
        /// </summary>
        [JsonProperty("tweets_count")]
        public int TweetsCount { get; set; }

        /// <summary>
        /// 国家手机编码
        /// </summary>
        [JsonProperty("phone_country_code")]
        public string PhoneCountryCode { get; set; }

        /// <summary>
        /// 国家
        /// </summary>
        [JsonProperty("country")]
        public string Country { get; set; }

        /// <summary>
        /// 是否已关注
        /// </summary>
        [JsonProperty("followed")]
        public bool Followed { get; set; }

        /// <summary>
        /// 是否关注
        /// </summary>
        [JsonProperty("follow")]
        public bool Follow { get; set; }

        /// <summary>
        /// 是否已验证手机
        /// </summary>
        [JsonProperty("is_phone_validated")]
        public bool IsPhoneValidated { get; set; }

        /// <summary>
        /// 邮箱验证
        /// </summary>
        [JsonProperty("email_validation")]
        public int EmailValidation { get; set; }

        /// <summary>
        /// 手机验证
        /// </summary>
        [JsonProperty("phone_validation")]
        public int PhoneValidation { get; set; }

        /// <summary>
        /// 启用二次验证
        /// </summary>
        [JsonProperty("twofa_enabled")]
        public int TwofaEnabled { get; set; }
    }
}
