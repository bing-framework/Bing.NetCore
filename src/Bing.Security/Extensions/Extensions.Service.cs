using Bing.Security.Identity.JwtBearer;
using Bing.Security.Identity.JwtBearer.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Security.Extensions
{
    /// <summary>
    /// 扩展服务
    /// </summary>
    public static partial class Extensions
    {
        public static void AddJwt(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JsonWebTokenOptions>(o => configuration.GetSection(nameof(JsonWebTokenOptions)).Bind(o));
            var options = GetOptions(configuration);

            // 1、配置授权服务

            // 2、配置认证服务

            // 3、设置Jw验证

            services.TryAddSingleton<IJsonWebTokenBuilder, JsonWebTokenBuilder>();
            services.TryAddSingleton<IJsonWebTokenStore,JsonWebTokenStore>();
            services.TryAddSingleton<IJsonWebTokenValidator, JsonWebTokenValidator>();
        }

        /// <summary>
        /// 获取Jwt身份认证选项
        /// </summary>
        /// <param name="configuration">配置</param>
        private static JsonWebTokenOptions GetOptions(IConfiguration configuration) => configuration.GetSection(nameof(JsonWebTokenOptions)).Get<JsonWebTokenOptions>();
    }
}
