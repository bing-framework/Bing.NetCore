using System;
using System.Threading.Tasks;
using Bing.Caching;

namespace Bing.Permissions.Identity.JwtBearer.Internal
{
    /// <summary>
    /// Jwt令牌存储器
    /// </summary>
    internal sealed class JsonWebTokenStore : JsonWebTokenStoreBase, IJsonWebTokenStore
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

        /// <inheritdoc />
        protected override async Task AddAsync<T>(string key, T value, TimeSpan? expiration = null) => await _cache.AddAsync(key, value, expiration);

        /// <inheritdoc />
        protected override async Task RemoveAsync(string key) => await _cache.RemoveAsync(key);

        /// <inheritdoc />
        protected override async Task<bool> ExistsAsync(string key) => await _cache.ExistsAsync(key);

        /// <inheritdoc />
        protected override async Task<T> GetAsync<T>(string key) => await _cache.GetAsync<T>(key);
    }
}
