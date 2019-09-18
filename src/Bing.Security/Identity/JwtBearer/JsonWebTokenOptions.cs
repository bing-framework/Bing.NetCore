namespace Bing.Security.Identity.JwtBearer
{
    /// <summary>
    /// Jwt选项配置
    /// </summary>
    public class JsonWebTokenOptions
    {
        /// <summary>
        /// 密钥。密钥加密算法：HmacSha256
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// 发行方
        /// </summary>
        public string Issuer { get; set; } = "bing_identity";

        /// <summary>
        /// 订阅方
        /// </summary>
        public string Audience { get; set; } = "bing_client";

        /// <summary>
        /// 访问令牌有效期分钟数
        /// </summary>
        public double AccessExpireMinutes { get; set; }

        /// <summary>
        /// 刷新令牌有效期分钟数
        /// </summary>
        public double RefreshExpireMinutes { get; set; }
    }
}
