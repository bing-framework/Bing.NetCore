using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// 请求日志中间件
    /// </summary>
    [Obsolete("请使用 RequestResponseLoggerMiddleware 中间件")]
    public class RequestLogMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 初始化一个<see cref="RequestLogMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        public RequestLogMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            if (!ExecuteInterception(context))
            {
                await _next(context);
                return;
            }

            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;
                var sw = new Stopwatch();
                sw.Start();
                await _next(context);
                sw.Stop();
                await WriteLogAsync(context, sw);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        /// <summary>
        /// 是否执行拦截
        /// </summary>
        /// <param name="context">Http上下文</param>
        protected virtual bool ExecuteInterception(HttpContext context)
        {
            if (context.Request.Path.Value.Contains("swagger"))
                return false;
            return true;
        }

        /// <summary>
        /// 记录请求日志
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <param name="stopwatch">计时器</param>
        private async Task WriteLogAsync(HttpContext context, Stopwatch stopwatch)
        {
            if (context == null)
                return;
            if (IgnoreOctetStream(context.Response))
            {
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                return;
            }

            var log = context?.RequestServices?.GetService<ILog>() ?? NullLog.Instance;
            log
                .Class(this.GetType().FullName)
                .Caption("请求日志中间件")
                .Content(new Dictionary<string, string>
                {
                    { "请求方法", context.Request.Method },
                    {
                        "请求地址",
                        $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}"
                    },
                    { "IP", context.Connection.RemoteIpAddress.ToString() },
                    { "请求耗时", $"{stopwatch.Elapsed.TotalMilliseconds} 毫秒" },
                    { "请求内容", await FormatRequestAsync(context.Request) },
                    { "响应内容", await FormatResponseAsync(context.Response) }
                });
            log.Trace();
        }

        /// <summary>
        /// 格式化请求内容
        /// </summary>
        /// <param name="request">Http请求</param>
        private async Task<string> FormatRequestAsync(HttpRequest request)
        {
            request.EnableBuffering();
            request.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }

        /// <summary>
        /// 格式化响应内容
        /// </summary>
        /// <param name="response">Http响应</param>
        private async Task<string> FormatResponseAsync(HttpResponse response)
        {
            if (response.HasStarted)
                return string.Empty;
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }

        /// <summary>
        /// 忽略二进制流
        /// </summary>
        /// <param name="response">Http响应</param>
        private bool IgnoreOctetStream(HttpResponse response) => response.ContentType == "application/octet-stream";
    }
}
