using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// Lazy延迟加载解析器
/// </summary>
/// <typeparam name="T">对象类型</typeparam>
internal class Lazier<T> : Lazy<T> where T : class
{
    /// <summary>
    /// 初始化一个<see cref="Lazier{T}"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public Lazier(IServiceProvider serviceProvider) : base(serviceProvider.GetRequiredService<T>)
    {
    }
}
