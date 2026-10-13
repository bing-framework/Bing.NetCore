# BING-CACHE-TODO-20261006-042：EasyCaching 前缀 List 扫描快路径

## 目标

避免 EasyCaching `GetByPrefixAsync` 通过 Stream 创建每项 `CacheEntry<T>` 和逻辑键列表。List 调用方只需要命中值；扫描阶段使用物理前缀校验，读取阶段直接批量追加命中值。

## 范围

- 修改 `Bing.EasyCaching.CacheManager.GetByPrefixAsync<T>` 及其私有异步扫描/批量读取 helper。
- 使用 `CacheKeyFormat.TryMatchLogicalPrefix` 做命名空间、规范编码和哈希校验，不绕过隔离边界。
- 复用现有 Cluster 主节点清单、末尾拓扑校验、Provider 原生键流、扫描预算、批大小、Hybrid/Redis 批量读取和取消语义。
- Stream API 保留逻辑键和 Hit 状态，不改变公共接口或注册方式。

## Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、对应测试和本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid/Cluster/Provider-key-stream 的异步前缀 List 读取。
- ChangedProviders：EasyCaching Redis、Hybrid、Redis Cluster、内存适配器键流。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；不重复已停止的同类数组候选实验。
- AffectedDependents：EasyCaching `GetByPrefixAsync` 调用方、Hybrid 本地回填和扫描隔离测试。
- RiskLevel：HIGH；扫描过滤和 Cluster 拓扑边界必须保持一致。

## 验收

1. `GetByPrefixAsync` 不再调用 `GetByPrefixStreamAsync`，且不构造每项 `CacheEntry<T>`。
2. 只返回命中值，保持字面前缀、命名空间隔离、扫描预算、重复/过期处理、取消和拓扑完整性。
3. 现有前缀 Stream/List、一致性、预算和取消测试通过；补充直接 List 回归。
4. EasyCaching 双 TFM 测试、netstandard2.0 构建和格式检查通过；FIX-007 仍不宣称整体通过。
