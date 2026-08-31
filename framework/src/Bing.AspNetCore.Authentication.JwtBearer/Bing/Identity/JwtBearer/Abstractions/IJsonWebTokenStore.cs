namespace Bing.Identity.JwtBearer.Abstractions;

/// <summary>
/// Jwt令牌存储器
/// </summary>
public interface IJsonWebTokenStore
{
    /// <summary>
    /// 获取刷新令牌
    /// </summary>
    /// <param name="token">刷新令牌</param>
    /// <returns>包含刷新令牌信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    Task<RefreshToken> GetRefreshTokenAsync(string token);

    /// <summary>
    /// 异步保存刷新令牌，并按令牌的 UTC 过期时间设置存储期限。
    /// </summary>
    /// <param name="token">刷新令牌</param>
    Task SaveRefreshTokenAsync(RefreshToken token);

    /// <summary>
    /// 异步移除刷新令牌及其关联的访问令牌绑定。
    /// </summary>
    /// <param name="token">刷新令牌</param>
    Task RemoveRefreshTokenAsync(string token);

    /// <summary>
    /// 获取访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>包含访问令牌信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    Task<JsonWebToken> GetTokenAsync(string token);

    /// <summary>
    /// 移除访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    Task RemoveTokenAsync(string token);

    /// <summary>
    /// 异步保存访问令牌及其刷新令牌绑定，并使用指定时间设置过期期限。
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="expires">过期时间</param>
    Task SaveTokenAsync(JsonWebToken token, DateTime expires);

    /// <summary>
    /// 是否存在访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>表示异步操作的任务，结果为访问令牌是否存在。</returns>
    Task<bool> ExistsTokenAsync(string token);

    /// <summary>
    /// 绑定用户设备令牌
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="clientType">客户端类型</param>
    /// <param name="info">设备信息</param>
    /// <param name="expires">过期时间</param>
    Task BindUserDeviceTokenAsync(string userId, string clientType, DeviceTokenBindInfo info, DateTime expires);

    /// <summary>
    /// 获取用户设备令牌
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="clientType">客户端类型</param>
    /// <returns>包含用户设备令牌绑定信息的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    Task<DeviceTokenBindInfo> GetUserDeviceTokenAsync(string userId, string clientType);
}
