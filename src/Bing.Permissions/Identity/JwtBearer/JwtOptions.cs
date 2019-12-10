namespace Bing.Permissions.Identity.JwtBearer
{
    /// <summary>
    /// Jwt选项配置
    /// </summary>
    public class JwtOptions
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

        /// <summary>
        /// 启用抛异常方式
        /// </summary>
        public bool ThrowEnabled { get; set; }

        /// <summary>
        /// 启用单设备登录
        /// </summary>
        public bool SingleDeviceEnabled { get; set; }

    }
}
