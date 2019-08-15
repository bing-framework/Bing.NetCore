using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dependency
{
    /// <summary>
    /// 默认服务作用域工厂
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    public class DefaultServiceScopeFactory : IHybridServiceScopeFactory
    {
        /// <summary>
        /// 服务作用域工厂
        /// </summary>
        protected IServiceScopeFactory ServiceScopeFactory { get; }

        /// <summary>
        /// 初始化一个<see cref="DefaultServiceProviderFactory"/>类型的实例
        /// </summary>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        public DefaultServiceScopeFactory(IServiceScopeFactory serviceScopeFactory)
        {
            ServiceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// 创建作用域
        /// </summary>
        public IServiceScope CreateScope() => ServiceScopeFactory.CreateScope();
    }
}
