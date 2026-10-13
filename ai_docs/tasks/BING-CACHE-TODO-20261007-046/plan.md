# 分片流式批量契约补齐计划

## 目标

补齐 FreeRedis、CSRedis 客户端分片场景的流式批量契约证据，覆盖同一输入下 List 与 Stream 的键顺序、重复键、缺失键、`null/default`、`Hit` 状态和命名空间隔离。生产流式实现已经存在，本轮只补测试和状态文档，不重复性能微优化。

## 实施范围

1. FreeRedis 双节点分片：增加 `GetAllStreamAsync` 与 `GetByPrefixStreamAsync` 的跨节点一致性测试。
2. CSRedis 双节点分片：增加 `GetByPrefixStreamAsync` 的跨节点一致性测试，并保留现有 `GetAllStreamAsync` 覆盖。
3. 所有测试使用随机命名空间、相似命名空间哨兵和显式清理；继续使用现有 `ShardedFact` 门控。
4. 同步任务 TODO、执行记录和生产符号到测试方法映射，移除已过期的性能 `OPEN_ACTIONABLE` 表述，统一为 `VERIFIED_BOUNDARY`。

## 非目标

- 不修改 FreeRedis、CSRedis、EasyCaching 生产实现和公共 API。
- 不在没有双节点/Cluster 环境时伪造集成测试通过。
- 不重跑已触发 No-Progress Guard 的正式性能基准。

## Change Impact Analysis

- ChangedFiles：两个客户端分片集成测试及本任务文档。
- ChangedProjects：`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：仅测试已存在的分片 `GetAllAsync`、`GetByPrefixAsync`、`GetAllStreamAsync` 和 `GetByPrefixStreamAsync`。
- ChangedProviders：FreeRedis、CSRedis 测试覆盖；生产实现不变。
- ChangedTFMs：测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：无生产调用方。
- RiskLevel：LOW。

## 验收

- 两个测试项目的新增测试在 net6.0/net8.0 成功编译。
- 已启用隔离双节点时，新增测试通过；门控关闭时明确记录 Skip，不计为通过。
- `git diff --check` 无错误。
- 任务文档明确区分已实现能力、外部阻塞和性能边界。
