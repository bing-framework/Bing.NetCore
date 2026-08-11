namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 最终实体映射缓存的聚合统计快照。
/// </summary>
/// <remarks>
/// 统计只包含实例级数值，不包含数据源、租户、映射配置或物理对象名称。
/// 并发访问时各字段为近似聚合读数，不保证来自同一时刻。
/// </remarks>
internal readonly struct EntityMappingCacheStatistics
{
    /// <summary>
    /// 初始化一个<see cref="EntityMappingCacheStatistics"/>类型的实例。
    /// </summary>
    /// <param name="cacheHitCount">首次缓存查找命中次数。</param>
    /// <param name="cacheMissCount">首次缓存查找未命中次数。</param>
    /// <param name="cacheBypassCount">因容量策略未写入缓存的次数。</param>
    /// <param name="entryCount">当前缓存条目数。</param>
    /// <param name="capacity">固定缓存容量；<see langword="null"/> 表示无上限。</param>
    internal EntityMappingCacheStatistics(long cacheHitCount, long cacheMissCount, long cacheBypassCount,
        int entryCount, int? capacity)
    {
        CacheHitCount = cacheHitCount;
        CacheMissCount = cacheMissCount;
        CacheBypassCount = cacheBypassCount;
        EntryCount = entryCount;
        Capacity = capacity;
    }

    /// <summary>
    /// 获取首次缓存查找命中次数。
    /// </summary>
    public long CacheHitCount { get; }

    /// <summary>
    /// 获取首次缓存查找未命中次数。
    /// </summary>
    public long CacheMissCount { get; }

    /// <summary>
    /// 获取因容量策略未写入缓存的次数。
    /// </summary>
    public long CacheBypassCount { get; }

    /// <summary>
    /// 获取当前最终实体映射缓存条目数。
    /// </summary>
    public int EntryCount { get; }

    /// <summary>
    /// 获取解析器创建时固定的缓存容量；<see langword="null"/> 表示无上限。
    /// </summary>
    public int? Capacity { get; }
}