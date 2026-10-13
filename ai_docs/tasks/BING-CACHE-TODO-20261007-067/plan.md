# BING-CACHE-TODO-20261007-067：EasyCaching 前缀流回退路径的缺失项契约

## 目标

统一 EasyCaching `GetByPrefixStreamAsync<T>` 在批量读取能力不可用时的行为。扫描得到的候选键即使在读取前失效，也必须按公共 `CacheEntry<T>` 契约产出 `Hit=false`；值为 `null` 的实际命中仍产出 `Hit=true`。保持扫描顺序、批次边界、取消和异常传播语义不变。

## 现状与证据

- `CacheManager.ReadPhysicalBatchAsync<T>` 在 `GetAllPhysicalValuesAsync` 返回字典时已经按物理键是否存在产出缺失项。
- 当 Provider 的批量读取返回 `null`，代码回退到逐键 `GetAsync<T>`，目前只对 `HasValue=true` 产出条目，缺失项被静默丢弃。
- 同一公共流式接口因此因 Provider 能力不同而返回不同条目数量；该差异不属于扫描竞态的约定。
- 任务 063 的回归覆盖了字典批量路径，不覆盖此逐键回退分支；本任务以新的分支证据补齐覆盖。

## 修改范围

### 任务 067-A：回退读取行为

- 修改 `framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs` 的 `ReadPhysicalBatchAsync<T>`。
- 逐键回退始终为每个扫描候选键产出一个 `CacheEntry<T>`；`CacheValue.HasValue` 仅决定 `Hit`，不得决定是否产出。
- 不改变 `GetByPrefixAsync` 的 List 过滤语义，也不改变批量读取 `GetAllStreamAsync` 已有的缺失项语义。

### 任务 067-B：回归测试

- 在 `framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs` 增加只提供逐键读取结果的 Provider 测试。
- 覆盖普通值、`null` 命中、缺失值、原顺序和 `Hit` 状态；另断言不会调用批量读取接口。

### 任务 067-C：证据与索引

- 更新 `TODO.md`、`test-mapping.md`、`execution.md` 和 `ai_docs/cache-todo-index.md`。
- 记录 Change Impact Analysis；不重写任务 063 的历史证据。

## 验证

1. `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --framework netstandard2.0 --no-restore`。
2. `CacheManagerTests` 前缀流精准测试在 net6.0、net8.0。
3. `Bing.EasyCaching.Tests` 完整回归在 net6.0、net8.0。
4. `git diff --check`。

## 风险与验收

- 风险：调用方以前可能依赖回退路径静默忽略扫描竞态 miss；公共流式契约已经定义 `CacheEntry.Hit`，统一返回缺失项是预期行为。List API 继续只返回命中值，不扩大其结果。
- 验收：逐键回退对每个候选键返回一个条目；普通值和 null 命中分别为 `Hit=true`，失效键为 `Hit=false`；双 TFM 精准和完整测试无新增失败。
