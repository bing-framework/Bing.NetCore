﻿namespace Bing.Identity.JwtBearer.Abstractions;

/// <summary>
/// Jwt令牌构建器
/// </summary>
public interface IJsonWebTokenBuilder
{
    /// <summary>
    /// 创建令牌
    /// </summary>
    /// <param name="payload">负载</param>
    Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload);

    /// <summary>
    /// 创建令牌
    /// </summary>
    /// <param name="payload">负载</param>
    /// <param name="options">Jwt选项配置</param>
    Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload, JwtOptions options);

    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    Task<JsonWebToken> RefreshAsync(string refreshToken);
}
