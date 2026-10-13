# 公共批量写入契约补齐计划

## 目标

补齐三个 Provider 共用契约中对批量写入行为的直接断言，确保 `SetAll` 与 `SetAllAsync` 覆盖已有值、空输入不产生无效读写、异步入口在调用前取消时立即停止。该轮只补契约证据，不调整性能预算、不修改缓存 API 和生产实现。

## 当前实现与证据

- FreeRedis、CSRedis 和 EasyCaching 已分别实现 `SetAll`/`SetAllAsync`；实现路径都应使用覆盖写入语义，`TrySet` 才保留 NX 语义。
- `framework/tests/CacheContractAssertions.cs` 当前只验证单键 `Set`/`TrySet`，没有把批量覆盖写入作为跨 Provider 公共契约。
- 共享契约由以下三个集成测试入口调用：
  - `framework/tests/Bing.Caching.FreeRedis.Tests.Integration/SharedCacheContractTest.cs`
  - `framework/tests/Bing.Caching.CSRedis.Tests.Integration/SharedCacheContractTest.cs`
  - `framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs`
- 现有 Provider 精准测试分别覆盖部分批量写入，但无法保证三个 Provider 始终遵守同一公共行为。

## 实施范围

### P1：扩展共享契约

在 `CacheContractAssertions.VerifyAsync` 中增加：

1. `SetAll` 首次写入后再次写入同一键，读取结果必须为新值；
2. `SetAllAsync` 首次写入后再次写入同一键，读取结果必须为新值；
3. 同一批次包含多个键时保持每个键的新值；
4. 空字典的同步、异步入口不抛异常且不改变现有键；
5. 预取消令牌传入 `SetAllAsync` 时抛出 `OperationCanceledException`，并在取消前不要求提交部分批次；
6. 最终清理新增的批量测试键，避免污染共享隔离 Redis。

契约使用随机命名空间，保留现有 `ClearAsync` 清理方式，不调用 `FLUSHDB`。

### P2：验证与记录

- 运行三个共享契约测试项目的精准测试，确认新增断言可编译并在门控开启时执行。
- 运行受影响 EasyCaching 测试项目的 net6.0/net8.0 精准测试；若集成门控关闭，明确记录 Skip，不把 Skip 当作通过。
- 构建 `Bing.Caching`、FreeRedis、CSRedis、EasyCaching 的 `netstandard2.0` Release 产物。
- 执行 `git diff --check`。
- 更新本任务 `TODO.md`、`execution.md`、`test-mapping.md` 和生产符号映射。

## 非目标

- 不修改 `ICache`、Provider 公共签名或序列化格式。
- 不修改 FreeRedis/CSRedis/EasyCaching 生产批量实现，除非新增契约在本地可重复证明现有行为错误。
- 不重跑已达到 `VERIFIED_BOUNDARY` 的正式性能矩阵，不调整性能阈值。
- 不伪造 Redis、Cluster、分片或独立 Hybrid 总线的外部环境结果。

## Change Impact Analysis

- ChangedFiles：共享测试契约及本任务文档、生产符号映射。
- ChangedProjects：`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：仅测试已存在的批量写入、读取和清理路径。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching 的测试覆盖；预期不改生产实现。
- ChangedTFMs：net6.0、net8.0 测试；生产 netstandard2.0 构建。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：无生产调用方。
- RiskLevel：LOW。

## 验收标准

- 共享契约能在三个 Provider 测试入口复用，且覆盖同步/异步批量覆盖写入、空输入和预取消。
- 测试失败时能明确指出 Provider 违反覆盖写入或取消契约，而不是只依赖实现细节 Mock。
- 受影响项目和生产项目构建通过，`git diff --check` 无错误。
- 集成环境未启用时，报告明确区分 `compiled` 与 `skipped`，不宣称实机通过。
