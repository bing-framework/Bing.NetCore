using System;
using Bing.Permissions.Authorization.Middlewares;
using Bing.Permissions.Authorization.Policies;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Permissions.Identity.JwtBearer.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Permissions.Extensions
{
    /// <summary>
    /// 扩展服务
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Jwt客户授权中间件
        /// </summary>
        /// <param name="builder">应用程序生成器</param>
        /// <param name="action">操作</param>
        public static IApplicationBuilder UseJwtCustomerAuthorize(this IApplicationBuilder builder,
            Action<IJsonWebTokenCustomerAuthorizeOption> action = null)
        {
            var option =
                builder.ApplicationServices.GetService<IJsonWebTokenCustomerAuthorizeOption>() as
                    JsonWebTokenCustomerAuthorizeOption ?? new JsonWebTokenCustomerAuthorizeOption();

            action?.Invoke(option);
            return builder.UseMiddleware<JsonWebTokenCustomerAuthorizeMiddleware>(option.ValidatePayload,
                option.AnonymousPaths);
        }

        /// <summary>
        /// 注册Jwt服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        public static void AddJwt(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtOptions>(o => configuration.GetSection(nameof(JwtOptions)).Bind(o));
            services.TryAddSingleton<IJsonWebTokenBuilder, JsonWebTokenBuilder>();
            services.TryAddSingleton<IJsonWebTokenStore,JsonWebTokenStore>();
            services.TryAddSingleton<IJsonWebTokenValidator, JsonWebTokenValidator>();
            services.TryAddSingleton<IJsonWebTokenCustomerAuthorizeOption, JsonWebTokenCustomerAuthorizeOption>();
            services.TryAddSingleton<IJsonWebTokenAuthorizationRequirement, JsonWebTokenAuthorizationRequirement>();
            services.AddSingleton<IAuthorizationHandler, JsonWebTokenAuthorizationHandler>();
        }

        /// <summary>
        /// 获取Jwt身份认证选项
        /// </summary>
        /// <param name="configuration">配置</param>
        private static JwtOptions GetOptions(IConfiguration configuration) => configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
    }
}
