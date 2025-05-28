using Bing.AspNetCore.Authentication;
using Bing.Security.Claims;
using Bing.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 提供 JWT Bearer 认证的扩展方法，用于快速集成 基于 JWT 的身份验证。
/// </summary>
public static class BingJwtBearerExtensions
{
    /// <summary>
    /// 添加 JWT Bearer 认证（默认 Scheme 名称）。
    /// </summary>
    /// <param name="builder">身份认证构建器</param>
    /// <returns>身份认证构建器</returns>
    public static AuthenticationBuilder AddBingJwtBearer(this AuthenticationBuilder builder)
        => builder.AddBingJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

    /// <summary>
    /// 添加 JWT Bearer 认证（默认 Scheme 名称，可配置）。
    /// </summary>
    /// <param name="builder">身份认证构建器</param>
    /// <param name="configureOptions">JWT 配置选项</param>
    /// <returns>身份认证构建器</returns>
    public static AuthenticationBuilder AddBingJwtBearer(this AuthenticationBuilder builder, Action<JwtBearerOptions> configureOptions)
        => builder.AddBingJwtBearer(JwtBearerDefaults.AuthenticationScheme, configureOptions);

    /// <summary>
    /// 添加 JWT Bearer 认证（支持自定义 Scheme）。
    /// </summary>
    /// <param name="builder">身份认证构建器</param>
    /// <param name="authenticationScheme">自定义 Scheme 名称</param>
    /// <param name="configureOptions">JWT 配置选项</param>
    /// <returns>身份认证构建器</returns>
    public static AuthenticationBuilder AddBingJwtBearer(this AuthenticationBuilder builder, string authenticationScheme, Action<JwtBearerOptions> configureOptions)
        => builder.AddBingJwtBearer(authenticationScheme, "Bearer", configureOptions);

    /// <summary>
    /// 添加 JWT Bearer 认证（支持自定义 Scheme 和显示名称）。
    /// </summary>
    /// <param name="builder">身份认证构建器</param>
    /// <param name="authenticationScheme">自定义 Scheme 名称</param>
    /// <param name="displayName">认证显示名称</param>
    /// <param name="configureOptions">JWT 配置选项</param>
    /// <returns>身份认证构建器</returns>
    public static AuthenticationBuilder AddBingJwtBearer(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<JwtBearerOptions> configureOptions)
    {
        // 配置 BingClaimsPrincipalFactory 选项，提供远程刷新支持
        builder.Services.Configure<BingClaimsPrincipalFactoryOptions>(options =>
        {
            var jwtBearerOptions = new JwtBearerOptions();
            configureOptions?.Invoke(jwtBearerOptions);
            // 如果 Authority（认证地址）不为空，则设置远程刷新 URL
            if (!jwtBearerOptions.Authority.IsNullOrEmpty())
            {
                options.RemoteRefreshUrl = jwtBearerOptions.Authority.RemoveStart("/") + options.RemoteRefreshUrl;
            }
        });

        // 添加 JWT Bearer 认证
        return builder.AddJwtBearer(authenticationScheme, displayName, options =>
        {
            configureOptions?.Invoke(options);
            // 事件处理配置
            options.Events ??= new JwtBearerEvents();
            var previousOnChallenge = options.Events.OnChallenge;
            // 自定义 OnChallenge 事件
            options.Events.OnChallenge = async eventContext =>
            {
                await previousOnChallenge(eventContext);
                // 如果请求已处理（Handled），或者已有错误信息，则直接返回
                if (eventContext.Handled ||
                    !string.IsNullOrEmpty(eventContext.Error) ||
                    !string.IsNullOrEmpty(eventContext.ErrorDescription) ||
                    !string.IsNullOrEmpty(eventContext.ErrorUri))
                    return;
                // 解析自定义错误信息
                var tokenUnauthorizedErrorInfo = eventContext.HttpContext.RequestServices.GetService<BingAspNetCoreTokenUnauthorizedErrorInfo>();
                if (string.IsNullOrEmpty(tokenUnauthorizedErrorInfo.Error) &&
                    string.IsNullOrEmpty(tokenUnauthorizedErrorInfo.ErrorDescription) &&
                    string.IsNullOrEmpty(tokenUnauthorizedErrorInfo.ErrorInfo))
                    return;
                // 赋值到 OnChallenge 事件上下文
                eventContext.Error = tokenUnauthorizedErrorInfo.Error;
                eventContext.ErrorDescription = tokenUnauthorizedErrorInfo.ErrorDescription;
                eventContext.ErrorUri = tokenUnauthorizedErrorInfo.ErrorInfo;
            };
        });
    }
}
