# EasyCaching 前缀流 null 命中修复计划

## 目标

修复 EasyCaching `GetByPrefixStreamAsync<T>` 在批量读取结果包含 `CacheValue<T>(null, true)` 时丢弃条目的问题，使 Redis/Hybrid 前缀流与公共 `CacheEntry<T>` 契约一致：物理键存在即产出条目，`Hit=true`，值可以是 `null/default`；扫描期间键已消失时产出 `Hit=false` 条目。

## 根因

`CacheManager.ReadPhysicalBatchAsync` 对批量返回值只在 `value.HasValue` 为 true 时 yield。EasyCaching 的 `CacheValue<T>.HasValue` 同时承担“键是否命中”标记，而 null 命中使用 `(null, true)`，因此不能用值的可空状态筛除条目。

## 实现范围

- 修改 `framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs` 的前缀流批量解码路径。
- 保持输入批量流、顺序、命名空间过滤、取消、异常传播和 Hybrid 最终一致语义不变。
- 新增 mock 单元测试，覆盖 null 命中、缺失键和普通值，断言逻辑键、值和 `Hit` 序列。
- 不修改公共 API、键格式、Provider 注册、Redis 拓扑和性能预算。

## Change Impact Analysis

```text
ChangedFiles: CacheManager.cs；Bing.EasyCaching.Tests/CacheManagerTests.cs；本任务文档
ChangedProjects: Bing.EasyCaching、Bing.EasyCaching.Tests
ChangedPublicContracts: 无
ChangedRuntimePaths: EasyCaching Redis/Hybrid 前缀流批量读取
ChangedProviders: EasyCaching Redis、Hybrid；Memory 键流适配器复用同一路径
ChangedTFMs: 生产 netstandard2.0；测试 net6.0/net8.0
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 否
AffectedDependents: GetByPrefixStreamAsync 调用方及 Hybrid 本地/分布式读取
RiskLevel: MEDIUM
```

## 验收

1. null 命中、普通值和缺失条目均按扫描顺序产出，`Hit` 分别为 true/true/false。
2. EasyCaching.Tests 相关精准测试在 net6.0/net8.0 通过。
3. Bing.EasyCaching netstandard2.0 构建通过，`git diff --check` 通过。
4. 不改变 List API 已有 null 命中和缺失语义。

## 非目标

- 不伪造 Redis/Hybrid 跨实例或 Cluster 实机证据。
- 不重新运行正式性能矩阵，不改变权威性能边界。
