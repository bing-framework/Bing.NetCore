# 有界性能证据核对

本核对只整理已有任务中的 probe、benchmark、baseline 与预算证据；没有运行测试或 benchmark。按任务计划和 `GOAL_RULES.md`，没有获批阈值时只记录观测，`THRESHOLD_STATUS` 保持 `BLOCKED_APPROVAL`，不把历史的局部 probe 状态扩写为当前性能 PASS。

| 路径 | 证据状态 | Baseline / 预算 | 当前候选适用性 |
| --- | --- | --- | --- |
| F008 SQL 工厂生命周期 | 历史有界本地观测；Dry smoke 24/24 | 无 before/after；正式性能预算未批准 | F008 candidate 记录中的两个工厂注册源码 hash 与当前文件不匹配；当前源码没有 fresh 性能数据 |
| 多源 SQL Uow | Round 2 原始 probe 及 watchdog、child/host exit 完整 | 无历史等价 before；预算 `BLOCKED_APPROVAL` | 仅适用于冻结 candidate；当前 `SqlUnitOfWorkProbe.cs` hash 与 candidate identity 不匹配 |
| ABP 阶段 B operation | net8 单次 current-only 观测 | 无等价 before；预算 `BLOCKED_APPROVAL` | 后续 FIX-005 改动 root operation 路径；无最终候选 fresh probe |
| 模块 GC / 生命周期 | 有回归、重复和独立进程测试；无性能计时/分配数据 | 无性能 baseline 或获批预算 | GC 测试不构成性能证据；当前模块 scope-factory 改动没有前后测量 |

## F008 SQL 工厂生命周期

[计划 §6](../20261021-f008-sql-factory-scope/plan.md)规定 net8.0 Release、`ValidateScopes=true`、无数据库的 60 秒窗口：两个场景各预热 100 次，再各测 3 组、每组 1,000 次。实际 JSON 原样记录在[生命周期 probe 输出](../20261021-f008-sql-factory-scope/artifacts/lifecycle-probe.log)：总耗时 `0.4127652 s`，窗口上限 `60 s`，异常 `0`，连接工厂调用及连接打开均为 `0`。

| 场景 | 三组耗时（ms） | 三组分配（bytes） |
| --- | --- | --- |
| 新建 scope、解析四工厂、首次 typed Query 渲染、释放 | 34.8529 / 30.4468 / 32.9028 | 30,772,192 / 30,794,280 / 30,803,488 |
| 同 scope 重复 Create | 2.2271 / 2.2096 / 2.3148 | 1,721,416 / 1,680,352 / 1,680,352 |

预热值也在 JSON 中：第一场景 `132.8098 ms / 3,208,600 bytes`，第二场景 `0.2873 ms / 173,576 bytes`。probe 目标框架为 `.NET 8.0`；日志路径显示 SDK `8.0.424`，但没有记录 host runtime 的 patch 版本或硬件身份。

同任务的 [BenchmarkDotNet Dry smoke](../20261021-f008-sql-factory-scope/artifacts/benchmark-e2e-smoke-final.log)完成 `24/24` case，只验证真实 E2E benchmark 装配和显式 scope 路径。它不是稳态基准、吞吐/P95 报告或预算比较。任务报告所标 `PASS_LOCAL_PROBE_AND_DRY_SMOKE_ONLY` 仅限于这两项本地执行证据；没有前后性能配对，也没有正式预算。映射缓存键、格式化热路径及 Builder Clone 算法未改，任务明确将这些既有 benchmark baseline 判为不适用，且未重建 baseline。[最终 Review](../20261021-f008-sql-factory-scope/review.md)及[执行记录](../20261021-f008-sql-factory-scope/execution.md)保留该限制。

当前 [F008 candidate hash 清单](../20261021-f008-sql-factory-scope/artifacts/review-round-2/candidate.json)中 `DapperServiceCollectionExtensions.cs` 的记录 SHA-256 `e7c088bd9ec9...`、`EfCoreSqlQueryServiceCollectionExtensions.cs` 的 `e8c206f94033...` 与当前 hash `54b07ddc3cc6...`、`76edf71fbc8b...` 不同；清单中的 `SqliteDapperE2EBenchmarks.cs` 仍为 `e1fbc821647e...`，与当前一致。缺少该 candidate 的生产源码快照，无法只凭哈希差异判断工厂创建/解析路径是否发生实质变化。因此原 probe 仅作为原候选成本记录，不复用为当前候选性能证据。当前未提交的 `BingModuleManager.cs` 修改本身不在 F008 路径内。

## 多源 SQL Uow

最终可审计证据是 [Round 2 probe JSON](../20261021-sql-multidb-unit-of-work/artifacts/review-round-2/probe-confirmation-001/probe.json)、[candidate identity](../20261021-sql-multidb-unit-of-work/artifacts/review-round-2/probe-confirmation-001/candidate-identity.json)、[watchdog 结果](../20261021-sql-multidb-unit-of-work/artifacts/review-round-2/probe-confirmation-001/watchdog-result.json)和[双端观察](../20261021-sql-multidb-unit-of-work/artifacts/review-round-2/probe-confirmation-001/host-observation.json)。candidate identity 将 harness 与 Release DLL hash 固定在该次 `review-fix-round-1` dirty-worktree candidate；runtime `.NET 8.0.30`，TFM `.NET 8.0`，60 秒外部 watchdog，`TimedOut=false`，probe、child、runner 与独立 host observation 均记录 exit `0`。16/16 measurement cells 完成，报告总耗时 `1,200.7183 ms`，异常 `0`；请求/实际并发为 `1/1`，排队 `0`。

工作量为 lease 100 次预热及 3×1,000 次；三个 SQLite E2E 场景各 10 次预热及 3×100 次业务操作。分配口径是 `process_gc_total`（`GC.GetTotalAllocatedBytes(precise: true)`）。三组 E2E 的耗时/分配分别为：无 Uow `57.8236/53.9316/65.6613 ms`、`1,163,440/1,204,504/1,163,440 bytes`；单源事务 Uow `209.3832/201.2707/206.6997 ms`、`3,529,840/3,529,840/3,530,608 bytes`；双源非事务汇总 `73.0178/70.4049/70.1275 ms`、`3,002,640/3,002,640/3,043,704 bytes`。总计创建/打开/关闭/释放连接 `1,554/1,554/1,554/1,554`，实际事务 begin/commit/rollback/dispose 为 `314/314/0/314`，当前打开连接 `0`、峰值 `2`。这些是该本地 SQLite 工作负载的观测，未给出 P95、吞吐承诺或前后提升结论。

同报告保留的 Round 1 `1,733.9612 ms` 因 wrapper 未直接捕获 child exit，已被 Round 2 直接采集的 `1,200.7183 ms` 取代；更早的单轮 `1,417.4 ms` 既没有三轮 E2E，也没有物理事务调用观测，明确标为 superseded。Round 1/2 不是 before/after 性能对比。性能阈值仍为 `BLOCKED_APPROVAL`，未建立新的 baseline；详见[性能摘要](../20261021-sql-multidb-unit-of-work/artifacts/performance-summary.md)。当前工作区对 `Bing.Data.Sql.Benchmarks`、`Bing.Uow`、`Bing.Dapper.Core`、`Bing.EntityFrameworkCore` 和 `Bing.Data.Sql` 没有未提交源码差异，因此本次模块管理器修改没有触及这些路径；不过 [Round 2 candidate identity](../20261021-sql-multidb-unit-of-work/artifacts/review-round-2/probe-confirmation-001/candidate-identity.json) 中 `SqlUnitOfWorkProbe.cs` 记录 SHA-256 `b85449be5454...`、当前为 `64e175b30a26...`。现有数值只可作为冻结 candidate 的历史证据，不能作为当前源码 fresh 结果。

ABP 阶段 B 的[另一份 SQL Uow probe JSON](../20261022-abp-phase-b-operation-uow/artifacts/probe/uow-probe.json)是单独候选、不可与上一个结果合并：它记为 runtime `.NET 8.0.30`、60 秒限额内 `1,893.682 ms`、16 项测量、连接 `1,554` 次全部关闭释放、事务 `314` 次 begin/commit/dispose、rollback `0`、峰值打开连接 `2`。但该任务的 [capture note](../20261022-abp-phase-b-operation-uow/artifacts/probe/uow-probe-capture-note.md)明确说明 wrapper 因 `$pid` 自动变量冲突而 exit `1`，未捕获 child stdout/stderr/exit；JSON 的 `EXPECTED_SUCCESS` 只支持其明示观测值，不证明 child 进程 exit 或形成性能 PASS。该值没有 before 对照，阈值仍是 `BLOCKED_APPROVAL`。

## ABP 阶段 B operation probe

[operation JSON](../20261022-abp-phase-b-operation-uow/artifacts/probe/automatic-operation-probe-net8.json)记录 runtime `.NET 8.0.30`、TFM `.NET 8.0`，单线程（并发 `1/1`，队列 `0`），无连接 I/O。5 秒诊断阶段完成 3,000 项（empty/nested/independent 各 1,000），耗时 `21.3454 ms`、分配 `7,543,928 bytes`，Gen0/1/2 为 `1/1/0`，scope opened/disposed 与 root commit 均为 `4,000`。30 秒正式阶段每类预热 100 次、完成 5 轮（各类 5,000，总 15,000），耗时 `45.3946 ms`、分配 `37,641,608 bytes`，Gen0/1/2 为 `2/1/1`，scope opened/disposed 与 root commit 均为 `20,000`。最终计数是 `24,800`，包括探针内部额外执行，不应改写为正式 workload 的计数。

JSON 明确 `CURRENT_ONLY_NO_EQUIVALENT_BEFORE`，不支持前后对比或改善率。更关键的是，该 probe 的 candidate 是 `phase-b-current-worktree-after-operation-entry-tests`；之后的最终阶段候选 `phase-b-review-fix-round-3-fix005-2026-10-23-v1` 修改了 `UnitOfWorkScopeManager` 的 root-start observer 与 `UnitOfWorkBase` 的保护记录/准入路径（见[阶段 B execution](../20261022-abp-phase-b-operation-uow/execution.md)及[Review](../20261022-abp-phase-b-operation-uow/review.md)）。这些操作生命周期路径与 probe 测量相关，因此该观测不能充作最终 FIX-005 候选的 fresh 性能数据。Review 将总体证据记为 `OBSERVED / CAPTURE_INCOMPLETE (复用)`；FIX-005 后没有再跑 probe，预算仍 `BLOCKED_APPROVAL`。当前相关生产源码 dirty diff 只有 [BingModuleManager.cs](../../../framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs)，不改变 ABP operation probe 的代码路径；这不消除阶段 B 自身的候选差异。

## 模块 GC 与当前生命周期变更

GC 任务的 [run summary](../20261023-module-lifecycle-gc/artifacts/run-summary.json)是测试结果账本，不是性能报告：它只统计 TRX 的通过/失败/未执行数，不输出墙钟吞吐或分配。`gc-stress-5000-net6` 与 `gc-stress-5000-net8` 各为一项通过；6/12 进程并发批次也属于隔离进程测试。执行记录说明这些 5,000 次与并发运行超出原计划，作为调查历史单列，不能拼成当前候选，也不能证明进程内静态污染不存在；49 份 TRX 中还有一个 0-test 无效运行，另一次无 TRX 编译失败须看独立账本。[diagnosis](../20261023-module-lifecycle-gc/diagnosis.md)标明正式候选 `gc-capture-20261023-01` 缺 build 前同步 source hash，故没有完整 source-at-build 冻结。其弱引用/GC 结论不提供性能 baseline。

当前模块生命周期实现通过惰性缓存 `IServiceScopeFactory` 复用解析结果，在初始化和显式关闭时仍创建独立 scope；[retention-fix execution](../20261023-module-lifecycle-retention-fix/execution.md)及[最终报告](../20261023-module-lifecycle-retention-fix/final-report.md)记录了当前生命周期回归和最终 build/test，但没有 timing、allocation 或 before/after 数据。独立 Review 明确 `PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE_NO_CLAIM`。因此这项当前变更既没有可声称的性能收益，也没有经测量的性能回归；旧 GC stress 结果不能补作性能证据。

## 核对结论

- 已有可量化观测：F008 两个无数据库 scope 场景；多源 SQL Uow 的 16-cell 双 SQLite 场景；ABP operation 的单线程空/嵌套/独立 operation 场景。
- 没有可用于本次审查的正式性能预算或经批准的 regression threshold；所有预算状态维持 `BLOCKED_APPROVAL`。各任务没有建立可比的 before/after baseline。F008 与 SQL Uow probe 的成功只表示限定输入在其边界内完成。
- ABP operation probe 早于最终 FIX-005 的 root-operation 改动，不能代表最终候选；ABP SQL Uow JSON 的外层捕获不完整。模块 GC stress 和当前生命周期回归不提供性能数据。
- 本报告没有运行任何测试、benchmark 或 probe；未据此判定生产代码存在性能缺陷，也没有把无预算状态转为代码 finding。
