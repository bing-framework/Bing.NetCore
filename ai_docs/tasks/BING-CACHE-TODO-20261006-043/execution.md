# BING-CACHE-TODO-20261006-043 执行记录

## 初始状态

`ShardedRedisCacheManager.GetAllAsync` 和 `GetByPrefixAsync` 仍通过 Stream API 收集 List 结果，List 路径额外创建 `CacheEntry<T>` 并保留逻辑键/Hit 状态。本轮改为直接值批处理，并复用单分片扫描预算。

## Round 1

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs`、对应测试和本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：客户端分片异步批量 List、异步前缀 List、单分片异步前缀 List。
- ChangedProviders：EasyCaching Redis 客户端分片及其单分片管理器。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：客户端分片批量读取、前缀读取和扫描取消路径。
- RiskLevel：HIGH。

### 已完成

- `ShardedRedisCacheManager.GetAllAsync<T>` 改为按输入批次调用 `ReadAsyncValuesBatch<T>`，各分片使用 `GetAllAsync<T>` 直接返回值列表；分片并发上限、身份前后校验、取消和失败观察保持不变。
- `ShardedRedisCacheManager.GetByPrefixAsync<T>` 改为逐分片调用 `CacheManager.GetByPrefixWithBudgetAsync<T>`，共享同一个 `CacheScanBudget`，不再收集 `CacheEntry<T>`。
- `CacheManager.GetByPrefixAsync<T>` 复用新增的带预算内部实现，公共行为和 Stream 实现保持独立。
- 新增 `GetAllAsync_ReturnsValuesFromAllShardsInInputOrder` 和 `GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget`，覆盖分片 List 结果与身份校验。

### 验证

- 精准测试 `ShardedScanCancellationUnitTest`：net8.0 `8 passed / 0 skipped / 0 failed`。
- 精准测试 `ShardedScanCancellationUnitTest`：net6.0 `8 passed / 0 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net8.0：`69 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net6.0：`69 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release：`0 warning / 0 error`。
- `git diff --check`：无错误；测试仍有既有离线 NuGet/近似版本警告。

### 未完成与状态

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：正式 P95/分配预算没有同构历史 baseline，本轮不宣称性能预算通过。
- 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵、历史 baseline 和真实资源指标为 `BLOCKED_EXTERNAL`。
- 100,000 项完整性能矩阵、网络往返、连接池排队和独立 `review.md` 仍为 `DEFERRED`。
