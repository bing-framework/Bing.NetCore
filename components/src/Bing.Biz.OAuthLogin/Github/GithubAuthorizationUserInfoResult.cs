using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Github
{
    /// <summary>
    /// Github 授权用户信息结果
    /// </summary>
    public class GithubAuthorizationUserInfoResult : AuthorizationUserInfoResult
    {
        /// <summary>
        /// 登录账号
        /// </summary>
        [JsonProperty("login")]
        public string Login { get; set; }

        /// <summary>
        /// 系统编号
        /// </summary>
        [JsonProperty("id")]
        public long Id { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 头像ID
        /// </summary>
        [JsonProperty("gravatar_id")]
        public string GravatarId { get; set; }

        /// <summary>
        /// Url
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// Html地址
        /// </summary>
        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        /// <summary>
        /// 关注者地址
        /// </summary>
        [JsonProperty("followers_url")]
        public string FollowersUrl { get; set; }

        /// <summary>
        /// 关注中地址
        /// </summary>
        [JsonProperty("following_url")]
        public string FollowingUrl { get; set; }

        /// <summary>
        /// 概要地址
        /// </summary>
        [JsonProperty("gists_url")]
        public string GistsUrl { get; set; }

        /// <summary>
        /// Star地址
        /// </summary>
        [JsonProperty("starred_url")]
        public string StarredUrl { get; set; }

        /// <summary>
        /// 订阅地址
        /// </summary>
        [JsonProperty("subscriptions_url")]
        public string SubscriptionsUrl { get; set; }

        /// <summary>
        /// 组织地址
        /// </summary>
        [JsonProperty("organizations_url")]
        public string OrganizationsUrl { get; set; }

        /// <summary>
        /// 仓储地址
        /// </summary>
        [JsonProperty("repos_url")]
        public string ReposUrl { get; set; }

        /// <summary>
        /// 事件地址
        /// </summary>
        [JsonProperty("events_url")]
        public string EventsUrl { get; set; }

        /// <summary>
        /// 接收事件地址
        /// </summary>
        [JsonProperty("received_events_url")]
        public string ReceivedEventsUrl { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// 是否站点管理员
        /// </summary>
        [JsonProperty("site_admin")]
        public bool SiteAdmin { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// 公司
        /// </summary>
        [JsonProperty("company")]
        public string Company { get; set; }

        /// <summary>
        /// 博客地址
        /// </summary>
        [JsonProperty("blog")]
        public string Blog { get; set; }

        /// <summary>
        /// 位置
        /// </summary>
        [JsonProperty("location")]
        public string Location { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>
        /// 是否雇佣
        /// </summary>
        [JsonProperty("hireable")]
        public bool Hireable { get; set; }

        /// <summary>
        /// 简介
        /// </summary>
        [JsonProperty("bio")]
        public string Bio { get; set; }

        /// <summary>
        /// 开放仓储
        /// </summary>
        [JsonProperty("public_repos")]
        public int PublicRepos { get; set; }

        /// <summary>
        /// 开放Gis
        /// </summary>
        [JsonProperty("public_gists")]
        public int PubulicGists { get; set; }

        /// <summary>
        /// 关注数
        /// </summary>
        [JsonProperty("followers")]
        public int Followers { get; set; }

        /// <summary>
        /// 关注中数量
        /// </summary>
        [JsonProperty("following")]
        public int Following { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        /// <summary>
        /// 私有Gis数量
        /// </summary>
        [JsonProperty("private_gists")]
        public int PrivateGists { get; set; }

        /// <summary>
        /// 私有仓数量
        /// </summary>
        [JsonProperty("total_private_repos")]
        public int TotalPrivateRepos { get; set; }

        /// <summary>
        /// 拥有私仓数量
        /// </summary>
        [JsonProperty("owned_private_repos")]
        public int OwnedPrivateRepos { get; set; }

        /// <summary>
        /// 磁盘使用量
        /// </summary>
        [JsonProperty("disk_usage")]
        public int DiskUsage { get; set; }

        /// <summary>
        /// 合作者
        /// </summary>
        [JsonProperty("collaborators")]
        public int Collaborators { get; set; }

        /// <summary>
        /// 是否启用二级认证
        /// </summary>
        [JsonProperty("two_factor_authentication")]
        public bool TwoFactorAuthentication { get; set; }
    }
}
