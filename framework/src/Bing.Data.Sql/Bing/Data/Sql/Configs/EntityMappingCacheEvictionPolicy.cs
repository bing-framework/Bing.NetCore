namespace Bing.Data.Sql.Configs;

/// <summary>
/// 实体最终映射缓存达到容量后的处理策略。
/// </summary>
public enum EntityMappingCacheEvictionPolicy
{
    /// <summary>
    /// 保留已缓存的稳定路由，新路由不写入缓存。
    /// </summary>
    AdmissionOnly = 0,

    /// <summary>
    /// 淘汰最近最少使用的路由并缓存新路由。
    /// </summary>
    LeastRecentlyUsed = 1
}