# BING-CACHE-TODO-20261007-094 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `IHybridInvalidationDeliveryDiagnostics` / `HybridInvalidationDelivery.GetSnapshot` | 未启用 outbox 时返回稳定零值快照 | `HybridInvalidationDeliveryTest.Snapshot_WithoutOutbox_ReturnsStableEmptyQueue` | 通过：net6.0/net8.0 |
| `HybridInvalidationDelivery.GetSnapshotAsync` | 失败尝试、入队计数、最近失败文本和队列年龄可读取 | `HybridInvalidationDeliveryTest.Snapshot_ReportsFailureCountersAndQueueAge` | 通过：net6.0/net8.0 |
| `RedisHybridInvalidationOutbox.GetStatus` / `GetStatusAsync` | pending/processing 深度与最早 envelope 时间，读取不改变队列 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild` | 通过：真实 Redis，net6.0/net8.0，各 1/1 |
| `RedisHybridInvalidationOutbox.Deserialize` | 没有入队时间字段的旧 envelope 仍可读取 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_ReadsEnvelopeWithoutEnqueueTime` | 通过：真实 Redis，net6.0/net8.0，各 1/1 |
| `CachingServiceCollectionExtensions` | 单实例及分片 Hybrid 注册诊断服务，分片按主题保持一一对应 | `EasyShardedHybridIntegrationTest.ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge`、`CacheManagerTests`；生产 netstandard2.0 构建 | 通过实现与编译；真实分片断言受 Redis 集成门控 |
