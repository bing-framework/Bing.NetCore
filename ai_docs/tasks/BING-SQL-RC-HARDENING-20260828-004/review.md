<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260828-004
AI_REVIEWED_AT: 2026-08-28T07:39:36.6732037Z

# Review Fix 独立复审报告

## 验收摘要

最终结论：`PASS`。

本次为 Review Fix Round 1 的独立复审。优先按上一轮 `FIX-001` 与 `FIX-002` 的修复目标复查当前源码、Git Diff、BenchmarkDotNet discovery、当前/历史原始制品、SHA-256 和执行记录，并重新执行相关构建、Smoke、定向测试和安全检查。

两个上一轮 `MUST_FIX` 均已解决，未发现修复引入的新 BLOCKER/HIGH/MEDIUM/LOW 问题。本结论仅表示当前可由本地独立验收的 Review Fix 已通过；任务整体仍因真实外部 Provider、远端 CI 证据和 FormalHost before 缺失而保持 `PARTIAL`，不能解释为这些外部前置条件已完成。

## 审查边界

- 计划期望模块：Provider/CI 安全边界、SQL Server 环境变量并发隔离、`WhereIfNotEmpty` 缓存契约、`Helper` 可见性、SQLite E2E benchmark 隔离及报告追溯。
- 当前 Git Diff：14 个已跟踪文件及任务目录内未跟踪报告均与任务范围一致；未发现本轮 Review Fix 范围外的生产行为变更。
- 当前基线 HEAD：`faba0eee924b7c992dc0aaad414099d92308f5f9`。
- 受保护配置：八个路径仅通过 `git diff --quiet -- <path>` 检查，均未变更；本次未读取、输出或修改其内容。
- `execution.md` 在复审开始前有外部改动提示，已读取当前内容但未修改。

## 上一轮 FIX 复核

### FIX-001：RESOLVED

- 原问题：诊断 benchmark 继承带 `[Benchmark]`/`[Params]` 的基类，导致各类型发现无关测量项，固定输入场景也展开为三组 `RowCount`。
- 当前代码：`SqliteDapperE2EBenchmarkInfrastructure` 仅提供 SQLite 生命周期和执行 helper，不含 `[Benchmark]`、`[Params]` 或 `NoOpDiagnosticObserver` 实例化。`SqliteDapperE2EScalableBenchmarksBase` 独占八个行数敏感方法与 `RowCount=1/100/1000`。固定输入、steady listener-on 和 subscribe-plus-query 各自拥有独立 setup、cleanup 与单一 benchmark。
- 独立 discovery：
  - `SqliteDapperE2EDiagnosticSteadyBenchmarks` 仅发现 `QueryWithDiagnosticListener`。
  - `SqliteDapperE2EDiagnosticSubscribeBenchmarks` 仅发现 `SubscribeDiagnosticListenerAndQuery`。
  - `SqliteDapperE2ECardinalityBenchmarks` 仅发现 `QueryToEntityCardinalityFailure`，无 `RowCount` 展开。
  - `SqliteDapperE2ESmokeBenchmarks` 仅发现 8 个 listener-off 可扩展方法。
- 运行证据：以新目录 `BenchmarkDotNet.Artifacts/rc28-e2e-smoke-rereview` 独立运行 `--e2e-smoke` 成功；报告含 8 个方法乘以 3 个 RowCount，共 24 个 Dry case。该 Smoke 不运行 FormalHost，且不作为性能比较依据。
- 结论：测量条件、observer 生命周期与 RowCount 矩阵已符合 `RC28-P3-01`。

### FIX-002：RESOLVED

- 原问题：制品索引仅记录基线 HEAD，无法唯一确定 dirty worktree 的 benchmark 源码，且缺少命令、patch identity、脱敏、报告链接和 JSON 缺失说明。
- 当前索引：`artifact-index.md` 记录基线 HEAD、`dirty worktree`、benchmark 源文件、benchmark binary diff SHA-1 `22c00a6038eed6082eca70c2db6bc8f116c59d0e`、运行时 TFM/Provider、完整命令、Dry/FormalHost job、八项 SHA-256、敏感信息扫描结论、报告链接及 BenchmarkDotNet JSON 未生成的原因。
- 独立哈希复核：当前 `SqliteDapperE2EBenchmarks.cs` 的 binary diff SHA-1 仍为 `22c00a6038eed6082eca70c2db6bc8f116c59d0e`；索引中的 8 个 review Smoke/FormalHost 制品全部存在，重新计算 SHA-256 与索引一致。
- 追溯边界：完整工作树 diff 会随制品生成后的文档变化而改变；当前值为 `728560873fdb7e14b35b46b3258b1a2ca25ff957`。制品所需的 benchmark 源专用 identity 保持不变，且已登记原始运行时的完整工作树快照；因此不影响当前 artifact 与执行源码的对应关系。
- 安全复核：对已索引制品按连接与凭据键名模式扫描，未命中连接字符串、数据库地址或凭据值。
- 历史制品：`rc28-e2e-smoke` 与 `rc28-formal-after` 已正确标为隔离重构前的历史证据，未被用于当前实现结论。
- 结论：制品 provenance 达到 `RC28-P0-01`、`RC28-P3-02` 和 `RC28-P5-02` 对本地 benchmark 的可审计要求。

## 计划验收矩阵

| 计划项 | 结论 | 独立证据 |
| --- | --- | --- |
| RC28-P0-01/02 报告与保护边界 | PASS | 任务报告齐全；制品索引具备 source/job/command/hash/脱敏/链接字段；受保护路径未变更。 |
| RC28-P1-01 CI lane materialization | NOT_VERIFIABLE | 远端 job、secret scope 与 trusted-lane 无本地无密证据；报告准确保留 PARTIAL。 |
| RC28-P1-02 runner 合同 | PASS | `Invoke-ProviderIntegrationTests.ps1 -SelfTest` 独立复跑通过，未被表述为真实 Provider 执行。 |
| RC28-P1-03 环境变量并发隔离 | PASS | SQL Server 专属 collection 使用 `DisableParallelization=true`，目标测试标注 collection；net8.0 筛选 3 passed。 |
| RC28-P2-01 no-op mutation cache | PASS | `WhereIfNotEmpty` 在 mutation gateway 前短路空输入；net8.0 定向筛选 4 passed。 |
| RC28-P2-02 `Helper` 边界收敛 | PASS | `Helper` 与 `JoinItem` 协作成员保持 internal；第三方 consumer 编译负向契约独立复跑 1 passed。 |
| RC28-P3-01 benchmark 测量隔离 | PASS | FIX-001 的源码断言、三项 discovery 和新的 24-case listener-off Smoke 均通过。 |
| RC28-P3-02 FormalHost 协议 | PARTIAL | 修复后 listener-off FormalHost after 的 3 个 case 与制品哈希可复核；无同 key before，保持 `NOT_COMPARABLE`。 |
| RC28-P4-01 本地矩阵 | PASS | 本轮 benchmark build、相关 no-op/API/并发定向测试与 runner self-test 均通过；既有完整矩阵结果已记录。 |
| RC28-P4-02 三个外部 Provider | BLOCKED | 无本任务 current non-skip TRX/JSON，解除条件在集成测试报告中完整记录。 |
| RC28-P5 文档与追溯 | PASS | benchmark 结构、制品来源和 `NOT_COMPARABLE` 陈述与当前源码及制品一致。 |
| RC28-P6 Review Fix 闭环 | PASS | 两个 MUST_FIX 均以独立实际验证解决，本报告完成复审。 |

## 构建、测试与制品验证

```text
Bing.Data.Sql.Benchmarks Release build: 0 warnings, 0 errors
Diagnostic steady discovery: 1 benchmark
Diagnostic subscribe discovery: 1 benchmark
Cardinality discovery: 1 benchmark, no RowCount matrix
Listener-off Smoke discovery: 8 methods
Fresh listener-off Dry Smoke: 24 cases, passed
RawFluent_WhenWhereIfNotEmpty* (net8.0): 4 passed, 0 failed
BuilderInternals_WhenUsedByThirdPartyConsumer_ShouldNotCompile: 1 passed, 0 failed
SqlServerStartupConnectionStringTest (net8.0): 3 passed, 0 failed
Invoke-ProviderIntegrationTests.ps1 -SelfTest: passed
Indexed artifact SHA-256 verification: 8/8 matched
Indexed artifact connection/credential scan: passed
Protected path diff check: passed
git diff --check: passed
```

## API、资源与文档审查

- 默认 listener-off 类型不创建 observer；steady-on 在其 `GlobalSetup` 创建并在 `GlobalCleanup` 释放 observer；subscribe-plus-query 每次测量使用 `using` 释放 observer。未发现跨 type 的 observer 生命周期泄漏。
- `SqlMetadataBenchmarks.Program.Main` 的 `--e2e-smoke` 仍显式 allow-list `SqliteDapperE2ESmokeBenchmarks`，不会误跑 FormalHost。
- `benchmark-report.md` 仅将修复后 FormalHost after 作为当前单次基线，并保持 `NOT_COMPARABLE`；未将 Dry Smoke 或历史制品用于性能改善/回归声明。
- `ai_docs/sql-metadata-test-traceability.md` 与 `artifact-index.md` 的 benchmark 表述与当前拆分结构一致。

## 外部阻塞与残余风险

- MySQL、PostgreSQL、SQL Server 仍需要受保护环境中的专属 gate、连接变量、安全测试数据库、reset 授权以及 current non-skip TRX/JSON。
- 远端 CI 的 job materialization、secret scope、trusted-lane 和实际运行制品仍需维护者提供无密证据。
- 没有与 current FormalHost after 同 key 的 before artifact；性能结论必须继续保持 `NOT_COMPARABLE`。

上述为计划已声明且报告准确记录的外部阻塞，不构成当前 Review Fix 的未解决修复项。

## 最终 Checklist

- [x] 已读取当前 plan、execution、旧 review、Git Diff、源码、测试、报告和制品。
- [x] 已逐项独立验证上一轮 `FIX-001` 与 `FIX-002`。
- [x] 已确认 benchmark 隔离、固定输入矩阵和 fresh Smoke 行为。
- [x] 已确认制品哈希、benchmark source identity、命令、脱敏与报告链接。
- [x] 已确认受保护路径未变更，且未读取其内容。
- [x] 已复跑相关构建、定向测试、runner self-test 和 `git diff --check`。
- [x] 未修改业务代码、测试代码、plan.md 或 execution.md。
- [x] 当前独立 Review 不含未解决 MUST_FIX 或 SHOULD_FIX。
