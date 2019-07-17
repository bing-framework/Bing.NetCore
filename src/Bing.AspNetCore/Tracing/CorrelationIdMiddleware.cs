using System.Threading.Tasks;
using Bing.Dependency;
using Bing.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Tracing
{
    /// <summary>
    /// 跟踪关联ID提供程序
    /// </summary>
    public class CorrelationIdMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 跟踪关联ID配置选项信息
        /// </summary>
        private readonly CorrelationIdOptions _options;

        /// <summary>
        /// 跟踪关联ID提供程序
        /// </summary>
        private readonly ICorrelationIdProvider _correlationIdProvider;

        /// <summary>
        /// 初始化一个<see cref="CorrelationIdMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="options">跟踪关联ID配置选项信息</param>
        /// <param name="correlationIdProvider">跟踪关联ID提供程序</param>
        public CorrelationIdMiddleware(RequestDelegate next, IOptions<CorrelationIdOptions> options, ICorrelationIdProvider correlationIdProvider)
        {
            _next = next;
            _options = options.Value;
            _correlationIdProvider = correlationIdProvider;
        }

        /// <summary>
        /// 执行方法
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            var correlationId = _correlationIdProvider.Get();
            try
            {
                await _next(context);
            }
            finally
            {
                CheckAndSetCorrelationIdOnResponse(context, _options, correlationId);
            }
        }

        /// <summary>
        /// 检查并设置跟踪关联ID在响应内容
        /// </summary>
        /// <param name="httpContext">Http上下文</param>
        /// <param name="options">跟踪关联ID配置选项信息</param>
        /// <param name="correlationId">跟踪关联ID</param>
        protected virtual void CheckAndSetCorrelationIdOnResponse(HttpContext httpContext, CorrelationIdOptions options,
            string correlationId)
        {
            if (httpContext.Response.HasStarted)
                return;
            if (!options.SetResponseHeader)
                return;
            if (httpContext.Response.Headers.ContainsKey(options.HttpHeaderName))
                return;
            httpContext.Response.Headers[options.HttpHeaderName] = correlationId;
        }
    }
}
