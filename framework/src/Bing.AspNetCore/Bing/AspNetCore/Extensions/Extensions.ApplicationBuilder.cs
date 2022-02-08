using System;
using Bing.AspNetCore.Logs;
using Bing.AspNetCore.RealIp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Extensions
{
    /// <summary>
    /// 应用程序生成器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class BingApplicationBuilderExtensions
    {
        /// <summary>
        /// 注册请求日志中间件
        /// </summary>
        /// <param name="builder">应用程序生成器</param>
        public static IApplicationBuilder UseRequestLog(this IApplicationBuilder builder) => builder.UseMiddleware<RequestLogMiddleware>();

        /// <summary>
        /// 注册真实IP中间件
        /// </summary>
        /// <param name="builder">应用程序生成器</param>
        /// <param name="setupAct">配置操作</param>
        public static IApplicationBuilder UseRealIp(this IApplicationBuilder builder, Action<RealIpOptions> setupAct = null)
        {
            var options = new RealIpOptions { HeaderKey = "x-forwarded-for" };
            setupAct?.Invoke(options);
            return builder.UseMiddleware<RealIpMiddleware>(Options.Create(options));
        }

        /// <summary>
        /// 注册真实IP
        /// </summary>
        /// <param name="hostBuilder">Web主机生成器</param>
        /// <param name="headerKey">请求头键名</param>
        public static IWebHostBuilder UseRealIp(this IWebHostBuilder hostBuilder, string headerKey = "X-Forwarded-For")
        {
            if (hostBuilder == null)
                throw new ArgumentNullException(nameof(hostBuilder));
            if (hostBuilder.GetSetting(nameof(UseRealIp)) != null)
                return hostBuilder;
            hostBuilder.UseSetting(nameof(UseRealIp), true.ToString());

            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new RealIpFilter());
                services.Configure<RealIpOptions>(options => options.HeaderKey = headerKey);
            });

            return hostBuilder;
        }
    }
}
