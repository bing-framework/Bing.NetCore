using Bing.Biz.OAuthLogin.Core;
using Newtonsoft.Json;

namespace Bing.Biz.OAuthLogin.Gitee
{
    /// <summary>
    /// Gitee 授权用户信息结果
    /// </summary>
    public class GiteeAuthorizationUserInfoResult: AuthorizationUserInfoResult
    {
        /// <summary>
        /// 头像地址
        /// </summary>
        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 简介
        /// </summary>
        [JsonProperty("bio")]
        public string Bio { get; set; }

        /// <summary>
        /// 博客
        /// </summary>
        [JsonProperty("blog")]
        public string Blog { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty("create_at")]
        public string CreateAt { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>
        /// 事件地址
        /// </summary>
        [JsonProperty("events_url")]
        public string EventsUrl { get; set; }

        /// <summary>
        /// 关注者数量
        /// </summary>
        [JsonProperty("followers")]
        public string Followers { get; set; }

        /// <summary>
        /// 关注者地址
        /// </summary>
        [JsonProperty("followers_url")]
        public string FollowersUrl { get; set; }

        /// <summary>
        /// 关注中
        /// </summary>
        [JsonProperty("following")]
        public string Following { get; set; }

        /// <summary>
        /// 关注中地址
        /// </summary>
        [JsonProperty("following_url")]
        public string FollowingUrl { get; set; }

        /// <summary>
        /// Gists地址
        /// </summary>
        [JsonProperty("gists_url")]
        public string GistsUrl { get; set; }

        /// <summary>
        /// Html地址
        /// </summary>
        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        /// <summary>
        /// 系统编号
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// 登录账号
        /// </summary>
        [JsonProperty("login")]
        public string Login { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// 组织地址
        /// </summary>
        [JsonProperty("organizations_url")]
        public string OrganizationsUrl { get; set; }

        /// <summary>
        /// 拥有私仓数量
        /// </summary>
        [JsonProperty("owned_private_repos")]
        public string OwnedPrivateRepos { get; set; }

        /// <summary>
        /// 拥有仓库数量
        /// </summary>
        [JsonProperty("owned_repos")]
        public string OwnedRepos { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        [JsonProperty("phone")]
        public string Phone { get; set; }

        /// <summary>
        /// 私有Gists数量
        /// </summary>
        [JsonProperty("private_gists")]
        public string PrivateGists { get; set; }

        /// <summary>
        /// 私有令牌
        /// </summary>
        [JsonProperty("private_token")]
        public string PrivateToken { get; set; }

        /// <summary>
        /// 公共Gists数量
        /// </summary>
        [JsonProperty("public_gists")]
        public string PublicGists { get; set; }

        /// <summary>
        /// 公共仓库数量
        /// </summary>
        [JsonProperty("public_repos")]
        public string PublicRepos { get; set; }

        /// <summary>
        /// 接收事件地址
        /// </summary>
        [JsonProperty("received_events_url")]
        public string ReceivedEventsUrl { get; set; }

        /// <summary>
        /// 仓储地址
        /// </summary>
        [JsonProperty("repos_url")]
        public string ReposUrl { get; set; }

        /// <summary>
        /// 站点管理员
        /// </summary>
        [JsonProperty("site_admin")]
        public string SiteAdmin { get; set; }

        /// <summary>
        /// star数量
        /// </summary>
        [JsonProperty("stared")]
        public string Stared { get; set; }

        /// <summary>
        /// start地址
        /// </summary>
        [JsonProperty("starred_url")]
        public string StarredUrl { get; set; }

        /// <summary>
        /// 订阅地址
        /// </summary>
        [JsonProperty("subscriptions_url")]
        public string SubscriptionsUrl { get; set; }

        /// <summary>
        /// 总私仓数
        /// </summary>
        [JsonProperty("total_private_repos")]
        public string TotalPrivateRepos { get; set; }

        /// <summary>
        /// 总仓数量
        /// </summary>
        [JsonProperty("total_repos")]
        public string TotalRepos { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        /// <summary>
        /// Url地址
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// 监察数量
        /// </summary>
        [JsonProperty("watched")]
        public string Watched { get; set; }

        /// <summary>
        /// 微博
        /// </summary>
        [JsonProperty("weibo")]
        public string Weibo { get; set; }
    }
}
