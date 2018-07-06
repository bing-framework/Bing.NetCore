using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Helpers;
using Bing.Webs.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Webs.Extensions
{
    /// <summary>
    /// 中间件扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册错误日志管道
        /// </summary>
        /// <param name="builder">应用程序生成器</param>
        /// <returns></returns>
        public static IApplicationBuilder UseErrorLog(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ErrorLogMiddleware>();
        }

        /// <summary>
        /// 启用静态请求上下文
        /// </summary>
        /// <param name="builder">应用程序生成器</param>
        /// <returns></returns>
        public static IApplicationBuilder UseStaticHttpContext(this IApplicationBuilder builder)
        {
            var httpContextAccessor = builder.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            Web.HttpContextAccessor = httpContextAccessor;
            return builder;
        }
    }
}
