namespace Bing.Caching;

/// <summary>
/// 指定缓存实例使用的 Redis 路由拓扑。
/// </summary>
public enum CacheTopology
{
    /// <summary>
    /// 使用单个 Redis 主节点。
    /// </summary>
    SingleNode,
    /// <summary>
    /// 使用按槽位路由的 Redis Cluster。
    /// </summary>
    RedisCluster,
    /// <summary>
    /// 使用由客户端按键路由的独立分片。
    /// </summary>
    ClientSharded
}
