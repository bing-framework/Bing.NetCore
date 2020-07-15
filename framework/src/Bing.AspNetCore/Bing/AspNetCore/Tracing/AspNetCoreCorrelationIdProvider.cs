using System;
using Bing.DependencyInjection;
using Bing.Extensions;
using Bing.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Tracing
{
    /// <summary>
    /// AspNetCore 跟踪关联ID提供程序
    /// </summary>
    [Dependency(ServiceLifetime.Transient, ReplaceExisting = true)]
    public class AspNetCoreCorrelationIdProvider : ICorrelationIdProvider, ITransientDependency
    {
        /// <summary>
        /// Http上下文访问器
        /// </summary>
        protected IHttpContextAccessor HttpContextAccessor { get; }

        /// <summary>
        /// 跟踪关联ID配置选项信息
        /// </summary>
        protected CorrelationIdOptions Options { get; }

        /// <summary>
        /// 初始化一个<see cref="AspNetCoreCorrelationIdProvider"/>类型的实例
        /// </summary>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        /// <param name="options">跟踪关联ID选项</param>
        public AspNetCoreCorrelationIdProvider(IHttpContextAccessor httpContextAccessor
            , IOptions<CorrelationIdOptions> options)
        {
            HttpContextAccessor = httpContextAccessor;
            Options = options.Value;
        }

        /// <summary>
        /// 获取跟踪关联ID
        /// </summary>
        public virtual string Get()
        {
            if (HttpContextAccessor.HttpContext?.Request?.Headers == null)
                return CreateNewCorrelationId();
            var correlationId = HttpContextAccessor.HttpContext.Request.Headers[Options.HttpHeaderName];
            if (correlationId.IsEmpty())
            {
                lock (HttpContextAccessor.HttpContext.Request.Headers)
                {
                    if (correlationId.IsEmpty())
                    {
                        correlationId = CreateNewCorrelationId();
                        HttpContextAccessor.HttpContext.Request.Headers[Options.HttpHeaderName] = correlationId;
                    }
                }
            }

            return correlationId;
        }

        /// <summary>
        /// 创建新跟踪关联ID
        /// </summary>
        protected virtual string CreateNewCorrelationId() => Guid.NewGuid().ToString("N");
    }
}
