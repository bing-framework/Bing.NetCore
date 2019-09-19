using System;
using System.Threading.Tasks;

namespace Bing.Permissions.Identity.JwtBearer
{
    /// <summary>
    /// Jwt令牌存储器
    /// </summary>
    public interface IJsonWebTokenStore
    {
        /// <summary>
        /// 获取刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        Task<RefreshToken> GetRefreshTokenAsync(string token);

        /// <summary>
        /// 保存刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        Task SaveRefreshTokenAsync(RefreshToken token);

        /// <summary>
        /// 移除刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        Task RemoveRefreshTokenAsync(string token);

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        Task<JsonWebToken> GetTokenAsync(string token);

        /// <summary>
        /// 移除访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        Task RemoveTokenAsync(string token);

        /// <summary>
        /// 保存访问令牌
        /// </summary>
        /// <param name="token">令牌</param>
        /// <param name="expires">过期时间</param>
        Task SaveTokenAsync(JsonWebToken token, DateTime expires);

        /// <summary>
        /// 是否存在访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        Task<bool> ExistsTokenAsync(string token);
    }
}
