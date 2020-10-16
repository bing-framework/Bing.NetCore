using System;
using Bing.AspNetCore.WebClientInfo;
using Bing.DependencyInjection;
using Bing.Logs.Core;
using Bing.Logs.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// AspNetCore日志上下文
    /// </summary>
    [Dependency(ServiceLifetime.Scoped, ReplaceExisting = true)]
    public class AspNetCoreLogContext : LogContext
    {
        /// <summary>
        /// Http上下文访问器
        /// </summary>
        protected IHttpContextAccessor HttpContextAccessor { get; }

        /// <summary>
        /// Web客户端信息提供程序
        /// </summary>
        protected IWebClientInfoProvider WebClientInfoProvider { get; }

        /// <summary>
        /// 初始化一个<see cref="AspNetCoreLogContext"/>类型的实例
        /// </summary>
        /// <param name="scopedDictionary">作用域字典</param>
        /// <param name="webClientInfoProvider">Web客户端信息提供程序</param>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        public AspNetCoreLogContext(ScopedDictionary scopedDictionary
            , IHttpContextAccessor httpContextAccessor
            , IWebClientInfoProvider webClientInfoProvider)
            : base(scopedDictionary)
        {
            HttpContextAccessor = httpContextAccessor;
            WebClientInfoProvider = webClientInfoProvider;
        }

        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        protected override LogContextInfo CreateInfo()
        {
            var logContextInfo = base.CreateInfo();

            logContextInfo.Ip = WebClientInfoProvider.ClientIpAddress;
            logContextInfo.Browser = WebClientInfoProvider.ClientIpAddress;

            logContextInfo.Url = HttpContextAccessor.HttpContext?.Request?.GetDisplayUrl();

            return logContextInfo;
        }

        /// <summary>
        /// 获取跟踪号
        /// </summary>
        protected override string GetTraceId()
        {
            var correlationId = HttpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"];
            if (!string.IsNullOrWhiteSpace(correlationId))
                return correlationId;
            var traceId = HttpContextAccessor.HttpContext?.TraceIdentifier;
            return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString() : Guid.TryParse(traceId, out var traceIdGuid) ? traceId : traceIdGuid.ToString();
        }
    }
}
