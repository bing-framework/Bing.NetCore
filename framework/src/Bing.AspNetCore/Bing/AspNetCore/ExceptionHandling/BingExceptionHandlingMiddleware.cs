using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 异常处理中间件
    /// </summary>
    public class BingExceptionHandlingMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 异常处理选项配置
        /// </summary>
        private readonly BingExceptionHandlingOptions _options;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILogger<BingExceptionHandlingMiddleware> _logger;

        /// <summary>
        /// 清空缓存头委托
        /// </summary>
        private readonly Func<object, Task> _clearCacheHeaderDelegate;

        /// <summary>
        /// 初始化一个<see cref="BingExceptionHandlingMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="options">异常处理选项配置</param>
        /// <param name="logger">日志</param>
        public BingExceptionHandlingMiddleware(RequestDelegate next, IOptions<BingExceptionHandlingOptions> options, ILogger<BingExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
            _clearCacheHeaderDelegate = ClearCacheHeaders;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {
                // 如果响应已经开始，我们什么也不能做，只能中止
                if(context.Response.HasStarted)
                {
                    _logger.LogWarning("An exception occurred, but response has already started!");
                    throw;
                }
                if (context.RequestAborted.IsCancellationRequested && (e is TaskCanceledException || e is OperationCanceledException))
                    _options.OnRequestAborted?.Invoke(context, _logger);
                else
                    _options.OnException?.Invoke(context, _logger, e);
            }
        }

        /// <summary>
        /// 处理并包装异常
        /// </summary>
        /// <param name="httpContext">Http上下文</param>
        /// <param name="exception">异常</param>
        private async Task HandleAndWarpException(HttpContext httpContext, Exception exception)
        {
            _logger.LogException(exception);

            httpContext.Response.Clear();
            httpContext.Response.StatusCode = 200;
            httpContext.Response.OnStarting(_clearCacheHeaderDelegate, httpContext.Response);
            httpContext.Response.Headers.Add("_BingErrorFormat", "true");

        }

        /// <summary>
        /// 清空缓存头
        /// </summary>
        /// <param name="state">状态</param>
        private Task ClearCacheHeaders(object state)
        {
            var response = (HttpResponse)state;
            response.Headers[HeaderNames.CacheControl] = "no-cache";
            response.Headers[HeaderNames.Pragma] = "no-cache";
            response.Headers[HeaderNames.Expires] = "-1";
            response.Headers.Remove(HeaderNames.ETag);

            return Task.CompletedTask;
        }
    }
}
