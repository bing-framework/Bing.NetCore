# BING-CACHE-TODO-20261007-092：EasyCaching Cluster 扫描与节点故障重叠门禁

## 目标

关闭当前唯一 Redis Cluster 故障组合缺口：EasyCaching Redis 的前缀流式扫描已经从上游主节点收到完整 SCAN 响应但响应仍被代理挂起时，重启该主节点，确认调用不会把截断/未完成扫描当作成功；故障恢复后重建 Manager 仍能读取并清理命名空间。

## 契约

- 测试只在 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_CLUSTER_SCAN_RESTART_TESTS=true`、`BING_TEST_ALLOW_REDIS_RESTART=true` 且容器名符合 `bing-cache-cluster-092-` 前缀时运行。
- 使用任务专用 Redis 7.4 三主三从 Cluster，端口 7000-7005、总线端口 17000-17005；只重启显式指定的 7000 主节点容器。
- 通过 `RedisScanResponseGate` 暂存已从目标 Redis 读取的完整 SCAN 响应，确认扫描 Task 尚未完成后重启目标容器并断言 Redis `run_id` 变化。
- 重启后释放/中止挂起响应，扫描必须传播异常且不得返回完整成功；观察到的条目数必须小于完整数据集。
- 恢复三个主节点公告端口、等待 Cluster 完整拓扑，再用重建 Manager 验证前缀 List/Stream、跨槽批量读和 `ClearAsync`；相似命名空间哨兵必须保留。
- 本轮不修改生产代码，不把测试闸门的连接中止解释为业务事务回滚或严格一次执行保证。

## 实现范围

- 新增 `EasyClusterScanRestartIntegrationTest`，复用现有 RESP 扫描闸门并增加 Docker 重启、run_id、公告恢复和重建 Manager 断言。
- 在一次性三主三从 Redis 7.4 Cluster 上运行 net6.0/net8.0 精准测试并保存故障窗口证据。
- 更新权威缓存 TODO 索引；不修改生产缓存代码、公共接口、键格式或注册方式。

## Change Impact Analysis

- ChangedFiles：`framework/tests/Bing.EasyCaching.Tests/EasyClusterScanRestartIntegrationTest.cs`、092 任务记录和 `ai_docs/cache-todo-index.md`。
- ChangedProjects：`Bing.EasyCaching.Tests`；生产项目仅作为测试依赖构建。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis Cluster 前缀流式扫描、Redis SCAN 响应闸门、主节点重启和重建恢复。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务证据和权威索引。
- AffectedDependents：EasyCaching Cluster 集成测试、RedisScanResponseGate 和任务专用 Redis 容器。
- RiskLevel：MEDIUM；测试临时修改三主公告端口并重启显式前缀容器，finally 必须恢复配置并清理资源。
