# BING-CACHE-TODO-20261007-093 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `HybridInvalidationDeliveryOptions.CloneValidated` | 重试次数、延迟、队列上限和轮询参数边界 | `HybridInvalidationDeliveryTest.Publish_RetriesThenEnqueuesAndPropagatesOriginalFailure`、`Publish_WhenOutboxIsFull_PropagatesFailureWithoutSilentSuccess` | 通过：net6.0/net8.0 精准子集 |
| `HybridInvalidationDelivery.Publish` / `PublishAsync` | 有限重试；恢复时不入队；失败时保留原始异常；预取消不派发或入队 | `HybridInvalidationDeliveryTest.Publish_RetriesThenEnqueuesAndPropagatesOriginalFailure`、`Publish_WhenTransientFailureRecovers_DoesNotEnqueue`、`PublishAsync_WhenCanceledBeforeDispatch_DoesNotEnqueue` | 通过：net6.0/net8.0，各 5/5 精准子集 |
| `RedisHybridInvalidationOutbox.TryEnqueue` / `TryTake` / `Acknowledge` | pending/processing 容量、FIFO 顺序、确认删除和重建恢复 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild` | 通过：真实 Redis，net6.0/net8.0，各 1/1 |
| `HybridInvalidationDelivery.DrainLoopAsync` | processing 消息在总线恢复后重试并确认；取消不删除未确认项 | `HybridInvalidationDeliveryTest.ProcessingMessage_IsRetriedAndAcknowledgedAfterBusRecovery` | 通过：net6.0/net8.0 |
| `CachingServiceCollectionExtensions.AddHybridCaching` / `AddShardedHybridCaching` | 每个实例注册单例投递器，默认兼容且 opt-in 使用 Redis outbox | `CacheManagerTests`、`EasyShardedHybridIntegrationTest` 相关注册回归，以及生产 netstandard2.0 构建 | 通过；受影响 EasyCaching 精准集合各 55 通过/1 跳过 |
| `HybridCacheManager` / `ShardedRedisCacheManager` 失效回调 | 单键、批量和异步失效统一经过投递器 | `HybridInvalidationDeliveryTest`、Hybrid 现有精准回归 | 通过：net6.0/net8.0 |
