using System;
using System.Text;
using Bing.Permissions.Authorization.Middlewares;
using Bing.Permissions.Authorization.Policies;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Permissions.Identity.JwtBearer.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

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
            var options = GetOptions(configuration);
            services.TryAddSingleton<IJsonWebTokenBuilder, JsonWebTokenBuilder>();
            services.TryAddSingleton<IJsonWebTokenStore,JsonWebTokenStore>();
            services.TryAddSingleton<IJsonWebTokenValidator, JsonWebTokenValidator>();
            services.TryAddSingleton<IJsonWebTokenCustomerAuthorizeOption, JsonWebTokenCustomerAuthorizeOption>();
            services.TryAddSingleton<IJsonWebTokenAuthorizationRequirement, JsonWebTokenAuthorizationRequirement>();
            services.TryAddSingleton<ITokenPayloadStore, TokenPayloadStore>();
            services.AddSingleton<IAuthorizationHandler, JsonWebTokenAuthorizationHandler>();
            services.AddAuthorization(o =>
                {
                    o.AddPolicy("jwt", policy => policy.Requirements.Add(new JsonWebTokenAuthorizationRequirement()));
                })
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(o =>
                {
                    o.TokenValidationParameters = GetValidationParameters(options);
                    o.SaveToken = true;
                });
        }

        /// <summary>
        /// 获取Jwt身份认证选项
        /// </summary>
        /// <param name="configuration">配置</param>
        private static JwtOptions GetOptions(IConfiguration configuration) => configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();

        /// <summary>
        /// 获取验证参数
        /// </summary>
        /// <param name="options">Jwt选项配置</param>
        private static TokenValidationParameters GetValidationParameters(JwtOptions options)
        {
            return new TokenValidationParameters()
            {
                ValidateIssuerSigningKey = true, // 是否验证发行者签名密钥
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
                ValidateIssuer = true, // 是否验证发行者
                ValidIssuer = options.Issuer,
                ValidateAudience = true, // 是否验证接收者
                ValidAudience = options.Audience,
                ValidateLifetime = true, // 是否验证超时
                LifetimeValidator = (before, expires, token, param) => expires > DateTime.UtcNow,
                ClockSkew = TimeSpan.FromSeconds(30), // 缓冲过期时间，总的有效时间等于该时间加上Jwt的过期时间，如果不配置，则默认是5分钟
                RequireExpirationTime = true
            };
        }
    }
}
