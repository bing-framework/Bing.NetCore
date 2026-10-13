# BING-CACHE-TODO-20261007-097 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法 |
| --- | --- | --- |
| `HybridInvalidationDeliveryOptions.DeadLetterMaxBytes` 范围和 dead-letter 依赖 | `Bing.EasyCaching.Tests` | `HybridInvalidationDeliveryTest.Options_RequireDeadLetterWhenByteBudgetIsEnabled`、`Options_RejectInvalidDeadLetterByteBudget` |
| 未启用字节预算时诊断保持 null | `Bing.EasyCaching.Tests` | `HybridInvalidationDeliveryTest.Snapshot_WithoutOutbox_ReturnsStableEmptyQueue` |
| `HybridInvalidationDelivery.CreateSnapshot` 映射字节总数 | `Bing.EasyCaching.Tests` | `HybridInvalidationDeliveryTest.Snapshot_ReportsFailureCountersAndQueueAge` |
| `RedisHybridInvalidationOutbox` 精确 payload 边界和 processing 保留 | `Bing.EasyCaching.Tests` + 非生产 Redis | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_EnforcesExactDeadLetterPayloadBytesAndRebuildsLegacySizes` |
| 历史 `:sizes`/`:bytes` 缺失时重算 | `Bing.EasyCaching.Tests` + 非生产 Redis | 同上，删除辅助键后断言 `DeadLetterBytes` 恢复 |
| 清理 Lua 同步递减和归零 | `Bing.EasyCaching.Tests` + 非生产 Redis | 同上，清理后断言 `DeadLetterBytes == 0` |
| 多 writer 原子预算 | `Bing.EasyCaching.Tests` + 非生产 Redis | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_CompetingDeadLetterWritersRespectByteBudgetAtomically` |
| 默认消息数/保留期/旧 envelope 回归 | `Bing.EasyCaching.Tests` + 非生产 Redis | 既有 `RedisHybridInvalidationOutboxIntegrationTest` 测试集合 |

## 验证环境

- 精准单元：net6.0/net8.0，各 14 passed。
- 真实 Redis outbox：net6.0/net8.0，各 6 passed、0 skipped；环境为显式指定的非生产 `127.0.0.1:6379`。
- EasyCaching 完整回归：net6.0/net8.0，各 122 passed、64 skipped。
- 生产构建：`Bing.EasyCaching` netstandard2.0，0 errors。
