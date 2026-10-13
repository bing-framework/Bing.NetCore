# BING-CACHE-TODO-20261006-041 执行记录

## 初始状态

EasyCaching 异步 `GetAllAsync` 当前通过 `GetAllStreamAsync` 收集结果，List 调用方不需要 `CacheEntry<T>` 的逻辑键和命中标记，却承担了对应的对象和迭代器分配。本任务先完成输入批量读取的直接值路径。

## Round 1（2026-10-06）

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`、本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid/Cluster 异步 List 批量读取。
- ChangedProviders：EasyCaching Redis、Hybrid、Redis Cluster。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；不重复同类数组复用实验。
- AffectedDependents：EasyCaching `GetAllAsync` 调用方和 List/Stream 一致性测试。
- RiskLevel：MEDIUM。

### 已完成

- `GetAllAsync<T>` 现在逐批生成物理键并调用 `GetAllPhysicalValuesAsync`，直接把值追加到结果列表。
- 保留物理键去重、重复输入位置还原、缺失值返回 `default`、Hybrid 剩余 TTL 回填、Cluster 槽分组、资源预算和取消检查。
- 只有 Stream API 继续创建 `CacheEntry<T>`；List API 不再经过 Stream 收集路径。
- 现有 List/Stream 一致性测试覆盖重复键、缺失值和 null 值，新增原始 Redis 批量测试同时覆盖 List 和 Stream。

### 验证

- 精准测试 net8.0：`2 passed / 0 skipped / 0 failed`。
- 精准测试 net6.0：`2 passed / 0 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net8.0：`64 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net6.0：`64 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release：`0 warning / 0 error`。
- `git diff --check`：无错误；保留既有离线 NuGet/近似版本警告。

### 未完成与状态

- `FIX-007` 正式批量读取/扫描 P95 与分配预算仍未闭环；本轮未执行正式性能矩阵，也未宣称整体预算通过。
- `GetByPrefixAsync` 仍复用 Stream 扫描收集路径，后续可单独设计扫描 List 快路径，不能与本轮混为已完成。
- 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵、100,000 项正式性能矩阵和真实网络/连接池指标仍为外部或延后项。
- 本轮未提交、推送、发布或修改独立 `review.md`。
