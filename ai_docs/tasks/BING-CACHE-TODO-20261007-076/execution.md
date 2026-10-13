<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261007-076
AI_EXECUTION_FINISHED_AT: 2026-10-07T09:51:21.7790988+08:00
AI_EXECUTION_STARTED_AT: 2026-10-07T01:48:27.233Z

# 执行记录

## Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs`、`framework/tests/Bing.EasyCaching.Tests/ShardedCleanupConcurrencyUnitTest.cs` 及本任务文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无；仅修正现有异步清理的内部取消时序。
- ChangedRuntimePaths：客户端分片异步前缀删除、模式删除和 Clear 的并发窗口异常路径。
- ChangedProviders：EasyCaching Redis/Hybrid 客户端分片；其他 Provider 不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务执行记录、TODO、测试映射和权威缓存索引。
- AffectedDependents：客户端分片异步清理调用方和分片清理单元测试。
- RiskLevel：MEDIUM。只改变失败窗口的取消及时性，成功路径和同步路径保持不变。

## 实现

- `RunShardScansAsync` 不再直接等待整个窗口的 `Task.WhenAll`；改用 `ObserveShardWindowAsync` 按完成顺序观察任务。
- 首个任务异常或取消时立即取消链接令牌，然后等待其余已派发任务，保留首个可观察异常；后续窗口不会继续派发。
- 成功路径仍按 `MaxConcurrency` 分窗，`CacheScanBudget` 分片上下文、完成/失败诊断和 `RunScanAsync` 拓扑收尾保持不变。
- 不改变同步清理、公共接口、Hybrid 消息协议或已发 Redis 命令不可撤回的边界。

## 验证

### L0 静态和构建

- `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj -c Release -f netstandard2.0 --no-restore`：0 警告、0 错误。
- `git diff --check` 在收口时执行；仅报告工作区既有 CRLF 转换提示，无新增空白错误。

### L1 精准测试

- `ShardedCleanupConcurrencyUnitTest`：net6.0 6/6、net8.0 6/6。
- `ShardedScanCancellationUnitTest`：net6.0 9/9、net8.0 9/9。
- `RemoveByPatternAsync_FailureCancelsOtherIssuedShardPromptly` 证明第二个已派发任务在首错后先收到取消，再由测试释放阻塞；原始 `InvalidOperationException` 保持不变。

### L2 受影响项目

- `Bing.EasyCaching.Tests` 完整 net6.0：100 通过、54 跳过、0 失败。
- `Bing.EasyCaching.Tests` 完整 net8.0：100 通过、54 跳过、0 失败。
- `NU1801` 和 `NU1603` 为既有 NuGet 源/依赖警告，不影响本轮结果。

### L3/L4/L5

- L3 分片实机门控复用 075 未变化范围证据：双节点环境缺失，net6/net8 各 6 项跳过，不能宣称真实跨节点通过。
- 未运行解决方案全量回归或正式性能矩阵；本轮没有公共 API、TFM、包图或 Benchmark Harness 变化。

## 状态分类

- 已完成：分片清理首错快速取消、已派发任务观察、原始异常传播和双 TFM 回归。
- `OPEN_ACTIONABLE`：无。
- `BLOCKED_EXTERNAL`：真实双节点分片、三主三从 Cluster、双进程回源、独立总线和实机 SCAN 页预算仍缺环境。
- `BLOCKED_APPROVAL`：NullCache 流式缺失项语义仍待明确。
- `DEFERRED`：同步清理并发契约、Hybrid 代理背压/重试/outbox、网络/客户端排队观测和严格三版性能对照。
- `VERIFIED_BOUNDARY`：正式性能预算仍为 `COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED`；100,000 项超时 cell 保留原结论。
- `ACCEPTED_LIMITATION`：清理扫描非快照、跨分片无事务/顺序保证、已发 Redis 命令不可撤回，失败允许部分成功。

## No-Progress Check

- 结论：`CHANGED`。076 关闭了 075 中异步清理失败取消滞后的本地根因，并用取消闸门测试证明首错会及时通知在途分片。
- 未重复运行不受影响的性能或外部拓扑实验。

## 下一步

- 当前没有可自动继续的本地 `OPEN_ACTIONABLE`；如继续实现，应先冻结同步清理并发契约，或提供外部 Redis/Cluster/总线环境补齐门禁。
