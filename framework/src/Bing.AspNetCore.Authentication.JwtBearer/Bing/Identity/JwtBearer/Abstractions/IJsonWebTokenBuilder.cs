namespace Bing.Identity.JwtBearer.Abstractions;

/// <summary>
/// Jwt令牌构建器
/// </summary>
public interface IJsonWebTokenBuilder
{
    /// <summary>
    /// 创建令牌
    /// </summary>
    /// <param name="payload">负载</param>
    /// <returns>包含新创建 JWT 的异步任务。</returns>
    Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload);

    /// <summary>
    /// 创建令牌
    /// </summary>
    /// <param name="payload">负载</param>
    /// <param name="options">Jwt选项配置</param>
    /// <returns>包含按指定配置创建 JWT 的异步任务。</returns>
    Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload, JwtOptions options);

    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>包含刷新后 JWT 的异步任务。</returns>
    Task<JsonWebToken> RefreshAsync(string refreshToken);
}
