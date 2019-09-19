using System;

namespace Bing.Security.Identity.JwtBearer
{
    /// <summary>
    /// JwtToken
    /// </summary>
    [Serializable]
    public class JsonWebToken
    {
        /// <summary>
        /// 访问令牌。用于业务身份认证的令牌
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// 刷新令牌。用于刷新AccessToken的令牌
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// 刷新令牌有效期。UTC标准
        /// </summary>
        public long RefreshUtcExpires { get; set; }
    }
}
