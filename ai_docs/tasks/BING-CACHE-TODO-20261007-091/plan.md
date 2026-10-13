# BING-CACHE-TODO-20261007-091：EasyCaching Redis Cluster 主节点重启门禁

## 目标

补充 EasyCaching Redis Cluster 主节点容器重启后的恢复证据。090 已证明副本 `FAILOVER TAKEOVER` 后重建 Manager 能重新发现完整拓扑，本轮验证节点进程重启、run_id 变化、槽位恢复以及重建 Manager 的跨槽读写和命名空间清理。

## 契约

- 测试只在 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_CLUSTER_RESTART_TESTS=true`、`BING_TEST_ALLOW_REDIS_RESTART=true` 且显式指定 `BING_TEST_CLUSTER_RESTART_CONTAINER` 时运行。
- 容器名必须以 `bing-cache-cluster-091-` 开头，目标端口只允许 7000、7001、7002，防止误操作共享服务。
- 集群为任务专用 Redis 7.4 三主三从，覆盖 16384 个槽位；重启前后均必须断言 3 主 3 从和完整槽位。
- 使用 Docker 重启目标主节点，必须观察 Redis `run_id` 变化；不能只根据客户端重连或命令成功推断已重启。
- 使用重建的 EasyCaching Redis Manager 验证跨槽批量读取、前缀列表/流式读取和 `ClearAsync`；随机命名空间外的哨兵键必须保留。
- 本轮不覆盖扫描与节点故障同时发生的在途组合，不把本轮结果扩大解释为完整 Cluster 故障矩阵。

## 实现范围

- 新增 `EasyClusterRestartIntegrationTest` 及显式门控属性、Docker 重启和拓扑/run_id 辅助方法。
- 在一次性三主三从 Redis 7.4 Cluster 上运行 net6.0/net8.0 精准测试并保存拓扑、run_id、重建 Manager 和清理证据。
- 更新权威缓存 TODO 索引；不修改生产缓存代码、公共接口、键格式或注册方式。

## Change Impact Analysis

- ChangedFiles：`framework/tests/Bing.EasyCaching.Tests/EasyClusterRestartIntegrationTest.cs`、091 任务记录和 `ai_docs/cache-todo-index.md`。
- ChangedProjects：`Bing.EasyCaching.Tests`；生产项目仅作为测试依赖构建。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis Cluster 节点重启后的拓扑重新发现、跨槽批量/扫描读取和命名空间清理。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务证据和权威索引。
- AffectedDependents：EasyCaching Redis Cluster 集成测试和任务专用 Redis 容器。
- RiskLevel：LOW；仅操作显式前缀的可丢弃容器和随机命名空间。
