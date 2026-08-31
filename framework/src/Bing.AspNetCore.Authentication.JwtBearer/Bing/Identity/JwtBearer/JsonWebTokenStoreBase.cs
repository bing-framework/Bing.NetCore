using Bing.Identity.JwtBearer.Abstractions;

namespace Bing.Identity.JwtBearer;

/// <summary>
/// Jwt令牌存储器基类
/// </summary>
public abstract class JsonWebTokenStoreBase : IJsonWebTokenStore
{
    /// <summary>
    /// 获取刷新令牌
    /// </summary>
    /// <param name="token">刷新令牌</param>
    /// <returns>包含刷新令牌信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    public async Task<RefreshToken> GetRefreshTokenAsync(string token) => await GetAsync<RefreshToken>(GetRefreshTokenKey(token));

    /// <summary>
    /// 异步保存刷新令牌，并按令牌的 UTC 过期时间设置存储期限。
    /// </summary>
    /// <param name="token">刷新令牌</param>
    public async Task SaveRefreshTokenAsync(RefreshToken token) => await AddAsync(GetRefreshTokenKey(token.Value), token, token.EndUtcTime.Subtract(DateTime.UtcNow));

    /// <summary>
    /// 异步移除刷新令牌及其关联的访问令牌绑定。
    /// </summary>
    /// <param name="token">刷新令牌</param>
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

    /// <summary>
    /// 获取访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>包含访问令牌信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    public async Task<JsonWebToken> GetTokenAsync(string token) => await GetAsync<JsonWebToken>(GetTokenKey(token));

    /// <summary>
    /// 移除访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    public async Task RemoveTokenAsync(string token)
    {
        if (!await ExistsAsync(GetTokenKey(token)))
            return;
        await RemoveAsync(GetTokenKey(token));
    }

    /// <summary>
    /// 异步保存访问令牌及其刷新令牌绑定，并使用指定时间设置过期期限。
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="expires">过期时间</param>
    public async Task SaveTokenAsync(JsonWebToken token, DateTime expires)
    {
        await AddAsync(GetTokenKey(token.AccessToken), token, expires.Subtract(DateTime.UtcNow));
        await AddAsync(GetBindRefreshTokenKey(token.RefreshToken), token, expires.Subtract(DateTime.UtcNow));
    }

    /// <summary>
    /// 是否存在访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>表示异步操作的任务，结果为访问令牌是否存在。</returns>
    public async Task<bool> ExistsTokenAsync(string token) => await ExistsAsync(GetTokenKey(token));

    /// <summary>
    /// 绑定用户设备令牌
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="clientType">客户端类型</param>
    /// <param name="info">设备信息</param>
    /// <param name="expires">过期时间</param>
    public async Task BindUserDeviceTokenAsync(string userId, string clientType, DeviceTokenBindInfo info, DateTime expires) => 
        await AddAsync(GetBindUserDeviceTokenKey(userId, clientType), info, expires.Subtract(DateTime.UtcNow));

    /// <summary>
    /// 获取用户设备令牌
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="clientType">客户端类型</param>
    /// <returns>包含用户设备令牌绑定信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
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
    /// <returns>表示异步操作的任务，结果为指定键是否存在。</returns>
    protected abstract Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="key">键名</param>
    /// <returns>包含指定类型数据的异步任务；缓存未命中时结果为 <see langword="null"/>。</returns>
    protected abstract Task<T> GetAsync<T>(string key);

    /// <summary>
    /// 获取刷新令牌缓存键
    /// </summary>
    /// <param name="token">刷新令牌</param>
    /// <returns>刷新令牌对应的缓存键。</returns>
    protected static string GetRefreshTokenKey(string token) => $"jwt:token:refresh:{token}";

    /// <summary>
    /// 获取访问令牌缓存键
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>访问令牌对应的缓存键。</returns>
    protected static string GetTokenKey(string token) => $"jwt:token:access:{token}";

    /// <summary>
    /// 获取绑定刷新令牌缓存键
    /// </summary>
    /// <param name="token">刷新令牌</param>
    /// <returns>刷新令牌与访问令牌绑定关系对应的缓存键。</returns>
    protected static string GetBindRefreshTokenKey(string token) => $"jwt:token:bind:{token}";

    /// <summary>
    /// 获取绑定用户设备令牌缓存键
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="clientType">客户端类型</param>
    /// <returns>用户设备令牌绑定信息对应的缓存键。</returns>
    protected static string GetBindUserDeviceTokenKey(string userId, string clientType) =>
        $"jwt:token:bind_user:{userId}:{clientType}";
}
