using Bing.Caching;
using EasyCaching.Core.Interceptor;

namespace Bing.EasyCaching;

/// <summary>
/// 缓存键生成器
/// </summary>
public class CacheKeyGenerator : ICacheKeyGenerator
{
    /// <summary>
    /// 生成 EasyCaching 缓存键的底层生成器。
    /// </summary>
    private readonly IEasyCachingKeyGenerator _keyGenerator;

    /// <summary>
    /// 初始化一个 <see cref="CacheKeyGenerator"/> 类型的实例。
    /// </summary>
    /// <param name="keyGenerator">EasyCaching缓存键生成器</param>
    public CacheKeyGenerator(IEasyCachingKeyGenerator keyGenerator)
    {
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
    }

    /// <inheritdoc />
    public string CreateCacheKey(MethodInfo methodInfo, object[] args, string prefix)
    {
        return _keyGenerator.GetCacheKey(methodInfo, args, prefix);
    }
}
