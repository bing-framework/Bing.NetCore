using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

namespace Bing.Webs.Middlewares
{
    /// <summary>
    /// 请求日志中间件
    /// </summary>
    public class RequestLogMiddleware
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
        /// 执行方法
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;
                var sw = new Stopwatch();
                sw.Start();
                await _next(context);
                sw.Stop();
                await WriteLog(context, sw);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        /// <summary>
        /// 记录请求日志
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <param name="stopwatch">计时器</param>
        /// <returns></returns>
        private async Task WriteLog(HttpContext context, Stopwatch stopwatch)
        {
            if (context == null)
            {
                return;
            }

            var log = Log.GetLog(this).Caption("请求日志中间件");
            log.Content(new Dictionary<string, string>()
            {
                {"请求方法", context.Request.Method},
                {
                    "请求地址",
                    $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}"
                },
                {"IP", context.Connection.RemoteIpAddress.ToString()},
                {"请求耗时", $"{stopwatch.Elapsed.TotalMilliseconds} 毫秒"},
                {"请求内容", await FormatRequest(context.Request)},
                {"响应内容", await FormatResponse(context.Response)}
            });
            log.Info();
        }

        /// <summary>
        /// 格式化请求内容
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <returns></returns>
        private async Task<string> FormatRequest(HttpRequest request)
        {
            request.EnableRewind();
            request.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }

        /// <summary>
        /// 格式化响应内容
        /// </summary>
        /// <param name="response">Http响应</param>
        /// <returns></returns>
        private async Task<string> FormatResponse(HttpResponse response)
        {
            if (response.HasStarted)
            {
                return string.Empty;
            }
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text?.Trim().Replace("\r", "").Replace("\n", "");
        }
    }
}
