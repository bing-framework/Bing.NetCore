# BING-CACHE-TODO-20261006-041：EasyCaching 异步 List 批量读取快路径

## 目标

避免 EasyCaching `GetAllAsync` 通过流式 API 创建每项 `CacheEntry<T>`、逻辑键和异步迭代状态。List API 只需要按输入顺序返回值，因此直接调用已有物理批量读取路径，降低批量读取的对象分配。

## 范围

- 修改 `Bing.EasyCaching.CacheManager.GetAllAsync<T>(IEnumerable<string>, CancellationToken)`。
- 新增私有异步批量值追加 helper，复用 `GetAllPhysicalValuesAsync`、批大小、Cluster 分槽、Hybrid 回填、资源预算和取消语义。
- Stream API、同步 API、公共接口、物理键和 Provider 注册方式不变。
- 保留重复键位置、缺失值返回 `default`、输入只枚举一次和空输入不访问 Redis。

## Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、对应测试和本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid/Cluster 的异步 List 批量读路径。
- ChangedProviders：EasyCaching Redis、Hybrid、Redis Cluster。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；不重复已完成的同类数组/键格式实验。
- AffectedDependents：EasyCaching `GetAllAsync` 调用方及 List/Stream 一致性测试。
- RiskLevel：MEDIUM。

## 验收

1. `GetAllAsync` 不再调用 `GetAllStreamAsync`，且不创建 `CacheEntry<T>`。
2. 顺序、重复键、缺失/default、取消、异常和 Hybrid/Cluster 批量路由保持一致。
3. EasyCaching 双 TFM 测试、netstandard2.0 构建和格式检查通过。
4. FIX-007 正式性能预算仍保持开放，未用局部优化宣称整体通过。
