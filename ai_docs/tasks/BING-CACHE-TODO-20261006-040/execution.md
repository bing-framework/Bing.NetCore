# BING-CACHE-TODO-20261006-040 执行记录

## 初始状态

本任务针对 EasyCaching Redis/Hybrid 批量读取解码阶段的 `RedisValue[] -> byte[][]` 引用数组复制。该数组不改变响应内容，只增加每批一次的对象分配和引用写入。正式性能矩阵不在本轮重复执行。

## Round 1（2026-10-06）

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.Hybrid.cs`、`framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`、本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：单节点/Cluster Redis 原始批量读取和 Hybrid 批量读取的解码阶段。
- ChangedProviders：EasyCaching Redis、Hybrid。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；没有重复正式性能矩阵。
- AffectedDependents：EasyCaching List/Stream 批量读取、Cluster 分组读取和 Hybrid 本地回填。
- RiskLevel：MEDIUM。

### 已完成

- `DecodeRawRedisBatch<T>` 直接调用 `CacheBatchBudget.Check(RedisValue[])`，并在同一数组上逐项转换和反序列化。
- 删除不再使用的 `ToByteArrays`，每个批次少创建一层 `byte[][]` 引用数组；Redis 命令、响应顺序、缺失值和异常传播保持不变。
- 新增 `CacheManagerTests.RawRedisBatchDecode_PreservesOrderAndMissingValues`，使用模拟 `IDatabase` 验证 `[命中、缺失、命中]` 顺序、值和 `Hit` 状态，并确认只反序列化两个命中值。

### 验证

- 精准测试 net8.0：`1 passed / 0 skipped / 0 failed`。
- 精准测试 net6.0：`1 passed / 0 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net8.0：`64 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching.Tests` net6.0：`64 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release：`0 warning / 0 error`。
- `git diff --check`：无错误；保留仓库既有离线 NuGet 警告记录。

### 未完成与状态

- `FIX-007` 正式批量读取/扫描 P95 与分配预算仍未闭环；本轮仅完成结构性中间数组消除，未宣称预算通过。
- 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵、100,000 项 EasyCaching 正式性能矩阵和真实网络/连接池指标仍为外部或延后项。
- 本轮未提交、推送、发布或修改独立 `review.md`。
