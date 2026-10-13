# BING-CACHE-TODO-20261006-042 执行记录

## 初始状态

EasyCaching `GetByPrefixAsync` 当前通过 `GetByPrefixStreamAsync` 收集结果，List 调用方不需要逻辑键和 Hit 状态。本任务将扫描过滤与命中值读取拆开，保留 Stream 路径的完整语义。

## Round 1（2026-10-06）

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`、本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid/Cluster/Provider-key-stream 异步前缀 List 读取。
- ChangedProviders：EasyCaching Redis、Hybrid、Redis Cluster 和内存适配器键流。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；不重复既有数组容器和键格式实验。
- AffectedDependents：EasyCaching `GetByPrefixAsync`、Hybrid 批量回填、命名空间扫描和取消测试。
- RiskLevel：HIGH。

### 已完成

- `GetByPrefixAsync<T>` 现在遍历 `EnumeratePhysicalPrefixKeysAsync`，使用 `TryMatchLogicalPrefix` 直接校验物理前缀，避免为 List 路径创建逻辑键字符串。
- 新增 `AppendHitValuesAsync<T>`，复用现有物理批量读取、Hybrid 回填、Cluster 分槽、资源预算和取消检查，只追加命中值。
- Stream API 保持原有逻辑键、Hit 状态和逐项产出路径；List API 与 Stream 的命中值语义保持一致。
- 新增 `GetByPrefixAsync_UsesPhysicalPrefixBatchPath`，验证目标前缀命中值和其他前缀过滤。
- 新增 `GetByPrefixAsync_PreservesNullHitAndIgnoresMissingValue`，验证 null 命中不会被误判为缺失。
- 新增 `GetByPrefixAsync_CancellationAfterFirstBatch_DoesNotDispatchNextRead`，验证首批读取完成后取消会停止继续枚举和批量派发。

### 验证

- 精准测试 net8.0：`3 passed / 0 skipped / 0 failed`。
- 精准测试 net6.0：`3 passed / 0 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net8.0：`67 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net6.0：`67 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release：`0 warning / 0 error`。
- `git diff --check`：无错误；保留既有离线 NuGet/近似版本警告。

### 未完成与状态

- `FIX-007` 正式批量读取/扫描 P95 与分配预算仍未闭环；本轮未执行正式性能矩阵，也未宣称整体预算通过。
- 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵、100,000 项正式性能矩阵和真实网络/连接池指标仍为外部或延后项。
- 本轮未提交、推送、发布或修改独立 `review.md`。
