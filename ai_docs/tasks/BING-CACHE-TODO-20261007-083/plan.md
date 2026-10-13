# BING-CACHE-TODO-20261007-083：EasyCaching 扫描竞态与槽位变化门禁

## 目标

继续补齐 EasyCaching P4/P8 的故障窗口证据：运行既有的 Cluster 槽位归属变化测试和单节点 SCAN 响应丢失测试，证明流式扫描不会把拓扑变化或已执行但响应丢失伪装成完整成功。

## Change Impact Analysis

- ChangedFiles：本任务计划、TODO、执行报告、测试映射和权威索引；不修改生产源码或既有测试逻辑。
- ChangedProjects：`Bing.EasyCaching.Tests` 集成门禁。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis 流式 SCAN 的拓扑校验和底层响应异常传播。
- ChangedProviders：EasyCaching Redis；Hybrid 不在本轮扫描故障门禁范围内。
- ChangedTFMs：测试 `net6.0`、`net8.0`。
- RiskLevel：MEDIUM。测试会临时修改专用 Redis Cluster 的一个空槽归属，必须在 finally 中恢复。

## 实施步骤

### P1：单节点响应丢失

- 启动隔离 Redis 7.4 单节点，设置 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_SCAN_GATE_TESTS=true` 和 `ConnectionStrings__RedisConnection`。
- 运行 `EasyScanInFlightFailureTest.RedisScan_ResponseLostAfterExecution_PropagatesFailure`，验证代理挂起完整 SCAN 响应后断开连接，流式调用返回异常而不宣称成功。

### P2：Cluster 槽位归属变化

- 创建任务专用三主 Redis 7.4 Cluster，公告地址和 7000-7002 端口与既有 EasyCaching 测试契约一致。
- 设置 `RUN_CACHE_CLUSTER_TESTS=true`、`RUN_CACHE_CLUSTER_SLOT_MIGRATION_TESTS=true`，运行 `RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected`。
- 验证测试 finally 恢复空槽原归属并删除随机命名空间数据。

### P3：收口

- 运行 net6/net8，记录通过/跳过和异常类型。
- 清理所有临时容器、网络和代理连接，确认已有服务不受影响。
- 更新权威索引：关闭 EasyCaching 单节点响应丢失和 Cluster 槽位变化证据；Cluster 扫描在途故障因公告契约不匹配继续列为外部边界。

## 非目标

- 不运行 10k/100k 性能矩阵。
- 不把单节点故障证据替代 Cluster 扫描在途故障、节点重启或独立总线故障证据。
- 不修改故障注入测试以绕过现有环境契约。

## Definition of Done

- Redis 响应丢失门禁 net6/net8 通过。
- Cluster 槽位变化门禁 net6/net8 通过并恢复原槽位。
- 临时资源清理完成，任务文档和索引状态一致。
