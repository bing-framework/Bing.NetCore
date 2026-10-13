# BING-CACHE-TODO-20261007-084 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `HybridBusFailureTracker` / Hybrid 订阅器 | 独立总线订阅断开后重连并清理可能错过的本地值 | `EasyHybridBusReconnectIntegrationTest.SubscriptionReconnect_ClearsMissedUpdateAndDeleteWithoutRepublish` | 待运行 |
| `HybridInvalidationMessageBatcher` / Hybrid 发布路径 | 独立总线暂停时显式报告发布失败，数据端提交保持可见，恢复后继续失效 | `EasyHybridBusReconnectIntegrationTest.HybridBus_PublishDuringPause_ReportsFailureAndRecovers` | 待运行 |
| `HybridBusFailureTracker` / 两个 Hybrid 实例 | 总线订阅连接断开后重建，恢复发布并使对端本地层失效 | `EasyHybridBusReconnectIntegrationTest.HybridBus_SubscriptionDisconnect_ReconnectsAndInvalidatesPeer` | 待运行 |
| Hybrid 订阅与发布组合 | 订阅重连与总线暂停后恢复发布、跨实例失效收敛 | `EasyHybridBusReconnectIntegrationTest.HybridBus_SubscriptionAndPauseFailures_ReconnectThenRepublishAndInvalidatePeer` | 待运行 |
| FreeRedis/CSRedis/易缓存 Redis 回源协调 | 双进程同键合并、租约续期、失租拒写和等待超时 | `BING-CACHE-TODO-20261006-034` `Bing.Caching.ProcessProbe` 矩阵 | 已有证据，复用 |
