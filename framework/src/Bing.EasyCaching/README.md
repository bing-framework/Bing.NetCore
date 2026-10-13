# Bing.EasyCaching
[![NuGet](https://img.shields.io/nuget/v/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

EasyCaching 1.9.2 的 Bing 缓存适配器，支持内存、Redis 和 Hybrid Provider。

## 注册

每个实例必须使用可信的命名空间和独立的 Provider 名称。内存模式：

```csharp
var cacheConfig = new CacheConfiguration("admin", "admin-memory");
services.AddMemoryCaching(cacheConfig);
```

Redis 模式：

```csharp
var cacheConfig = new CacheConfiguration("admin", "admin-redis");
services.AddRedisCaching(cacheConfig, redis =>
{
    redis.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379));
});
```

Hybrid 模式需要独立的 Redis 数据 Provider 和 Redis 总线配置：

```csharp
var cacheConfig = new CacheConfiguration("admin", "admin-hybrid");
services.AddHybridCaching(cacheConfig, "admin-hybrid-redis",
    redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379)),
    bus => bus.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse("localhost:6379"));
```

Hybrid 管理器由 `AddHybridCaching` 或 `AddShardedHybridCaching` 统一装配，以保证 Hybrid Provider 与管理器共用失效总线跟踪器。

失效发布默认只执行一次，保持现有调用语义。需要有限重试和 Redis outbox 时，在注册入口显式启用；`MaxPendingMessages` 同时限制 pending 与 processing 消息数量：

```csharp
services.AddHybridCaching(cacheConfig, "admin-hybrid-redis",
    redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379)),
    bus => bus.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse("localhost:6379"),
    configureInvalidation: options =>
    {
        options.MaxPublishAttempts = 3;
        options.RetryDelay = TimeSpan.FromMilliseconds(100);
        options.MaxPendingMessages = 1000;
        options.MaxDeliveryAttempts = 5;
        options.DeadLetterMaxMessages = 100;
        options.DeadLetterMaxBytes = 1024 * 1024;
        options.DeadLetterRetention = TimeSpan.FromDays(7);
        options.DeadLetterCleanupInterval = TimeSpan.FromMinutes(1);
        options.DeadLetterCleanupBatchSize = 100;
    });
```

启用 outbox 要求分布式 Redis Provider 暴露 `StackExchange.Redis.IDatabase`。当前缓存写入仍会在最终发布失败时抛出原始总线异常，即使消息已经入队；后台投递器随后继续处理 pending/processing 列表。`MaxDeliveryAttempts` 与 `DeadLetterMaxMessages` 必须同时设置为正值，才会启用跨进程持久化投递次数和有界 dead-letter；`DeadLetterMaxBytes` 可选限制所有 dead-letter JSON envelope 的 UTF-8 payload 字节总和，零表示关闭。达到消息数或字节预算后只有原子转移成功才会从 processing 移除，预算不足或转移失败时消息继续保留在 processing。启用字节预算时，旧 dead-letter member 缺少辅助计数会在首次状态/转移/清理操作中按实际 payload 字节自动重算。`DeadLetterRetention` 默认是零，只有显式设置正保留期才自动清理，以 dead-letter 转移时间为准，按 `DeadLetterCleanupBatchSize` 分批运行；`DeadLetterMaxBytes` 只计算 payload 字节，不等同于 Redis allocator 的实际进程内存。该机制提供有界排队和至少一次投递，进程在发布与确认之间退出时允许重复失效，不提供严格一次、跨键事务或全局顺序保证。

可从 DI 读取 `IHybridInvalidationDeliveryDiagnostics` 获取只读快照。快照包含 pending/processing/dead-letter 深度、dead-letter payload 字节总数、最早消息年龄、当前进程的成功发布/失败尝试/入队/隔离/过期清理计数和最近失败文本；队列字段和 payload 字节总数跨进程共享，计数器在进程重启后清零。状态读取不是事务快照，队列在并发变化时各字段可能来自相邻时刻。客户端分片会为每个主题注册一个诊断实例，使用 `IEnumerable<IHybridInvalidationDeliveryDiagnostics>` 按主题区分。

需要人工处置 dead-letter 时，宿主必须显式注册 `IHybridInvalidationDeadLetterAuthorizer` 和 `IHybridInvalidationDeadLetterAuditSink`，然后从 DI 读取 `IHybridInvalidationDeadLetterAdmin`；未同时注册两者时管理调用会在访问 Redis 前失败。`GetDeadLettersAsync` 按最早转移时间返回有界条目，条目提供 payload 摘要形式的 opaque handle 和确认 token；调用方可以只保存这两个字符串并在后续请求中调用 `ReplayAsync(handle, actor)` 或 `ConfirmDeleteAsync(handle, token, actor)`，旧的 DTO 重载仍可用于进程内调用。`ReplayAsync` 会受 pending/processing 总容量限制并原子回到 pending，`ConfirmDeleteAsync` 必须回传匹配的 confirmation token。授权器负责身份和权限判断，审计接收器负责持久化 actor、topic、动作、结果和影响数量；审计失败不会回滚已经完成的 Redis 状态。客户端分片按主题注册多个管理实例，应通过 `IEnumerable<IHybridInvalidationDeadLetterAdmin>` 选择目标主题。Handle 和 token 不包含原始缓存键，不应写入普通日志。

新版本使用 SHA-256 handle 作为 dead-letter Sorted Set member，并将 JSON envelope 单独存入同 hash slot 的 payload hash。历史版本直接存放 JSON member 的记录会在有界 `GetDeadLettersAsync` 读取时惰性迁移，保留 dead-letter 时间分数；迁移不会扫描其他缓存命名空间，也不要求停机。payload 字节预算仍按 envelope 的 UTF-8 字节数计算，不包含 Redis 索引、hash 与 allocator 开销。

客户端分片按固定顺序配置独立 Redis 节点，`ExpectedShardCount` 必须与节点数一致：

```csharp
var shards = new Action<RedisOptions>[]
{
    redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint("redis-a", 6379)),
    redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint("redis-b", 6379))
};
var cacheConfig = new CacheConfiguration("admin", "admin-sharded",
    topology: CacheTopology.ClientSharded, expectedShardCount: shards.Length);
services.AddShardedRedisCaching(cacheConfig, shards);
// Hybrid 使用相同的 shards，并为每个分片注册独立的失效总线。
// services.AddShardedHybridCaching(cacheConfig, shards,
//     bus => bus.ConfigurationOptions = ConfigurationOptions.Parse("redis-bus:6379"));
```

注册方法分别提供 `ILocalCache` 或 `IRedisCache`，并在尚未注册时提供 `ICache`。多个同类实例可以采用不同的 Provider 名称；需要按名称选择实例时，可直接使用 EasyCaching 的 Provider Factory。Redis `DBConfig.KeyPrefix` 必须为空，适配器统一负责键隔离。

可按实例启用输入边界，客户端分片的内部管理器会复制该配置：

```csharp
cacheConfig = cacheConfig.WithInputLimits(new CacheInputLimits(
    maxLogicalKeyLength: 256,
    maxPrefixLength: 128,
    maxPatternLength: 256));
```

限制只作用于逻辑键、前缀和模式长度，默认值为 0（不限制）；超限请求在本地失败，不会访问 Redis。

内存模式只支持 `SingleNode`。Redis 和 Hybrid 模式支持 `SingleNode` 与 `RedisCluster`；Cluster 批量操作按 Redis 槽位拆分，扫描遍历当前全部在线主节点并在扫描前后校验完整拓扑。客户端分片使用专用的 `AddShardedRedisCaching` 或 `AddShardedHybridCaching` 入口，按物理键哈希标签路由，并在单键及批量操作完成前复验节点身份；节点顺序变化或重启后需重建管理器，不会自动迁移旧数据。Hybrid 为每个分片创建独立总线实例和失效主题，以保证跨实例本地层失效。Cluster 配置应向 EasyCaching Redis Provider 提供可发现全部节点的连接端点，且 Redis 总线也应连接同一个 Cluster。

客户端分片同步 `RemoveByPrefix`、`RemoveByPattern` 和 `Clear` 默认按分片顺序执行。需要并发调用同步 Provider 时，可在 `CacheConfiguration` 中显式设置 `SyncCleanupMode = CacheSyncCleanupMode.BoundedParallel`；适配器按 `MaxConcurrency` 分窗，完整观察失败窗口后才进入下一窗口，并按分片索引传播首个异常。该模式不提供跨分片事务性，调用方应确认 Provider 连接池和同步调用支持并发；需要取消时使用对应异步 API。

## 行为

- 物理键使用 `bing:v2:<namespace>:` 前缀；升级后旧键不自动回读或删除，首次读取会回源。
- `Set` 与 `SetAll` 覆盖已有值；`TrySet` 使用上游 Provider 的条件写入。
- `Clear` 只删除当前命名空间的缓存键，不执行 Redis `FLUSHDB`。前缀按字面量匹配；模式支持 `*`、`?` 和反斜线转义，均只匹配逻辑键。
- Hybrid 写入、删除和命名空间清理均经过 Hybrid Provider，使本地层及 Redis 总线参与失效。Hybrid 是最终一致缓存；适配器会报告总线发布失败，包括 EasyCaching Provider 内部吞掉的批量发布异常。失败发生时 Redis 写入可能已经完成，调用方不得把异常解释为事务回滚。
- 内存回源在同一进程内按物理键合并。Redis 和 Hybrid 回源先在本地同键合并，再使用随机令牌的 Redis 租约协调跨进程请求；数据写回由同槽脚本校验租约所有权。同键递归回源会报错，等待与回源受 `CacheConfiguration.Coordination` 的时间参数约束。回源函数仍应幂等，不保证故障转移时严格执行一次。
- 批量操作按 `CacheConfiguration.BatchSize` 分批，异步读取最多同时派发 `MaxConcurrency` 个请求。Cluster 的批量请求再按槽位分组，同槽内使用 Provider 的批量命令；Redis/Hybrid 键枚举使用主节点分页 `SCAN`，边扫描边处理；内存 Provider 通过 `MemoryCacheManager` 的键流适配逐项消费 `GetAllKeysByPrefix`。自定义非 Redis Provider 可实现 `Bing.Caching.ICacheKeyPageStreamProvider` 提供带页边界的异步键流，使 `MaxScanPages` 和页级探针生效；未实现时继续按优先级使用 `ICacheKeyStreamProvider` 的逐键流，最后回退到 `GetAllKeysByPrefixAsync` 的完整键集合接口。返回列表的 API 仍需容纳最终结果。
- 删除批次在 `BatchSize` 之外还受 `CacheResourceOptions.MaxBatchCommandBytes` 限制，按数据键和 `:lease` 键参数的 UTF-8 字节数拆分；Cluster 先按槽位分组，客户端分片先按节点分组。Redis/Hybrid 协调路径也按该预算批量删除，Hybrid 每批删除本地层并发布一条包含该批键的失效消息。该预算不包括 Redis 协议帧，单个键对超限时操作直接失败。
- Hybrid 失效消息可单独设置 `CacheResourceOptions.MaxInvalidationMessageBytes`；适配器按实际 EasyCaching serializer 载荷顺序拆分批量失效通知，单个物理键超限时 fail fast。默认值 `0` 保持单条消息行为。需要消息重试和持久化排队时使用注册入口的 `HybridInvalidationDeliveryOptions`；outbox 只在 `MaxPendingMessages > 0` 时启用，并按上述至少一次语义运行。
- outbox 的诊断快照只读取列表/死信集合长度和队尾样本，不完整收集消息；旧版本没有入队时间的 envelope 仍可投递，但其年龄返回 null。死信只保存原始 envelope；自动清理只在配置正保留期时按批次执行。人工管理仍不提供跨进程全局计数、Redis allocator 实际内存预算或跨 Redis 写入与失效入队的事务保证。启用死信前应为消息数容量、保留期、授权器、审计持久化和人工处置建立运维策略。
- 设置 `CacheResourceOptions.MaxScanKeys` 后，扫描、前缀流和命名空间清理在达到候选键上限时抛出 `CacheScanLimitExceededException`；异常提供 `Limit` 与 `Consumed`，客户端分片还提供已完成的 `CompletedShards` 和产生部分结果的 `FailedShard`。调用方应按可能已经产生部分结果处理。默认值 `0` 不限制候选键数量。
- `CacheResourceOptions.MaxScanPages` 由能够暴露原生 `SCAN` 页边界的 FreeRedis/CSRedis/EasyCaching Redis 适配器，以及实现 `ICacheKeyPageStreamProvider` 的自定义 EasyCaching Provider 执行。旧的逐键流和 `GetAllKeysByPrefixAsync` 完整集合回退仍无法观察真实页数，`ScanPages` 返回 -1，并继续使用 `MaxScanKeys` 保护已接受候选。页流 Provider 在收到一页后计数并记录；达到上限时不消费该页中的键。

Redis/Hybrid 的原子写回使用对应 Provider 暴露的 `IDatabase` 与序列化器。自定义 EasyCaching Provider 若不提供兼容的 StackExchange.Redis 数据库，会在构造管理器时失败。
