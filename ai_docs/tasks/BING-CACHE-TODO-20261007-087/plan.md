# BING-CACHE-TODO-20261007-087：为自定义 EasyCaching Provider 暴露可观测分页键流

## 目标

补齐当前缓存索引中“自定义 EasyCaching Provider 隐藏分页”的可执行部分。保留现有 `ICacheKeyStreamProvider` 和 `GetAllKeysByPrefixAsync` 兼容路径，新增一个可选页流契约，使适配器能够在异步扫描时执行 `MaxScanPages`、记录页级探针，并继续按 `MaxScanKeys` 保护已接受候选键。

## 实现范围

- 在 `Bing.Caching` 增加公开的 `ICacheKeyPageStreamProvider` 可选接口，使用 `IAsyncEnumerable<IReadOnlyList<string>>` 按物理前缀产出键页。
- 在 `Bing.EasyCaching.CacheManager` 的异步前缀读取、前缀删除、模式删除和 Clear 共享路径中优先消费页流；每页在消费前执行 `CacheScanBudget.ConsumePage()`，并记录 `CacheCommandProbe.RecordScanPage`。
- 保留内部测试键流、旧逐键键流和完整集合回退，不伪造其页数；所有路径继续执行取消、命名空间校验和候选键预算。
- 更新 EasyCaching 与 Bing.Caching 文档，明确页流 Provider 的职责、页预算语义和旧兼容边界。
- 增加公共契约和 EasyCaching 双 TFM 测试，覆盖页优先级、页预算、键顺序、空页、取消和旧接口回退。

## 验收

- 实现页流接口的自定义 Provider 在 `MaxScanPages=1` 时最多向扫描处理路径消费一页，第二页返回后在消费其键前抛出 `CacheScanLimitExceededException`，异常类型为 `ScanPages`；由于 `IAsyncEnumerable` 没有“是否还有下一页”的预读契约，不宣称能阻止 Provider 在第二次 `MoveNextAsync` 中准备该页。
- 页流产生的键保持原顺序，空页不会导致错误，页内键仍受 `MaxScanKeys` 和逻辑命名空间过滤。
- 取消后不继续请求下一页；未实现页流的旧 Provider 行为保持不变且 `ScanPages` 继续为不可观测边界。
- `Bing.Caching` netstandard2.0、EasyCaching netstandard2.0 构建，以及 `Bing.EasyCaching.Tests` net6.0/net8.0 精准测试通过。

## Change Impact Analysis

- ChangedFiles：新增公共接口、EasyCaching 异步扫描适配、缓存文档、公共与 EasyCaching 测试、087 任务证据。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：新增可选 `ICacheKeyPageStreamProvider`，无现有成员签名破坏。
- ChangedRuntimePaths：EasyCaching 非 Redis Provider 的异步前缀读取和命名空间清理路径。
- ChangedProviders：自定义 EasyCaching Provider；Redis `IDatabase` 路径不变。
- ChangedTFMs：生产 `netstandard2.0`，测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：仅公共程序集新增类型，无包版本或 API 删除。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、任务计划、执行报告和测试映射。
- AffectedDependents：实现 `IEasyCachingProvider` 的外部适配器可选择实现新接口；未实现者继续使用旧回退。
- RiskLevel：MEDIUM；新增页边界限制会让显式配置 `MaxScanPages` 的页流 Provider 更早失败，但默认值 0 保持兼容。
