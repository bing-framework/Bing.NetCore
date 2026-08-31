using Bing.DependencyInjection;

namespace Bing.AspNetCore.Authentication;

/// <summary>
/// 表示 ASP.NET Core 令牌未通过授权时的标准错误信息。
/// </summary>
/// <remarks>
/// 该类用于承载 OAuth 认证或 JWT 验证失败时的错误代码、描述和附加上下文。
/// </remarks>
public class BingAspNetCoreTokenUnauthorizedErrorInfo : IScopedDependency
{
    /// <summary>
    /// 获取或设置符合 OAuth 2.0 或 OpenID Connect 约定的错误代码。
    /// </summary>
    /// <remarks>
    /// 常见值包括 <c>invalid_token</c> 和 <c>unauthorized_client</c>。
    /// </remarks>
    public string? Error { get; set; }

    /// <summary>
    /// 获取或设置解释 <see cref="Error"/> 代码的详细描述。
    /// </summary>
    /// <remarks>
    /// 该值可能会返回给客户端，内容不应包含密钥、令牌或内部堆栈等敏感信息。
    /// </remarks>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// 获取或设置附加错误上下文。
    /// </summary>
    /// <remarks>
    /// 该值主要用于日志或诊断；写入客户端响应前应确认其中不包含敏感信息。
    /// </remarks>
    public string? ErrorInfo { get; set; }
}
