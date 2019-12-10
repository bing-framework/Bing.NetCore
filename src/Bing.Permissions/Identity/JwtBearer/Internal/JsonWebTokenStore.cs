using System;
using System.Threading.Tasks;
using Bing.Caching;

namespace Bing.Permissions.Identity.JwtBearer.Internal
{
    /// <summary>
    /// Jwt令牌存储器
    /// </summary>
    internal sealed class JsonWebTokenStore : IJsonWebTokenStore
    {
        /// <summary>
        /// 缓存
        /// </summary>
        private readonly ICache _cache;

        /// <summary>
        /// 初始化一个<see cref="JsonWebTokenStore"/>类型的实例
        /// </summary>
        /// <param name="cache">缓存</param>
        public JsonWebTokenStore(ICache cache) => _cache = cache;

        /// <summary>
        /// 获取刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        public async Task<RefreshToken> GetRefreshTokenAsync(string token) =>
            await _cache.GetAsync<RefreshToken>(GetRefreshTokenKey(token));

        /// <summary>
        /// 保存刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        public async Task SaveRefreshTokenAsync(RefreshToken token) => await _cache.AddAsync(GetRefreshTokenKey(token.Value), token, token.EndUtcTime.Subtract(DateTime.UtcNow));

        /// <summary>
        /// 移除刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        public async Task RemoveRefreshTokenAsync(string token)
        {
            if (!await _cache.ExistsAsync(GetRefreshTokenKey(token)))
                return;
            await _cache.RemoveAsync(GetRefreshTokenKey(token));
            if(!await _cache.ExistsAsync(GetBindRefreshTokenKey(token)))
                return;
            var accessToken = await _cache.GetAsync<JsonWebToken>(GetBindRefreshTokenKey(token));
            await _cache.RemoveAsync(GetBindRefreshTokenKey(token));
            await RemoveTokenAsync(accessToken.AccessToken);
        }

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        public async Task<JsonWebToken> GetTokenAsync(string token) => await _cache.GetAsync<JsonWebToken>(GetTokenKey(token));

        /// <summary>
        /// 移除访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        public async Task RemoveTokenAsync(string token)
        {
            if(!await _cache.ExistsAsync(GetTokenKey(token)))
                return;
            await _cache.RemoveAsync(GetTokenKey(token));
        }

        /// <summary>
        /// 保存访问令牌
        /// </summary>
        /// <param name="token">令牌</param>
        /// <param name="expires">过期时间</param>
        public async Task SaveTokenAsync(JsonWebToken token, DateTime expires)
        {
            await _cache.AddAsync(GetTokenKey(token.AccessToken), token, expires.Subtract(DateTime.UtcNow));
            await _cache.AddAsync(GetBindRefreshTokenKey(token.RefreshToken), token, expires.Subtract(DateTime.UtcNow));
        }

        /// <summary>
        /// 是否存在访问令牌
        /// </summary>
        /// <param name="token">访问令牌</param>
        public async Task<bool> ExistsTokenAsync(string token) => await _cache.ExistsAsync(GetTokenKey(token));

        /// <summary>
        /// 绑定用户设备令牌
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型</param>
        /// <param name="info">设备信息</param>
        /// <param name="expires">过期时间</param>
        public async Task BindUserDeviceTokenAsync(string userId, string clientType, DeviceTokenBindInfo info,
            DateTime expires) => await _cache.AddAsync(GetBindUserDeviceTokenKey(userId, clientType), info,
            expires.Subtract(DateTime.UtcNow));

        /// <summary>
        /// 获取用户设备令牌
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型</param>
        public async Task<DeviceTokenBindInfo> GetUserDeviceTokenAsync(string userId, string clientType) =>
            await _cache.GetAsync<DeviceTokenBindInfo>(GetBindUserDeviceTokenKey(userId, clientType));

        /// <summary>
        /// 获取刷新令牌缓存键
        /// </summary>
        /// <param name="token">刷新令牌</param>
        private static string GetRefreshTokenKey(string token) => $"jwt:token:refresh:{token}";

        /// <summary>
        /// 获取访问令牌缓存键
        /// </summary>
        /// <param name="token">访问令牌</param>
        private static string GetTokenKey(string token) => $"jwt:token:access:{token}";

        /// <summary>
        /// 获取绑定刷新令牌缓存键
        /// </summary>
        /// <param name="token">刷新令牌</param>
        private static string GetBindRefreshTokenKey(string token) => $"jwt:token:bind:{token}";

        /// <summary>
        /// 获取绑定用户设备令牌缓存键
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型</param>
        private static string GetBindUserDeviceTokenKey(string userId, string clientType) =>
            $"jwt:token:bind_user:{userId}:{clientType}";
    }
}
