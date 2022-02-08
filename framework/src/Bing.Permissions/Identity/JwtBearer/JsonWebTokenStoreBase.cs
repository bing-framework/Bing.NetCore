using System;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Bing.Permissions.Identity.JwtBearer
{
    /// <summary>
    /// Jwt令牌存储器基类
    /// </summary>
    public abstract class JsonWebTokenStoreBase : IJsonWebTokenStore
    {
        /// <inheritdoc />
        public async Task<RefreshToken> GetRefreshTokenAsync(string token) => await GetAsync<RefreshToken>(GetRefreshTokenKey(token));

        /// <inheritdoc />
        public async Task SaveRefreshTokenAsync(RefreshToken token) => await AddAsync(GetRefreshTokenKey(token.Value), token, token.EndUtcTime.Subtract(DateTime.UtcNow));

        /// <inheritdoc />
        public async Task RemoveRefreshTokenAsync(string token)
        {
            if (!await ExistsAsync(GetRefreshTokenKey(token)))
                return;
            await RemoveAsync(GetRefreshTokenKey(token));
            if (!await ExistsAsync(GetBindRefreshTokenKey(token)))
                return;
            var accessToken = await GetAsync<JsonWebToken>(GetBindRefreshTokenKey(token));
            await RemoveAsync(GetBindRefreshTokenKey(token));
            await RemoveTokenAsync(accessToken.AccessToken);
        }

        /// <inheritdoc />
        public async Task<JsonWebToken> GetTokenAsync(string token) => await GetAsync<JsonWebToken>(GetTokenKey(token));

        /// <inheritdoc />
        public async Task RemoveTokenAsync(string token)
        {
            if (!await ExistsAsync(GetTokenKey(token)))
                return;
            await RemoveAsync(GetTokenKey(token));
        }

        /// <inheritdoc />
        public async Task SaveTokenAsync(JsonWebToken token, DateTime expires)
        {
            await AddAsync(GetTokenKey(token.AccessToken), token, expires.Subtract(DateTime.UtcNow));
            await AddAsync(GetBindRefreshTokenKey(token.RefreshToken), token, expires.Subtract(DateTime.UtcNow));
        }

        /// <inheritdoc />
        public async Task<bool> ExistsTokenAsync(string token) => await ExistsAsync(GetTokenKey(token));

        /// <inheritdoc />
        public async Task BindUserDeviceTokenAsync(string userId, string clientType, DeviceTokenBindInfo info, DateTime expires) => 
            await AddAsync(GetBindUserDeviceTokenKey(userId, clientType), info, expires.Subtract(DateTime.UtcNow));

        /// <inheritdoc />
        public async Task<DeviceTokenBindInfo> GetUserDeviceTokenAsync(string userId, string clientType) =>
            await GetAsync<DeviceTokenBindInfo>(GetBindUserDeviceTokenKey(userId, clientType));

        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="key">键名</param>
        /// <param name="value">值</param>
        /// <param name="expiration">过期时间</param>
        protected abstract Task AddAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// 移除指定键
        /// </summary>
        /// <param name="key">键名</param>
        protected abstract Task RemoveAsync(string key);

        /// <summary>
        /// 是否存在指定键
        /// </summary>
        /// <param name="key">键名</param>
        protected abstract Task<bool> ExistsAsync(string key);

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="key">键名</param>
        protected abstract Task<T> GetAsync<T>(string key);

        /// <summary>
        /// 获取刷新令牌缓存键
        /// </summary>
        /// <param name="token">刷新令牌</param>
        protected static string GetRefreshTokenKey(string token) => $"jwt:token:refresh:{token}";

        /// <summary>
        /// 获取访问令牌缓存键
        /// </summary>
        /// <param name="token">访问令牌</param>
        protected static string GetTokenKey(string token) => $"jwt:token:access:{token}";

        /// <summary>
        /// 获取绑定刷新令牌缓存键
        /// </summary>
        /// <param name="token">刷新令牌</param>
        protected static string GetBindRefreshTokenKey(string token) => $"jwt:token:bind:{token}";

        /// <summary>
        /// 获取绑定用户设备令牌缓存键
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型</param>
        protected static string GetBindUserDeviceTokenKey(string userId, string clientType) =>
            $"jwt:token:bind_user:{userId}:{clientType}";
    }
}
