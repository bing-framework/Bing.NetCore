using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// 初始化一个<see cref="BingExceptionHandlingMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="options">异常处理选项配置</param>
        public BingExceptionHandlingMiddleware(RequestDelegate next, IOptions<BingExceptionHandlingOptions> options)
        {
            _next = next;
            _options = options.Value;
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
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<BingExceptionHandlingMiddleware>();
                if (context.RequestAborted.IsCancellationRequested && (e is TaskCanceledException || e is OperationCanceledException))
                    _options.OnRequestAborted?.Invoke(context, logger);
                else
                    _options.OnException?.Invoke(context, logger, e);
            }
        }
    }
}
