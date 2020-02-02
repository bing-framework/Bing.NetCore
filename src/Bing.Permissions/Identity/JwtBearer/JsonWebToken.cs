using System;
using Bing.Helpers;
using Bing.Utils.Helpers;
using Bing.Utils.Timing;

namespace Bing.Permissions.Identity.JwtBearer
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
        /// 访问令牌有效期。UTC标准
        /// </summary>
        public long AccessTokenUtcExpires { get; set; }

        /// <summary>
        /// 刷新令牌。用于刷新AccessToken的令牌
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// 刷新令牌有效期。UTC标准
        /// </summary>
        public long RefreshUtcExpires { get; set; }

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired() => Conv.To<long>(DateTime.UtcNow.ToJsGetTime()) > AccessTokenUtcExpires;
    }
}
