# BING-CACHE-TODO-20261007-096 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `HybridInvalidationDeliveryOptions.CloneValidated` | 保留期必须在范围内，且只有 dead-letter 启用时才允许为正 | `HybridInvalidationDeliveryTest.Options_RequireDeadLetterWhenRetentionIsEnabled` | 通过：net6.0/net8.0 |
| `RedisHybridInvalidationOutbox.CleanupExpiredDeadLetters` / `CleanupExpiredDeadLettersAsync` | 按 score 和批大小原子清理过期成员，未过期成员保留 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_CleansExpiredDeadLettersInBoundedBatches` | 通过：真实 Redis，net6.0/net8.0，各 1/1 |
| `RedisHybridInvalidationOutbox.GetStatus` / `GetStatusAsync` | 报告 dead-letter 数量和最早转移时间 | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_CleansExpiredDeadLettersInBoundedBatches` | 通过：真实 Redis，net6.0/net8.0 |
| `HybridInvalidationDelivery.CleanupExpiredDeadLettersAsync` | 以配置间隔调度清理，批大小透传，清理失败保留 processing 并记录失败 | `HybridInvalidationDeliveryTest.DeadLetterRetention_CleansInBoundedBatchesAndReportsCount`、`DeadLetterCleanupFailure_PreservesProcessingAndReportsFailure` | 通过：net6.0/net8.0 |
| `HybridInvalidationDeliverySnapshot` | 暴露最早死信年龄和过期清理计数 | `HybridInvalidationDeliveryTest.Snapshot_WithoutOutbox_ReturnsStableEmptyQueue`、`DeadLetterRetention_CleansInBoundedBatchesAndReportsCount` | 通过：net6.0/net8.0 |
