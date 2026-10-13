# BING-CACHE-TODO-20261007-095 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `HybridInvalidationDeliveryOptions.CloneValidated` | dead-letter 两个配置必须同时为零或同时为正值 | `HybridInvalidationDeliveryTest.Options_RequirePairedDeadLetterSettings` | 通过：net6.0/net8.0 |
| `RedisHybridInvalidationOutbox.TryTake` / `TryTakeAsync` | 启用 dead-letter 时持久化递增投递次数；默认关闭不创建 attempts 结构 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_PersistsAttemptsAndMovesToBoundedDeadLetter`、`Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild` | 通过：真实 Redis，net6.0/net8.0 |
| `RedisHybridInvalidationOutbox.TryDeadLetter` / `TryDeadLetterAsync` | 同槽 Lua 原子转移 processing 到有界 dead-letter，并清理 attempts；容量满时保留 processing | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_PersistsAttemptsAndMovesToBoundedDeadLetter` | 通过：真实 Redis，net6.0/net8.0 |
| `HybridInvalidationDelivery.DrainLoopAsync` | 达到阈值后隔离并继续 drain；失败转移不确认消息 | `HybridInvalidationDeliveryTest.ProcessingMessage_ReachesLimitAndMovesToDeadLetter` | 通过：net6.0/net8.0 |
| `HybridInvalidationDeliverySnapshot` | 暴露共享 dead-letter 数量和当前进程隔离计数 | `HybridInvalidationDeliveryTest.Snapshot_WithoutOutbox_ReturnsStableEmptyQueue`、`ProcessingMessage_ReachesLimitAndMovesToDeadLetter` | 通过：net6.0/net8.0 |
