# BING-CACHE-TODO-20261007-082：EasyCaching 三主三从 Cluster 门禁

## 目标

补齐缓存计划 P4/P8 中 EasyCaching Redis/Hybrid 的多主 Cluster 实机证据。079 已验证 FreeRedis/CSRedis，但 EasyCaching 的 `EasyClusterIntegrationTest` 仍因 Cluster 门控跳过；本轮使用任务专用 Redis 7.4 三主三从拓扑运行既有测试，不修改生产实现。

## Change Impact Analysis

- ChangedFiles：本任务计划、TODO、执行报告、测试映射和权威 TODO 索引；不修改生产源码或现有测试逻辑。
- ChangedProjects：`Bing.EasyCaching.Tests` 集成门禁。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid 在 Redis Cluster 中的跨槽批量读写、全主节点 SCAN、Clear、Hybrid 本地层和失效总线。
- ChangedProviders：EasyCaching Redis、EasyCaching Hybrid。
- ChangedTFMs：测试 `net6.0`、`net8.0`；生产代码不变。
- RiskLevel：LOW。只使用隔离容器和随机命名空间，测试结束精确清理。

## 实施步骤

### P1：隔离 Cluster

- 创建六个 Redis 7.4 容器，7000-7002 为主节点，7003-7005 为副本，使用任务专用 Docker 网络和宿主机映射端口。
- 完成槽位分配并用 `CLUSTER CHECK` 验证 16384 个槽位覆盖；不触碰工作区已有 `redis:6379`。

### P2：EasyCaching 门禁

- 设置 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_CLUSTER_TESTS=true`。
- 运行 `EasyClusterIntegrationTest` 的小规模 Redis 与 Hybrid 跨槽批量、流式读取、全主节点扫描、删除、Clear 和命名空间哨兵测试。
- 运行同一测试的 `net6.0`、`net8.0`；大规模 10k/100k、槽迁移和故障注入保持门控跳过，不用小规模结果替代这些证据。

### P3：证据收口

- 记录每个 TFM 的通过/跳过数量、Cluster 健康检查和清理结果。
- 将权威索引中 Cluster 项补充 EasyCaching 覆盖；严格三方性能、双进程回源和独立总线故障仍保持原状态。
- 更新生产符号到测试方法映射，运行 `git diff --check`，不提交/推送/发布。

## 非目标

- 不开启 10,000/100,000 项性能矩阵，不把小规模 Cluster 结果当作性能预算结论。
- 不执行槽迁移、主节点重启或总线故障注入；这些已有独立门控和外部条件。
- 不修改 EasyCaching Cluster 实现；若门禁暴露代码缺陷，再另立修复计划。

## Definition of Done

- EasyCaching Redis 与 Hybrid 的小规模 Cluster 测试在 net6/net8 均通过。
- 结果明确证明跨槽批量、全主节点扫描、Clear 和命名空间哨兵隔离。
- 任务容器、网络和测试数据清理完成，原有服务未受影响。
- 权威索引、TODO、execution 和 test-mapping 一致。
