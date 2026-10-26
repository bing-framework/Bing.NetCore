namespace Bing.Caching;

/// <summary>
/// 按显式名称选择混合缓存实例的提供程序契约。
/// </summary>
public interface IHybridCacheProvider
{
    /// <summary>
    /// 获取指定名称的混合缓存实例。
    /// </summary>
    /// <param name="name">注册时指定的缓存实例名称。</param>
    /// <returns>与名称对应的混合缓存门面。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> 为 <see langword="null" /> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">名称未注册时抛出，不回退到默认实例。</exception>
    IHybridCache GetCache(string name);
}
