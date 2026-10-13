# BING-CACHE-TODO-20261007-086：修正 EasyCaching Cluster SCAN 故障门禁公告配置

## 目标

修正 `EasyScanInFlightFailureTest.RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure` 的测试拓扑配置。当前测试只修改 7000 节点的 `cluster-announce-port`，却等待 7001/7002 公告同一代理端口，导致门禁无法按自身契约启动。本任务让三个主节点都临时公告 SCAN 代理端口，并按节点保留/恢复各自总线公告端口。

## 实现范围

- 修改 `framework/tests/Bing.EasyCaching.Tests/EasyScanInFlightFailureTest.cs` 和 `framework/tests/Bing.Test.Shared/RedisScanResponseGate.cs` 的测试装置。
- 启动三个主节点的 `IServer` 句柄，读取并断言原始客户端公告端口 `7000/7001/7002`。
- 代理启动后将三个主节点的客户端公告端口统一设置为代理端口；等待每个节点的 `CLUSTER NODES` 公告包含对应代理端口和原始总线端口。
- finally 中恢复三个节点的原始客户端公告端口；清理逻辑保留原异常和清理异常。
- 使用任务专用 Redis 7.4 三主 Cluster，运行 net6.0/net8.0 门禁；不修改生产源码。

## 验收

- Cluster SCAN 响应已被代理截获时流式操作不会提前完成。
- 中断代理持有连接后，流式操作显式传播非超时异常。
- 测试结束后三个节点的公告端口恢复为原值，Cluster 槽位完整。
- 既有 Cluster 正常门禁不受影响，任务文档和权威索引更新。

## Change Impact Analysis

- ChangedFiles：Cluster SCAN 故障测试装置、共享 RESP 代理、086 任务文档和权威索引；生产源码不变。
- ChangedProjects：`Bing.EasyCaching.Tests`、`Bing.Test.Shared`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Cluster SCAN 流式读取故障传播测试。
- ChangedProviders：EasyCaching Redis Cluster 测试路径。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务报告、测试映射和权威索引。
- AffectedDependents：无生产消费者。
- RiskLevel：MEDIUM；测试会临时修改任务专用 Cluster 节点公告配置。
