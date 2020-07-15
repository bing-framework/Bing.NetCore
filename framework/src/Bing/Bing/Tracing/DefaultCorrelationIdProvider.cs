using System;
using Bing.DependencyInjection;

namespace Bing.Tracing
{
    /// <summary>
    /// 默认跟踪标识提供程序
    /// </summary>
    internal class DefaultCorrelationIdProvider : ICorrelationIdProvider, ISingletonDependency
    {
        /// <summary>
        /// 跟踪标识提供程序
        /// </summary>
        public string Get() => CreateNewCorrelationId();

        /// <summary>
        /// 创建新跟踪标识
        /// </summary>
        protected virtual string CreateNewCorrelationId() => Guid.NewGuid().ToString("N");
    }
}
