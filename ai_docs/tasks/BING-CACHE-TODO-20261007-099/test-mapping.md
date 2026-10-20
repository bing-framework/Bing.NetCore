# 生产符号到测试映射

| 生产符号/行为 | 测试项目与方法 | 本轮证据 |
| --- | --- | --- |
| `IHybridInvalidationDeadLetterAdmin.ReplayAsync(string, ...)` 和 `ConfirmDeleteAsync(string, ...)` | `Bing.EasyCaching.Tests.HybridInvalidationDeliveryTest.DeadLetterAdmin_HandleAndTokenSurviveJsonRoundTripAcrossDeliveryInstances` | net6.0/net8.0 通过；不同 delivery 实例只使用 JSON 往返后的字符串 handle/token |
| handle 格式校验、授权顺序与 payload 读取边界 | `Bing.EasyCaching.Tests.HybridInvalidationDeliveryTest.DeadLetterAdmin_RejectsMalformedHandleBeforeAuthorizationOrRedisRead`、`DeadLetterAdmin_UnauthorizedHandleDoesNotReadRedisPayload` | net6.0/net8.0 通过 |
| DTO 兼容入口、错误确认 token、损坏消息处理和审计 | `Bing.EasyCaching.Tests.HybridInvalidationDeliveryTest.DeadLetterAdmin_ListsReplaysAndAuditsOpaqueEntry`、`DeadLetterAdmin_RejectsWrongConfirmationAndMalformedReplay` | net6.0/net8.0 通过 |
| 历史 payload member 惰性迁移及字节预算保持 | `Bing.EasyCaching.Tests.RedisHybridInvalidationOutboxIntegrationTest.Outbox_MigratesLegacyPayloadMemberToHandleWithoutChangingByteBudget` | 临时 Redis 7.4 实测；net6.0/net8.0 通过 |
| 滚动并存时重复 member 去重、迁移后读取去重与 payload 字节预算修正 | `Bing.EasyCaching.Tests.RedisHybridInvalidationOutboxIntegrationTest.Outbox_DeduplicatesLegacyPayloadAndHandleWithoutInflatingByteBudget` | 临时 Redis 7.4 实测；修复前 1 项失败，修复后 outbox 门禁 net6.0/net8.0 各 11/11 通过 |
| handle 重放、确认删除和字节计数 | `Bing.EasyCaching.Tests.RedisHybridInvalidationOutboxIntegrationTest.Outbox_AdminReplayAndDeleteMaintainPayloadBudget`、`Outbox_AdminConcurrentReplayAndDeleteHaveSingleWinner` | 临时 Redis 7.4 实测；作为 outbox 专项的一部分，net6.0/net8.0 均通过 |
| Cluster 同槽 Lua、旧格式迁移、重放/删除与隔离哨兵 | `Bing.EasyCaching.Tests.EasyClusterIntegrationTest.RedisCluster_DeadLetterHandlesMigrateReplayAndDeleteInOneSlot` | 临时三主三从 Redis Cluster 实测；net6.0/net8.0 各通过 |
