# BING-CACHE-TODO-20261007-088：冻结并实现 EasyCaching 分片同步清理并发契约

## 目标

关闭当前索引中“EasyCaching 客户端分片同步清理并发契约”的本地可执行部分。同步 API 默认继续保持旧的顺序行为；新增显式配置的有界并发模式，避免隐式改变已有调用方的异常、生命周期和部分成功语义。

## 契约

- `CacheSyncCleanupMode.Sequential` 为默认值，按分片索引顺序执行，保持现有行为。
- `CacheSyncCleanupMode.BoundedParallel` 按 `CacheConfiguration.MaxConcurrency` 分窗派发同步分片清理，每个窗口全部观察结束后才进入下一窗口。
- 一个窗口中任一分片失败时，不再派发后续窗口；已派发分片继续执行并被观察，避免后台同步 Provider 调用脱离调用方。
- 一个窗口完成后按分片索引选择首个失败异常传播；不包装为 `AggregateException`。已完成分片可能已经删除，操作不提供跨分片事务性。
- 同步模式不接受取消令牌；调用方如需可取消，应使用现有异步清理 API。Provider 必须支持同一实例上的并发同步调用，且调用方负责为并发模式配置足够连接池容量。
- 不使用无界任务；每个窗口最多创建 `MaxConcurrency` 个受控长运行工作项。拓扑开始/成功收尾校验、共享扫描预算和 Hybrid 每分片失效传播保持不变。

## 实现范围

- 在 `Bing.Caching` 增加同步清理模式枚举，并将不可变配置沿所有分片配置复制路径传递。
- 在 `ShardedRedisCacheManager` 抽取同步分片扫描调度器，接入 `RemoveByPrefix`、`RemoveByPattern` 和 `Clear`。
- 增加默认顺序、并发窗口上限、窗口失败观察、异常选择、空输入和模式字面量路径测试。
- 更新 EasyCaching/缓存 README、任务证据和权威索引；不修改 FreeRedis/CSRedis 同步 API。

## 验收

- 默认配置下现有同步清理测试的顺序、异常和拓扑探针计数不变。
- BoundedParallel 配置在 `MaxConcurrency=2`、3 分片时同时最多 2 个分片，首窗失败后不启动第三分片，并等待已派发分片结束。
- 多个失败时按分片索引传播稳定的首个异常；不残留后台任务。
- 前缀、通配模式和 Clear 三条同步路径共享调度器；字面模式仍走单键路由。
- `Bing.Caching`、`Bing.EasyCaching` netstandard2.0 构建及 EasyCaching 双 TFM 精准/完整测试通过。

## Change Impact Analysis

- ChangedFiles：`CacheSyncCleanupMode.cs`、`CacheConfiguration.cs`、`ShardedRedisCacheManager.cs`、分片配置复制路径、同步清理测试、README、088 任务证据和权威索引。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：新增 `CacheSyncCleanupMode`、只读配置属性和 `WithSyncCleanupMode` 派生配置方法；保留原 `CacheConfiguration` 构造函数签名，默认行为不变。
- ChangedRuntimePaths：EasyCaching 客户端分片同步前缀/模式删除和 Clear 的显式并发模式。
- ChangedProviders：EasyCaching Redis/Hybrid 客户端分片；其他 Provider 不变。
- ChangedTFMs：生产 `netstandard2.0`，测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无版本、包或 TFM 图变更。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、任务报告、测试映射和权威索引。
- AffectedDependents：使用 `CacheConfiguration` 的所有生产调用方；无需改造即可保持顺序，选择并发模式的调用方需显式调用 `WithSyncCleanupMode`。
- RiskLevel：HIGH；并发模式会同时调用同步 Provider，需显式选择并满足连接池和线程资源条件。
