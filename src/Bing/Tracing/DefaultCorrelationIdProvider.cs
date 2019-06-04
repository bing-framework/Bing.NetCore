using System;

namespace Bing.Tracing
{
    /// <summary>
    /// 默认跟踪关联ID提供程序
    /// </summary>
    public class DefaultCorrelationIdProvider : ICorrelationIdProvider
    {
        /// <summary>
        /// 跟踪关联ID提供程序
        /// </summary>
        public string Get() => CreateNewCorrelationId();

        /// <summary>
        /// 创建新跟踪关联ID
        /// </summary>
        /// <returns></returns>
        protected virtual string CreateNewCorrelationId() => Guid.NewGuid().ToString("N");
    }
}
