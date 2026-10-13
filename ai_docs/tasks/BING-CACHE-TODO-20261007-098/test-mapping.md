# 098 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法或证据 |
| --- | --- | --- |
| `IHybridInvalidationDeadLetterAdmin` 授权与审计边界 | `Bing.EasyCaching.Tests` | `HybridInvalidationDeliveryTest.DeadLetterAdmin_RequiresAuthorizationAndAuditBeforeReading`、`AuthorizationFailureIsAudited`、`PreCanceledRequestDoesNotAuthorizeOrRead` |
| dead-letter 条目解析、malformed 和 token | `Bing.EasyCaching.Tests` | `HybridInvalidationDeliveryTest.DeadLetterAdmin_ListsReplaysAndAuditsOpaqueEntry`、`RejectsWrongConfirmationAndMalformedReplay` |
| Redis 原子重放、容量限制、删除幂等 | `Bing.EasyCaching.Tests` | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_AdminReplayAndDeleteMaintainPayloadBudget`、`Outbox_AdminReplayLeavesDeadLetterWhenQueueIsFull` |
| Redis 并发管理单赢家 | `Bing.EasyCaching.Tests` | `RedisHybridInvalidationOutboxIntegrationTest.Outbox_AdminConcurrentReplayAndDeleteHaveSingleWinner` |
| 097 dead-letter 字节预算/历史重算回归 | `Bing.EasyCaching.Tests` | `Outbox_EnforcesExactDeadLetterPayloadBytesAndRebuildsLegacySizes`、`Outbox_CompetingDeadLetterWritersRespectByteBudgetAtomically` |
| 单节点/分片 DI 注册 | `Bing.EasyCaching.Tests` | `CacheManagerTests.HybridRegistration_ExposesDeadLetterAdminAlongsideDiagnostics`、`ShardedHybridRegistration_ExposesOneDeadLetterAdminPerTopic` |
| 全部 EasyCaching 调用链 | `Bing.EasyCaching.Tests` | net6.0/net8.0 完整回归 |
| 生产构建 | `Bing.EasyCaching` | `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --no-restore` |
