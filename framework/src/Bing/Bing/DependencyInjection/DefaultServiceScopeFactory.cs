using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 默认<see cref="IServiceScope"/>工厂，行为和<see cref="IServiceScopeFactory"/>一样
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    public class DefaultServiceScopeFactory : IHybridServiceScopeFactory
    {
        /// <summary>
        /// 服务作用域工厂
        /// </summary>
        protected IServiceScopeFactory ServiceScopeFactory { get; }

        /// <summary>
        /// 初始化一个<see cref="DefaultServiceScopeFactory"/>类型的实例
        /// </summary>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        public DefaultServiceScopeFactory(IServiceScopeFactory serviceScopeFactory) => ServiceScopeFactory = serviceScopeFactory;

        /// <summary>
        /// 创建作用域
        /// </summary>
        public IServiceScope CreateScope() => ServiceScopeFactory.CreateScope();
    }
}
