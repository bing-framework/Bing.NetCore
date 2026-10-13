# 生产符号到测试方法映射

本轮只修改审计文档，没有修改生产代码。下表记录当前能力、已有证据和仍需补充的测试方法，供下一轮实现直接使用。

| 生产符号/能力 | 已有测试或证据 | 当前缺口 |
| --- | --- | --- |
| `GetAllAsync<T>`、`GetAllStreamAsync<T>` | `framework/tests/CacheContractAssertions.cs`；FreeRedis/CSRedis `BatchContractTest.cs`；EasyCaching `CacheManagerTests.cs` | 大规模 List/Stream 对照；消费一批后取消不派发下一批 |
| `GetByPrefixAsync<T>`、`GetByPrefixStreamAsync<T>` | FreeRedis/CSRedis `CacheSpecialKeyTest.cs`；EasyCaching `RedisCoordinatorIntegrationTest.cs`；任务 057 实机测试 | 三 Provider 特殊键前缀 Stream；中途取消；大规模 List/Stream 对照 |
| FreeRedis/CSRedis 批量扫描和删除 | 两个 Provider 的 `ClusterFailureTest.cs`、`ShardedCacheTest.cs`、任务 048/051/056 | Stream API 在第一批后节点失败时的异常传播 |
| `CacheManager.Hybrid.FillHybridBatch` | `framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs`；任务 058 net6/net8 实机回归 | Hybrid `SetAll/GetAll/GetAllStream/RemoveAll` 跨本地/Redis 实机一致性 |
| 自适应批量响应拆分 | `framework/tests/CacheCoordinationAssertions.cs` 的 `VerifyAdaptivePayloadSplittingAsync`；任务 058 三 Provider 集成测试 | Cluster/分片拓扑下的外部验证 |
| 删除取消响应闸门 | `framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs`；`Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs`；任务 056 | Cluster/分片故障环境 |
| 键格式、命名空间和拓扑解析 | `framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs`；`CacheClusterTopologyTest.cs`；任务 052-054 | 依赖外部拓扑的实机故障转移 |
| 跨进程回源协调 | 任务 001 ProcessProbe；任务 004 Provider 内协调回归 | T04 后候选的双进程重新回归及持有者故障环境 |
| 性能预算与探针 | `ai_docs/tasks/BING-CACHE-TODO-20261005-004/performance.md` 及其 `performance-matrix` | 预算失败属于边界；网络/客户端队列和严格三方 baseline 外部阻塞或延后 |

## 验证方法

- 本轮 L0：UTF-8 读取、引用路径存在性和 Markdown 结构检查。
- 本轮 L1-L5：不重复代码测试；复用任务 056-058 已落盘的 net6/net8、netstandard2.0 和单节点 Redis 证据。
- 下一轮首先处理五项 `OPEN_ACTIONABLE` 测试，测试完成后回写本文件和仓库级索引。
