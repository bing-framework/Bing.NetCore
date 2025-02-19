using Bing.DependencyInjection;

namespace Bing.AspNetCore.Authentication;

/// <summary>
/// 表示 ASP.NET Core 令牌（Token）未经授权的错误信息。
/// </summary>
/// <remarks>
/// 该类用于存储 身份验证失败 或 访问令牌无效 时的错误信息，<br />
/// 例如 OAuth 认证、JWT 令牌验证失败等情况。<br />
/// 
/// 适用于： <br />
/// - 认证失败返回标准化错误信息 <br />
/// - 记录日志或 API 响应格式化
/// </remarks>
public class BingAspNetCoreTokenUnauthorizedErrorInfo : IScopedDependency
{
    /// <summary>
    /// 错误代码，例如 "invalid_token"、"unauthorized_client" 等。<br />
    /// </summary>
    /// <remarks>
    /// 此错误代码通常符合 OAuth 2.0 或 OpenID Connect 规范，表示身份验证失败的原因。<br />
    /// 
    /// 示例：<br />
    /// - "invalid_token"：令牌无效或已过期 <br />
    /// - "unauthorized_client"：客户端未经授权
    /// </remarks>
    public string? Error { get; set; }

    /// <summary>
    /// 错误的详细描述信息，解释 `Error` 代码的具体原因。<br />
    /// </summary>
    /// <remarks>
    /// 该字段通常用于 用户可读的错误信息，例如：<br />
    /// - "The access token is expired" <br />
    /// - "Client does not have permission to access this resource"
    /// </remarks>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// 附加的错误信息，可包含调试信息或其他错误上下文。<br />
    /// </summary>
    /// <remarks>
    /// 该字段通常 用于日志记录 或 API 调试，例如：<br />
    /// - "Token expired at 2024-02-18T12:30:00Z" <br />
    /// - "Request ID: abc123456789"
    /// </remarks>
    public string? ErrorInfo { get; set; }
}
