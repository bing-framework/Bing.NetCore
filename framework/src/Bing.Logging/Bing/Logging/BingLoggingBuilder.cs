using Microsoft.Extensions.DependencyInjection;

namespace Bing.Logging
{
    /// <summary>
    /// 日志构建器
    /// </summary>
    public sealed class BingLoggingBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="BingLoggingBuilder"/>类型的实例
        /// </summary>
        /// <param name="services">服务集合</param>
        public BingLoggingBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// 服务集合
        /// </summary>
        public IServiceCollection Services { get; }
    }
}
