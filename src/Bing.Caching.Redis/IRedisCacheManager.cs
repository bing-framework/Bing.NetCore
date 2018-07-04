using System;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis缓存管理器
    /// </summary>
    public interface IRedisCacheManager:IHybridCacheProvider,IDisposable
    {
    }
}
