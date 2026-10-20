namespace Bing.Caching;

/// <summary>
/// 缓存实例的不可变配置。
/// </summary>
public sealed class CacheConfiguration
{
    /// <summary>
    /// 初始化一个 <see cref="CacheConfiguration"/> 类型的实例。
    /// </summary>
    /// <param name="nameSpace">应用或租户使用的可信逻辑命名空间。</param>
    /// <param name="providerName">缓存提供器的注册名称。</param>
    /// <param name="defaultExpiration">未指定单次过期时间时使用的默认时长。</param>
    /// <param name="batchSize">批量命令和扫描页的最大条目数。</param>
    /// <param name="maxConcurrency">批量操作允许的最大在途并发数。</param>
    /// <param name="coordination">缓存未命中时使用的回源协调参数；为 <see langword="null"/> 时使用默认值。</param>
    /// <param name="topology">Redis 节点拓扑类型。</param>
    /// <param name="expectedShardCount">客户端分片拓扑预期包含的节点数。</param>
    /// <param name="resources">回源和响应资源限制；为 <see langword="null"/> 时使用默认值。</param>
    public CacheConfiguration(string nameSpace, string providerName, TimeSpan? defaultExpiration = null,
        int batchSize = 500, int maxConcurrency = 8, CacheCoordinationOptions coordination = null,
        CacheTopology topology = CacheTopology.SingleNode, int? expectedShardCount = null,
        CacheResourceOptions resources = null)
    : this(nameSpace, providerName, defaultExpiration, batchSize, maxConcurrency, coordination, topology,
            expectedShardCount, resources, CacheInputLimits.Unlimited, CacheSyncCleanupMode.Sequential)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheConfiguration"/> 类型的实例。
    /// </summary>
    /// <param name="nameSpace">应用或租户使用的可信逻辑命名空间。</param>
    /// <param name="providerName">缓存提供器的注册名称。</param>
    /// <param name="defaultExpiration">未指定单次过期时间时使用的默认时长。</param>
    /// <param name="batchSize">批量命令和扫描页的最大条目数。</param>
    /// <param name="maxConcurrency">批量操作允许的最大在途并发数。</param>
    /// <param name="coordination">缓存未命中时使用的回源协调参数。</param>
    /// <param name="topology">Redis 节点拓扑类型。</param>
    /// <param name="expectedShardCount">客户端分片拓扑预期包含的节点数。</param>
    /// <param name="resources">回源和响应资源限制。</param>
    /// <param name="inputLimits">逻辑输入长度限制。</param>
    /// <param name="syncCleanupMode">客户端分片同步清理的调度模式。</param>
    /// <remarks>此重载用于创建保留全部配置选项的副本。</remarks>
    private CacheConfiguration(string nameSpace, string providerName, TimeSpan? defaultExpiration,
        int batchSize, int maxConcurrency, CacheCoordinationOptions coordination, CacheTopology topology,
        int? expectedShardCount, CacheResourceOptions resources, CacheInputLimits inputLimits,
        CacheSyncCleanupMode syncCleanupMode)
    {
        if (string.IsNullOrWhiteSpace(nameSpace))
            throw new ArgumentException("缓存命名空间不能为空。", nameof(nameSpace));
        CacheInputValidator.ValidateWellFormedUtf16(nameSpace, nameof(nameSpace), "缓存命名空间");
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("缓存提供器名称不能为空。", nameof(providerName));
        CacheInputValidator.ValidateWellFormedUtf16(providerName, nameof(providerName), "缓存提供器名称");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        if (topology == CacheTopology.ClientSharded && (!expectedShardCount.HasValue || expectedShardCount <= 0))
            throw new ArgumentOutOfRangeException(nameof(expectedShardCount), "客户端分片必须配置预期分片数。");
        if (topology != CacheTopology.ClientSharded && expectedShardCount.HasValue)
            throw new ArgumentException("仅客户端分片拓扑可以配置预期分片数。", nameof(expectedShardCount));
        if (!Enum.IsDefined(typeof(CacheSyncCleanupMode), syncCleanupMode))
            throw new ArgumentOutOfRangeException(nameof(syncCleanupMode));

        Namespace = nameSpace;
        ProviderName = providerName;
        PhysicalKeyPrefix = "bing:v2:" + Uri.EscapeDataString(nameSpace) + ":" + Uri.EscapeDataString(providerName) + ":";
        HashInputPrefix = nameSpace + ":" + providerName + ":";
        DefaultExpiration = NormalizeExpiration(defaultExpiration ?? TimeSpan.FromHours(8));
        BatchSize = batchSize;
        MaxConcurrency = maxConcurrency;
        Coordination = coordination ?? new CacheCoordinationOptions();
        Topology = topology;
        ExpectedShardCount = expectedShardCount;
        Resources = resources ?? new CacheResourceOptions();
        InputLimits = inputLimits ?? CacheInputLimits.Unlimited;
        SyncCleanupMode = syncCleanupMode;
    }

    /// <summary>
    /// 获取应用或租户使用的可信逻辑命名空间。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 获取缓存提供器的注册名称。
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// 获取未显式指定单次过期时间时使用的默认时长。
    /// </summary>
    public TimeSpan DefaultExpiration { get; }

    /// <summary>
    /// 获取批量命令和扫描页的最大条目数。
    /// </summary>
    public int BatchSize { get; }

    /// <summary>
    /// 获取批量操作允许的最大在途并发数。
    /// </summary>
    public int MaxConcurrency { get; }

    /// <summary>
    /// 获取缓存未命中时使用的回源协调参数。
    /// </summary>
    public CacheCoordinationOptions Coordination { get; }

    /// <summary>
    /// 获取回源准入和单批原始值资源限制。
    /// </summary>
    /// <remarks>
    /// 多个配置实例可以共享同一对象以共用回源准入上限。
    /// </remarks>
    public CacheResourceOptions Resources { get; }

    /// <summary>
    /// 获取逻辑键、前缀和模式的可选长度限制。
    /// </summary>
    public CacheInputLimits InputLimits { get; }

    // 不可变派生值在实例创建时生成，避免每次扫描验证重复拼接和转义。
    /// <summary>
    /// 获取用于拼接物理缓存键的命名空间前缀。
    /// </summary>
    internal string PhysicalKeyPrefix { get; }

    /// <summary>
    /// 获取用于计算逻辑键哈希的输入前缀。
    /// </summary>
    internal string HashInputPrefix { get; }

    /// <summary>
    /// 获取 Redis 节点拓扑类型。
    /// </summary>
    public CacheTopology Topology { get; }

    /// <summary>
    /// 获取客户端分片的预期节点数。
    /// </summary>
    /// <remarks>
    /// 扫描时使用该数量检查节点清单是否完整。
    /// 未配置时返回 <see langword="null"/>。
    /// </remarks>
    public int? ExpectedShardCount { get; }

    /// <summary>
    /// 获取同步清理调度模式。
    /// </summary>
    /// <remarks>只有选择 <see cref="CacheSyncCleanupMode.BoundedParallel"/> 时才会并发调用同步 Provider。</remarks>
    public CacheSyncCleanupMode SyncCleanupMode { get; }

    /// <summary>
    /// 创建仅替换输入限制的新配置实例。
    /// </summary>
    /// <param name="inputLimits">新的输入限制；不能为 <see langword="null"/>。</param>
    /// <returns>保留其他配置的新实例。</returns>
    public CacheConfiguration WithInputLimits(CacheInputLimits inputLimits)
    {
        if (inputLimits == null)
            throw new ArgumentNullException(nameof(inputLimits));
        return new CacheConfiguration(Namespace, ProviderName, DefaultExpiration, BatchSize, MaxConcurrency,
            Coordination, Topology, ExpectedShardCount, Resources, inputLimits, SyncCleanupMode);
    }

    /// <summary>
    /// 创建仅替换同步清理调度模式的新配置实例。
    /// </summary>
    /// <param name="syncCleanupMode">新的同步清理调度模式。</param>
    /// <returns>保留其他配置的新实例。</returns>
    public CacheConfiguration WithSyncCleanupMode(CacheSyncCleanupMode syncCleanupMode)
    {
        if (!Enum.IsDefined(typeof(CacheSyncCleanupMode), syncCleanupMode))
            throw new ArgumentOutOfRangeException(nameof(syncCleanupMode));
        return new CacheConfiguration(Namespace, ProviderName, DefaultExpiration, BatchSize, MaxConcurrency,
            Coordination, Topology, ExpectedShardCount, Resources, InputLimits, syncCleanupMode);
    }

    /// <summary>
    /// 获取本次写入采用的有效期。
    /// </summary>
    /// <param name="options">本次写入的缓存选项；为 <see langword="null"/> 时使用默认有效期。</param>
    /// <returns>大于零且向上取整到毫秒后的有效期。</returns>
    public TimeSpan GetExpiration(CacheOptions options) =>
        NormalizeExpiration(options?.Expiration ?? DefaultExpiration);

    /// <summary>
    /// 校验并将有效期向上取整到毫秒。
    /// </summary>
    /// <param name="expiration">待规范化的有效期。</param>
    /// <returns>规范化后的有效期。</returns>
    private static TimeSpan NormalizeExpiration(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "缓存有效期必须大于零。");
        var milliseconds = Math.Ceiling(expiration.TotalMilliseconds);
        if (milliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(expiration), "缓存有效期超出 Redis 毫秒范围。");
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
