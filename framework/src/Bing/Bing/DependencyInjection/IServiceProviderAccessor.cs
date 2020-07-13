using System;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 服务提供程序访问器
    /// </summary>
    public interface IServiceProviderAccessor
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}
