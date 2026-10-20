namespace Bing.Caching;

/// <summary>
/// 客户端分片同步清理的调度模式。
/// </summary>
public enum CacheSyncCleanupMode
{
    /// <summary>
    /// 按分片索引顺序执行同步清理。
    /// </summary>
    Sequential,

    /// <summary>
    /// 按配置的并发上限分窗执行同步清理。
    /// </summary>
    /// <remarks>并发上限由 <see cref="CacheConfiguration.MaxConcurrency"/> 指定。</remarks>
    BoundedParallel
}
