using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing
{
    /// <summary>
    /// 服务提供程序访问器(<see cref="IServiceProviderAccessor"/>) 扩展
    /// </summary>
    public static class ServiceProviderAccessorExtensions
    {
        /// <summary>
        /// 获取Http上下文
        /// </summary>
        /// <param name="serviceProviderAccessor">服务提供程序访问器</param>
        public static HttpContext GetHttpContext(this IServiceProviderAccessor serviceProviderAccessor) => serviceProviderAccessor.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
    }
}
