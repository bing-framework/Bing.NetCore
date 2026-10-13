# 本轮生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法 | 证据边界 |
| --- | --- | --- | --- |
| `CacheBatchAdaptive.Read`：超预算递归拆分、输入顺序、非预算异常不重试 | `Bing.Caching.Tests` | `CacheBatchAdaptiveTest.Read_SplitsPayloadOversizeAndPreservesOrder`；`Read_DoesNotRetryNonPayloadFailures` | 模拟读取函数；不替代真实 Redis 负载测试 |
| `CacheBatchAdaptive.ReadAsync`：单值超限和取消 | `Bing.Caching.Tests` | `CacheBatchAdaptiveTest.ReadAsync_SingleOversizeValueFailsWithoutRetry`；`ReadAsync_CancellationStopsBeforeRetry` | 模拟异步读取函数；真实客户端取消仍需集成门禁 |
| FreeRedis/CSRedis/EasyCaching Redis/Hybrid 同步、异步、Cluster、分片批量读取接入自适应拆分 | 对应 Provider 集成测试 | `BatchContractTest`、`ClusterCacheTest`、`ShardedCacheTest`、`RedisCoordinatorIntegrationTest`、流式取消测试 | `RUN_INTEGRATION_TESTS` 开启且连接非生产 Redis 时执行；本轮未以门控关闭的跳过冒充通过 |
| Hybrid 回填代次闸门 | `Bing.EasyCaching.Tests` | `HybridRefillGenerationTest.AdvanceRejectsStaleSynchronousAndAsynchronousRefills`；`AsyncRefillHoldsGenerationGateWhileApplying` | 进程内闸门单元证据；双实例总线竞态仍需隔离 Redis |
