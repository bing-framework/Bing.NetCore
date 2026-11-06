namespace Bing.Identity.JwtBearer.Abstractions;

/// <summary>
/// 令牌Payload存储器
/// </summary>
public interface ITokenPayloadStore
{
    /// <summary>
    /// 保存令牌对应的负载数据及过期时间。
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="payload">负载字典</param>
    /// <param name="expires">过期时间</param>
    Task SaveAsync(string token, IDictionary<string, string> payload, DateTime expires);

    /// <summary>
    /// 移除指定令牌的负载数据。
    /// </summary>
    /// <param name="token">令牌</param>
    Task RemoveAsync(string token);

    /// <summary>
    /// 获取Payload
    /// </summary>
    /// <param name="token">令牌</param>
    /// <returns>包含令牌负载字典的异步任务；未找到时结果为 <see langword="null"/>。</returns>
    Task<IDictionary<string, string>> GetAsync(string token);
}
