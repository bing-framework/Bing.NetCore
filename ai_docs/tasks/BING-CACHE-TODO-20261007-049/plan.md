# 扫描删除取消证据补齐计划

## 目标

为 FreeRedis 和 CSRedis 的异步扫描删除补充真实 Redis `MONITOR` 证据，确认首个删除批次完成后取消不会继续派发后续 `DEL`。本轮只补回归测试和生产符号映射，不改变公共 API、键格式、扫描算法或删除协议。

## 现状与缺口

- `RemoveByPrefixAsync`、`RemoveByPatternAsync` 和 `ClearAsync` 已在扫描循环和批量删除前检查 `CancellationToken`。
- task 048 已将 FreeRedis/CSRedis 扫描删除改为按批次、客户端或 Cluster 槽位分组，并保留取消语义。
- task 048 的扫描删除测试覆盖删除结果、租约键和命名空间哨兵，但没有在首批 `DEL` 后取消并观察实际 Redis 命令数。
- 两个 Provider 已有 `RedisCommandMonitor` 共享测试工具，且流式读取已有同类“首批后取消”证据，可复用其门控和屏障模式。

## 实施范围

### P1：FreeRedis 取消回归

- 新增门控测试，使用 `batchSize=1` 和至少四个匹配键。
- 通过 `MONITOR` 等待当前命名空间的首个批量 `DEL`，然后取消令牌。
- 等待 `OperationCanceledException`，发送唯一 `PING` 作为排空屏障，断言该操作只产生一个命名空间删除命令。
- 清理残留数据，不把清理命令计入断言。

### P2：CSRedis 取消回归

- 使用同样的测试结构和命名空间隔离断言。
- 复用 CSRedis 连接的实际路由客户端，确保观察到服务端命令而非仅输入枚举次数。

### P3：证据与验证

- 更新 `TODO.md`、`execution.md` 和 `test-mapping.md`，区分本地编译证据与 Redis 门控实机证据。
- 运行受影响集成测试的 net6.0/net8.0 精准筛选；门控关闭时记录 Skip，不伪造实机通过。
- 构建 FreeRedis/CSRedis `netstandard2.0`，执行 `git diff --check`。

## 非目标

- 不修改生产扫描、批量删除、Cluster/分片路由或取消实现。
- 不新增性能基准，不重跑既有批量读/扫描性能实验。
- 不在没有 Redis 环境时用单元测试替代服务端命令证据。

## Change Impact Analysis

- ChangedFiles：两套扫描删除集成测试及本任务文档。
- ChangedProjects：`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：仅验证已有异步前缀扫描删除的取消路径。
- ChangedProviders：FreeRedis、CSRedis 测试范围；生产 Provider 不变。
- ChangedTFMs：测试 net6.0/net8.0；生产构建 netstandard2.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：扫描删除调用方和 Redis 集成门控。
- RiskLevel：LOW（测试与文档变更；实机测试受外部环境门控）。

## 验收标准

1. 两套测试在 Redis 门控开启时能观察到首批 `DEL`，取消后不再出现同一操作的第二个命名空间 `DEL`。
2. 测试覆盖取消异常、命名空间过滤和残留数据清理，不依赖时间延迟猜测。
3. net6.0/net8.0 测试项目可编译，Redis 未开启时明确 Skip；生产项目 netstandard2.0 构建通过。
4. 任务文档明确 `OPEN_ACTIONABLE`、`BLOCKED_EXTERNAL` 和 `DEFERRED` 状态，不把门控 Skip 记为实机通过。
