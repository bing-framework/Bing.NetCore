<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260828-004
AI_EXECUTION_FINISHED_AT: 2026-08-28T07:26:59.9994594Z

# 实施执行报告

## 执行结论

当前状态：`PARTIAL`。所有无需外部授权的计划项已实现和验证；真实 MySQL/PostgreSQL/SQL Server、远端受保护 CI、FormalHost before 与 Phase 6 独立 review 尚未具备可审计证据，因此不能标记为 `COMPLETED`。

## 任务信息

- Task ID：`BING-SQL-RC-HARDENING-20260828-004`
- 开始时间（UTC）：`2026-08-28T06:03:46.591Z`
- 基线 HEAD：`faba0eee924b7c992dc0aaad414099d92308f5f9`
- 分支：`dev_v6.0-refactor-sqlquery`
- SDK：`global.json` 指定 `8.0.419`

## 计划执行情况

- RC28-P0-01：`COMPLETED`。报告已建立，受保护路径仅通过 Git diff 状态验证。
- RC28-P0-02：`COMPLETED`。报告使用仓库/SQLite/Provider-remote-FormalHost 三层证据模型。
- RC28-P1-03：`COMPLETED`。SQL Server 进程环境变量测试进入本程序集禁并行 collection，net6.0/net8.0 筛选均通过。
- RC28-P1-01/RC28-P1-02：`PARTIAL`。runner self-test、默认安全 lane 和无自动 runsettings 验证已完成；远端 provider job/secret scope 需要维护者配置。
- RC28-P2-01：`COMPLETED`。公开 `WhereIfNotEmpty` 空输入在扩展边界短路，直接测试锁定 SQL、参数、shape/cache version 和 render count。
- RC28-P2-02：`COMPLETED`。`Helper`、JoinItem 注入/clone 协作和 JoinClause helper 字段内部化；PublicAPI、Analyzer consumer contract、ReleaseNotes 与 governance 已同步。
- RC28-P3-01：`COMPLETED`。listener-off、steady-on、subscribe-plus-query 拆为独立 benchmark 类型。
- RC28-P3-02：`PARTIAL`。FormalHost after 已生成；before artifact 不存在，结论为 `NOT_COMPARABLE`。
- RC28-P4-01：`COMPLETED`。Data.Sql、Analyzer、Dapper Core、SQLite、runner 和 Release build 全部通过。
- RC28-P4-02：`BLOCKED`。缺少真实外部 Provider 安全环境。
- RC28-P5：`COMPLETED`。文档和追溯以当前 evidence 更新。
- RC28-P6：`BLOCKED`。等待独立 reviewer。

## 已完成事项

- 在修改业务代码前注册任务运行状态，运行模式为 `plan-execution`。
- 记录当前基线：Windows 10.0.19045、分支和 HEAD；运行时架构字段在本机返回空值，记录为 `unknown`，不影响安全验证。
- 对八个受保护配置路径逐个执行 `git diff --quiet -- <path>`，均未检测到改动；未读取其内容。
- 创建本任务的测试、集成、benchmark、验证、制品索引和 review 报告骨架。
- 在 `WhereIfNotEmpty` 公开 Fluent 扩展边界短路已定义空输入，避免 `SqlQueryOperationAccessor` 的成功 mutation 通知使 no-op 查询缓存失效；增加 null、空串、空白和非空直接 lifecycle 回归。
- 将 `Helper` 和涉及它的 `JoinItem` 协作成员内部化，收敛 `JoinClause` 的 helper 字段；添加第三方 consumer 不可编译合同和维护 PublicAPI shipped baseline。
- 为 SQL Server Startup 环境变量测试添加本程序集禁并行 collection，保留原 `try/finally` 恢复。
- 将 E2E diagnostics benchmark 拆分为 listener-off、steady-on 和 subscribe-plus-query 类型，默认基线不创建 observer。
- 运行 SQLite Dry smoke 和 listener-off FormalHost after，登记原始 artifact hash；没有 before 时不比较性能。
- 更新集成说明、ReleaseNotes、public API governance 和 traceability；历史绝对测试数字不再被描述为当前 commit 验收。

## 部分/未完成事项

- MySQL、PostgreSQL、SQL Server 的 real non-skip 测试需要受保护 CI 变量、专用安全测试数据库与 reset 授权。
- 远端 CI trusted-lane、secret scope、实际 job/run 与 required-check 状态无法从本地仓库证明。
- FormalHost before/after 需要同机、同 key 的 BenchmarkDotNet 原始制品；当前尚无本任务 before artifact。

## 修改文件

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/WhereClauseExtensions.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Internal/Helper.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/JoinItem.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerStartupConnectionStringTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerEnvironmentVariableTestCollection.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
- `ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`docs/ReleaseNotes.md`、`docs/integration-testing.md`、`docs/testing/database-integration-tests.md`
- 本任务报告目录下的执行期 Markdown 文件。

## API/数据/配置变化

`Helper` 与 `JoinItem` 内部协作成员是已批准的 7.0.0 主版本 Breaking Change；没有新增公开 SPI、没有新增 production friend assembly，也没有数据库 schema 变化。用户配置未读取、未修改。

## 测试结果

详见 `unit-test-report.md` 和 `integration-test-report.md`。本地 unit、Analyzer、Dapper Core、SQLite 和 runner self-test 全部通过；外部 Provider 仍为 `BLOCKED`。

## Build/Typecheck/Lint/Format

`dotnet build .\Bing.All.sln -c Release -nologo -v quiet -clp:ErrorsOnly` 通过：158 warnings、0 errors。`git diff --check` 通过。

## 计划偏差

- Phase 0 系统架构信息的 PowerShell 属性在本机为空；报告保留 `unknown`，没有通过猜测补值。

## 基线问题

- AppVeyor repository config 仍只提供安全 lane switch；为避免无凭据 Provider job 失败，远端 job materialization/secret scope 必须由维护者受保护配置完成。

## 已知问题

外部 Provider/CI/FormalHost 的状态必须以本任务 current 制品验证，历史报告不作为通过证据。首次 FormalHost 命令使用了 BenchmarkDotNet 不支持的 `--params`，未运行任何 benchmark；随后去除该参数后正式生成 after 制品。

## 风险与回归关注点

- `Helper` 与 `JoinItem` 内部化已通过 analyzer consumer contract 和 PublicAPI baseline 验证；外部 NuGet 消费者仍未知，ReleaseNotes/governance 已明确该主版本迁移边界。
- 不得将 DryJob、runner self-test、ValidateOnly、默认 gate Skip 或 DI Startup 测试标记为真实 Provider 执行。

## Reviewer 注意事项

优先检查受保护路径未变更、runner 的无密输出、mutation cache contract、Helper 可见性、diagnostic observer 生命周期以及 FormalHost/Smoke 证据是否区分。

## Git 状态

开始前工作树仅有本任务目录未跟踪。最终工作树仅含本任务实现/报告变更和本地生成但未跟踪的 benchmark artifacts。未自动执行 `git add`、`git commit`、`git push` 或创建 PR。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/review.md`

#### FIX-001

- 严重程度：`MEDIUM`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/benchmark-report.md`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：诊断 benchmark 类型继承带有 `[Benchmark]` 与 `[Params]` 的基类，BenchmarkDotNet 发现会将所有继承测量项和 `RowCount` 矩阵带入诊断及固定输入制品。
- 修复：将 SQLite 生命周期和真实执行 helper 移入不含 BenchmarkDotNet 属性的 `SqliteDapperE2EBenchmarkInfrastructure`；仅 listener-off 可扩展基类声明八个随行数变化的方法和 `RowCount`。steady listener-on、subscribe-plus-query、预取消、基数异常、Activity 与 Trace 改为各自独立的固定输入类型，分别拥有 setup、cleanup 和单一测量项。
- 验证：
	- `dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore --nologo -v minimal`：PASS，0 warnings、0 errors。
	- `--list flat --filter "*SqliteDapperE2EDiagnosticSteadyBenchmarks*"`：PASS，仅 `QueryWithDiagnosticListener`。
	- `--list flat --filter "*SqliteDapperE2EDiagnosticSubscribeBenchmarks*"`：PASS，仅 `SubscribeDiagnosticListenerAndQuery`。
	- `--list flat --filter "*SqliteDapperE2ECardinalityBenchmarks*"`：PASS，仅 `QueryToEntityCardinalityFailure`，无 `RowCount` case。
	- `--e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\rc28-e2e-smoke-review"`：PASS，Dry smoke 仅执行八个 listener-off 可扩展方法的 24 个 case。

#### FIX-002

- 严重程度：`MEDIUM`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/artifact-index.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/benchmark-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/progress.md`
- 根因：原索引仅记录基线 HEAD，不能唯一标识 dirty worktree 中的 benchmark 源码，且缺少命令、patch identity、脱敏结论、报告链接和 JSON 缺失说明。
- 修复：重新生成修复后拓扑的 Dry smoke 与 listener-off FormalHost after 制品；索引登记基线 HEAD、benchmark binary diff SHA-1 `22c00a6038eed6082eca70c2db6bc8f116c59d0e`、完整 worktree binary diff SHA-1、关联文件、完整命令、TFM/job、八个 SHA-256、脱敏扫描结论和反向报告链接。历史 `rc28-e2e-smoke` 与 `rc28-formal-after` 明确标为旧拓扑制品，不再代表当前实现。FormalHost before 仍缺失，因此 after 结论保持 `NOT_COMPARABLE`。
- 验证：
	- 新 FormalHost after：`--filter "*SqliteDapperE2EBenchmarks.QueryToList*" --artifacts "BenchmarkDotNet.Artifacts\rc28-formal-after-review"`：PASS，RowCount `1/100/1000` 三个 listener-off case 完成。
	- `Get-FileHash -Algorithm SHA256`：PASS，八个当前制品均与索引一致。
	- 凭据键名模式只读扫描：PASS，未发现敏感值。
	- 八个受保护路径均执行 `git diff --quiet -- <path>`：PASS；未读取、修改或输出其内容。
	- `git diff --check`：PASS。

### Round 1 汇总

- MUST_FIX：2。
- 已完成：2。
- PARTIAL：0。
- BLOCKED：0。
- FAILED：0。
- 回归验证：benchmark Release build、类型发现、Dry smoke、listener-off FormalHost after、制品哈希、敏感信息扫描、受保护路径检查和 `git diff --check` 均通过。
- 下一步：重新进行独立 Review；本终态仅表示 review-fix 执行器已完成本轮修复，不表示 `review.md` 已通过。
