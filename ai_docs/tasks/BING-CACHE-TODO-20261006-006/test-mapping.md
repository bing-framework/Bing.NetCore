# 本轮生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 证据边界 |
| --- | --- | --- |
| FreeRedis/CSRedis 流式首批取消停止派发 | 各 Provider `CommandCancellationTest` | 真实单节点 Redis `127.0.0.1:6379`；net6.0/net8.0 各 1/1；不覆盖 Cluster |
| Redis 租约与回源提交 | `Bing.Caching.ProcessProbe` normal/crash-holder/timeout-holder/lost-lease/waiter-timeout | 独立进程、单节点 Redis、随机命名空间；三个 Provider 全场景通过 |
| FreeRedis/CSRedis 进程内协调边界 | 两个 Provider `CoordinationBoundaryTest` 四个方法 | 真实单节点 Redis；net6.0/net8.0 各 4/4 |
| EasyCaching Redis/Hybrid 协调与显式变更 | `RedisCoordinatorIntegrationTest` 全类 19 个方法 | `RUN_INTEGRATION_TESTS=true`、`BING_TEST_EASYCACHING_REDIS=127.0.0.1:6379`；net6.0/net8.0 各 19/19；独立总线故障拓扑仍未覆盖 |
| Hybrid 本地回填代次闸门 | `HybridRefillGenerationTest` | 进程内单元测试；不替代跨实例总线竞态 |
