using Bing.Caching;

using EasyCaching.Core;
using System.Runtime.CompilerServices;

namespace Bing.EasyCaching;

/// <summary>
/// EasyCaching 内存缓存适配器。
/// </summary>
public class MemoryCacheManager : CacheManager, ILocalCache
{
    /// <summary>
    /// 初始化一个 <see cref="MemoryCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="factory">EasyCaching Provider 工厂。</param>
    /// <param name="configuration">缓存实例配置。</param>
    public MemoryCacheManager(IEasyCachingProviderFactory factory, CacheConfiguration configuration)
        : this(factory?.GetCachingProvider(configuration?.ProviderName), configuration)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="MemoryCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="provider">EasyCaching 内存 Provider。</param>
    /// <param name="configuration">缓存实例配置。</param>
    private MemoryCacheManager(IEasyCachingProvider provider, CacheConfiguration configuration)
        : base(provider, configuration, CreateKeyStream(provider))
    {
    }

    /// <summary>
    /// 创建内存 Provider 的键扫描委托。
    /// </summary>
    /// <param name="provider">提供键枚举能力的内存 Provider。</param>
    /// <returns>按前缀枚举键的异步委托；Provider 为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
    private static Func<string, CancellationToken, IAsyncEnumerable<string>> CreateKeyStream(
        IEasyCachingProvider provider)
    {
        if (provider == null)
            return null;
        return Enumerate;

        async IAsyncEnumerable<string> Enumerate(string prefix,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var key in provider.GetAllKeysByPrefix(prefix))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }
        }
    }
}
