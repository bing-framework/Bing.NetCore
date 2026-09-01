using Bing.Identity.JwtBearer;
using Bing.Identity.JwtBearer.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Authorization.JwtBearer;

/// <summary>
/// 执行 JWT 客户端授权检查的中间件。
/// </summary>
public class JsonWebTokenCustomerAuthorizeMiddleware
{
    /// <summary>
    /// 请求处理管道中的后续中间件委托。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// JWT 选项配置。
    /// </summary>
    private readonly JwtOptions _options;

    /// <summary>
    /// JWT 负载校验委托。
    /// </summary>
    private readonly Func<IDictionary<string, string>, JwtOptions, bool> _validatePayload;

    /// <summary>
    /// 匿名访问路径列表。
    /// </summary>
    private readonly IList<string> _anonymousPathList;

    /// <summary>
    /// JWT 令牌校验器。
    /// </summary>
    private readonly IJsonWebTokenValidator _tokenValidator;

    /// <summary>
    /// 初始化一个 <see cref="JsonWebTokenCustomerAuthorizeMiddleware"/> 类型的实例。
    /// </summary>
    /// <param name="next">请求处理管道中的下一个中间件。</param>
    /// <param name="options">JWT 选项配置。</param>
    /// <param name="tokenValidator">JWT 令牌校验器。</param>
    /// <param name="validatePayload">JWT 负载校验委托。</param>
    /// <param name="anonymousPathList">允许匿名访问的路径列表。</param>
    public JsonWebTokenCustomerAuthorizeMiddleware(
        RequestDelegate next
        , IOptions<JwtOptions> options
        , IJsonWebTokenValidator tokenValidator
        , Func<IDictionary<string, string>, JwtOptions, bool> validatePayload
        , IList<string> anonymousPathList)
    {
        _next = next;
        _options = options.Value;
        _tokenValidator = tokenValidator;
        _validatePayload = validatePayload;
        _anonymousPathList = anonymousPathList;
    }

    /// <summary>
    /// 执行 JWT 客户端授权检查。
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <remarks>匿名路径直接放行，其余请求必须提供并通过 Bearer 令牌校验。</remarks>
    public async Task Invoke(HttpContext context)
    {
        // 如果是匿名访问路径，则直接跳过
        if (_anonymousPathList.Contains(context.Request.Path.Value))
        {
            await _next(context);
            return;
        }

        var result = context.Request.Headers.TryGetValue("Authorization", out var authStr);
        if (!result || string.IsNullOrWhiteSpace(authStr.ToString()))
            throw new UnauthorizedAccessException("未授权，请传递Header头的Authorization参数");
        // 校验以及自定义校验
        result = _tokenValidator.Validate(authStr.ToString().Substring("Bearer ".Length).Trim(), _options,
            _validatePayload);
        if (!result)
            throw new UnauthorizedAccessException("验证失败，请查看传递的参数是否正确或是否有权限访问该地址。");
        await _next(context);
    }
}
