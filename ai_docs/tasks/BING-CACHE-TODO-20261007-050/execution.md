<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-TODO-20261007-050
AI_EXECUTION_FINISHED_AT: 2026-10-07T03:08:00+08:00

# 实施执行报告

## Change Impact Analysis

- ChangedFiles：FreeRedis/CSRedis `RemoveAll` 实现、两套命令聚合集成测试及本任务文档。
- ChangedProjects：`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` 及对应集成测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：同步/异步显式键删除。
- ChangedProviders：FreeRedis、CSRedis。
- ChangedTFMs：测试 net6.0/net8.0；生产 netstandard2.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：调用 `RemoveAll`/`RemoveAllAsync` 的缓存清理路径。
- RiskLevel：MEDIUM。

## 实施内容

- FreeRedis 同步和异步 `RemoveAll` 按 `BatchSize` 收集物理键，复用 `RemovePhysicalBatch`/`RemovePhysicalBatchAsync`。
- CSRedis 同步和异步 `RemoveAll` 按 `BatchSize` 收集逻辑键，复用 `RemoveLogicalBatch`/`RemoveLogicalBatchAsync`，继续由物理层按客户端或 Cluster 槽位分组。
- 异步路径在每个批次前检查取消；已派发批次由现有批量删除路径等待和观察，不回退成逐键无界任务。
- 新增 FreeRedis/CSRedis 门控测试：5 个键、`BatchSize=2`，验证当前命名空间产生 3 个 `DEL`，数据键与租约键均删除，兄弟命名空间哨兵保留。

## 验证结果

### L1：精准测试

- FreeRedis net8.0：0 passed / 1 skipped / 0 failed；项目编译成功。
- FreeRedis net6.0：0 passed / 1 skipped / 0 failed；项目编译成功。
- CSRedis net8.0：0 passed / 1 skipped / 0 failed；项目编译成功。
- CSRedis net6.0：0 passed / 1 skipped / 0 failed；项目编译成功。

Skip 原因均为 Redis 集成门控未开启；本轮没有把服务端 MONITOR 结果记为通过。

### L0：生产构建与格式

- FreeRedis `netstandard2.0` Release 构建：0 warning / 0 error。
- CSRedis `netstandard2.0` Release 构建：0 warning / 0 error。
- `git diff --check`：通过；仅保留工作区既有 CRLF 转换提示。

## TODO 分类

### Completed

- FreeRedis `RemoveAll`/`RemoveAllAsync` 有界批量删除。
- CSRedis `RemoveAll`/`RemoveAllAsync` 有界批量删除。
- 租约键、命名空间哨兵、批次命令数回归测试和生产符号映射。

### Open Actionable

- 无。本轮没有仍可在当前工作区完成的同范围实现项。

### Blocked External

- Redis 实机门控未开启，尚未验证两个 Provider 在真实服务端上分别产生 3 个 `DEL`；需要非生产 Redis 端点并设置 `RUN_INTEGRATION_TESTS=true`。

### Deferred

- 删除命令按 UTF-8 字节预算拆分、网络往返和客户端内部排队观测不在本轮范围；沿用缓存性能 TODO。

## 计划偏差

无。未修改公共 API、物理键格式、租约协议或性能预算；未提交、推送或发布。
