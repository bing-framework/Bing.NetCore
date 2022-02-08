using System.Threading.Tasks;

namespace Bing.Aspects
{
    /// <summary>
    /// 拦截器
    /// </summary>
    public interface IBingInterceptor
    {
        /// <summary>
        /// 拦截
        /// </summary>
        /// <param name="invocation">方法调用</param>
        Task InterceptAsync(IBingMethodInvocation invocation);
    }
}
