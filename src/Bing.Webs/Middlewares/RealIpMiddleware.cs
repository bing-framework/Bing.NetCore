using System;
using System.Net;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Http;

namespace Bing.Webs.Middlewares
{
    /// <summary>
    /// 真实IP中间件
    /// </summary>
    public class RealIpMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 初始化一个<see cref="RealIpMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        public RealIpMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <returns></returns>
        public Task Invoke(HttpContext context)
        {
            var headers = context.Request.Headers;
            if (headers.ContainsKey("X-Forwarded-For"))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(headers["X-Forwarded-For"].ToString()
                    .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)[0]);
            }

            return _next(context);
        }
    }
}
