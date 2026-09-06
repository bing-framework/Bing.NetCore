<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260904-001
AI_EXECUTION_FINISHED_AT: 2026-09-05T00:38:06.839Z

# 实施执行报告

> Round 1 为 `BLOCKED`；Round 2 已完成纳入 scope 的代码修复与回归验证，但 FIX-003 的受保护 CI 外部输入仍未具备，因此本轮终态为 `PARTIAL`。

## 执行结论

当前状态：`PARTIAL`，RC 结论为 `RC NOT READY`。核心代码、测试、Provider runner 安全门禁和聚合报告已完成较大范围实施，但当前工作树仍为 dirty，Provider 真实运行不能升级为 `ReleaseEvidence`；PostgreSQL OUT/INOUT 保留明确 `ImplementationGap`；FormalHost 仅保留部分元数据组运行日志，未形成完整正式制品；完整文档导航与最终 RC 序列未闭环。未执行 git add、commit、push、PR、reset、clean 或自动合并。

## 任务信息

- Task ID：`BING-SQL-RC-HARDENING-20260904-001`
- 执行器：Copilot
- 分支：`dev_v6.0-refactor-sqlquery`
- HEAD：`ea006172207927bae0ebc9da2ea9c634b4045e68`
- SDK：`8.0.424`
- OS：Windows 10.0.19045，RID `win-x64`
- 运行时：.NET 8.0.11
- 执行开始：`2026-09-04T05:38:26.988Z`

## 计划执行情况

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| T01 | `COMPLETED` | 已记录 Git 状态、HEAD、分支、SDK、Runtime、OS；保留前置任务未提交变更。 |
| T02 | `COMPLETED` | 七个生产项目 Release build 均 ExitCode=0，RS0026 匹配数均为 0。 |
| T03-T04 | `PARTIAL` | 已有前置 API/Analyzer 改动和测试基础；最终 Public API 冻结与全量复核仍待完成。 |
| T05-T07 | `COMPLETED` | runner 安全隔离、无密诊断和 SQL Server startup 环境隔离已实现并完成局部验证。 |
| T08 | `PARTIAL` | 复用现有六态 Capability Catalog；已绑定 MySQL Batch/Multiple Result、PostgreSQL Function 和 SQL Server 新增合同，其他 Provider 仍待收口。 |
| T09 | `PARTIAL` | MySQL 双 TFM protected lane 均真实通过：59/59、0 失败、0 跳过；因 dirty source 不能升级为 Release Evidence。 |
| T10 | `PARTIAL` | PostgreSQL 原生 Function fixture 和 async/pre-cancel 合同已新增；项目双 TFM 编译通过，net8.0 protected lane 43/43 通过但因 dirty source 不能升级为 Release Evidence。OUT/INOUT 未伪造，保留 `ImplementationGap`。 |
| T11-T12 | `PARTIAL` | SQL Server 双 TFM protected lane 均核心通过：22 发现、21 执行通过、0 失败、1 个已声明 optional skip；T12 真实边界为 2098 成功、2099/2100/2101 由 SQL Server 拒绝；因 dirty source 不能升级为 Release Evidence。 |
| T13 | `COMPLETED` | SQLite 受控合同双 TFM 通过并标记 `TestGenerated`；Oracle unit 通过、外部连接明确跳过；Doris 只读探针明确跳过。 |
| T14 | `PARTIAL` | 已提前补齐运行元数据 sidecar、validator clean-source 门禁、CLI 参数和职责级单测；真实 protected-run 与版本语义仍待验证。 |
| T15 | `COMPLETED` | Evidence CLI 已重新编译并生成四份聚合报告；代表性真实 TRX 方法已正确绑定，dirty/TestGenerated 运行按设计保持 `NOT VERIFIED`。 |
| T16 | `PARTIAL` | 既有追溯已覆盖多项本轮符号；runner、CLI、SQLite、MySQL Batch、PostgreSQL Function、SQL Server T12 的完整最终映射仍需独立复核。 |
| T17 | `COMPLETED` | 未新增生产 friend assembly；IVT 保持在测试、集成测试、Benchmark 和必要辅助范围。 |
| T18 | `PARTIAL` | 现有 FormalHost 类覆盖主要纯 CPU、Mutation、Aggregate、Lambda、Debug 和 SQLite E2E 场景；未补齐全部计划独立大规模场景。 |
| T19 | `BLOCKED` | CI/E2E smoke 成功但仅为 DryJob；FormalHost 元数据组中途停止，仅保留日志，没有完整 raw/CSV/Markdown/HTML 正式制品。 |
| T20 | `PARTIAL` | 已更新 Release Notes 和数据库集成测试说明；完整 `docs/sql/` 导航及 Provider capabilities/migration/testing 页面未全部完成。 |
| T21 | `BLOCKED` | 局部 Unit、Provider、SQLite、Evidence CLI 和 smoke 已通过；完整 clean protected evidence、全量 FormalHost、全量文档/链接检查未完成。 |
| T22 | `BLOCKED` | 已生成最终报告并判定 `RC NOT READY`；不满足 `RC READY` 条件。 |

## 已完成事项

### T01：固定当前工作树、SDK 与 RC 输入

当前工作树已有前置任务相关修改，包括 API、PublicAPI、Provider runner、Provider 测试、RunSettings 删除和 Evidence 文件；本任务不覆盖、不回滚这些修改。工作树基线命令已执行：`git status --short`、`git diff --stat`、`git diff --check`、`git rev-parse HEAD`、`git branch --show-current`、`dotnet --version`、`dotnet --info`。

### T02：RS0026 Inventory 基线构建

构建日志目录：`artifacts/reports/rs0026-build-20260904/`。

| 项目 | ExitCode | RS0026 匹配 | Error 行 |
| --- | ---: | ---: | ---: |
| `Bing.Data.Sql` | 0 | 0 | 0 |
| `Bing.Dapper.Core` | 0 | 0 | 0 |
| `Bing.Dapper.MySql` | 0 | 0 | 0 |
| `Bing.Dapper.PostgreSql` | 0 | 0 | 0 |
| `Bing.Dapper.SqlServer` | 0 | 0 | 0 |
| `Bing.Dapper.Sqlite` | 0 | 0 | 0 |
| `Bing.Dapper.Oracle` | 0 | 0 | 0 |

### T08/T09：共享能力目录与 MySQL 真实合同增量

本轮继续复用现有 `ProviderCapabilityCatalog`、`ProviderContractRunner` 和六态 Evidence 模型，未创建第二套 Provider 合同框架。新增 MySQL 集成测试覆盖：

- `MySqlBatchAndMultipleContractTest.BatchCrud_WhenEntitiesAreProvided_ShouldPersistAndMutateRows`
- `MySqlBatchAndMultipleContractTest.InsertBatch_WhenLaterBatchFails_ShouldRollbackEarlierBatches`
- `MySqlBatchAndMultipleContractTest.Execute_WhenResultsAreReadInOrder_ShouldReleaseResourcesAfterDispose`
- `MySqlBatchAndMultipleContractTest.ExecuteAsync_WhenResultsAreReadInOrder_ShouldReleaseResourcesAfterDispose`

MySQL fixture 新增 `CreateMultipleQueryExecutor()`，Catalog 的 Batch 和 Multiple Result 条目已绑定上述真实测试方法；Returning 未被伪造为支持。

MySQL 双 TFM protected lane 均真实通过 `Discovered=59, Executed=59, Passed=59, Failed=0, CoreSkipped=0`。数据库版本为 `8.0.16`，driver assembly 为 `2.0.0.0`，运行时为 `.NET 8.0.30` / `net8.0` 或 `.NET 6.0.36` / `net6.0`；运行制品因工作树 dirty 被发布证据 validator 拒绝，仅保留为 `TestGenerated`。

### T10：PostgreSQL 原生 Function 合同

新增 PostgreSQL 固定前缀 Function `public.bing_sql_contract_function(integer)`，通过公开 `Sql(...)` 文本入口以 `Select ... From public.bing_sql_contract_function(@input_value)` 调用，验证返回表结果集的真实物化语义。没有将 PostgreSQL Function 错误地当作 Npgsql 6 `CommandType.StoredProcedure` 调用，也没有将结果集伪造成 SQL Server/MySQL OUT/INOUT 参数。

新增合同：

- `PostgreSqlProcedureContractTest.ExecuteFunctionAsync_WhenFunctionReturnsTable_ShouldMaterializeNativeRows`
- `PostgreSqlProcedureContractTest.ExecuteFunctionAsync_WhenCancellationIsRequested_ShouldCancelBeforeFunctionExecution`

PostgreSQL 集成项目 Release 双 TFM 编译通过，静态诊断无错误；本地 protected runner 的 `net8.0` 运行结果为 `Discovered=43, Executed=43, Passed=43, Failed=0, CoreSkipped=0`。sidecar 记录了 PostgreSQL `11.5`、Npgsql `6.0.11`、Provider assembly `7.0.0.0`、`.NET 8.0.30` 和 `net8.0`。由于工作树为 dirty，发布证据 validator 按设计拒绝 `SourceState=dirty`，该运行仅保留为 `TestGenerated`，不能作为 `ReleaseEvidence`。

### Runner 修复

PostgreSQL lane 首次启动前发现 `eng/ci/Invoke-ProviderIntegrationTests.ps1` 摘要哈希表重复定义版本和运行环境字段，PowerShell 在解析阶段失败。已删除旧的占位字段，保留 testhost sidecar 写入的真实元数据；随后 runner 成功完成 preflight、测试执行和 dirty-source 安全拒绝。未放宽跨 Provider gate、默认连接或 clean-source 门禁。

### T11/T12：SQL Server 真实合同和参数边界

SQL Server 首轮真实运行发现两处测试契约问题：批量实体的 Identity 元数据缺失，以及 `SET NOCOUNT ON` 下 Dapper Execute 返回 `-1`。已分别在测试实体上声明 `DatabaseGeneratedOption.Identity`，并按真实 Dapper 语义断言 `-1`，没有修改生产执行逻辑。

新增 T12 真实 `IN @ids` 边界合同：`2098` 个展开参数成功；`2099`、`2100` 和 `2101` 个展开参数均由 SQL Server 返回“maximum of 2100 parameters”错误，其中 `2101` 额外验证失败后同一查询对象可恢复执行。该事实说明公开 Dapper `IN` 展开存在额外参数开销，不能将 2099/2100 误报为可执行边界。

SQL Server 双 TFM protected lane 均真实通过 `Discovered=22, Executed=21, Passed=21, Failed=0, CoreSkipped=0, OptionalSkipped=1`。数据库版本为 `15.0.4480.2`，driver assembly 为 `2.0.20168.4`；运行制品因工作树 dirty 被发布证据 validator 拒绝，仅保留为 `TestGenerated`。

### T14：提前实施的 Evidence metadata 安全门禁

T14 基础实现提前于计划依赖 T09-T13 开始，原因是后续 Provider 真实运行必须先具备不可发布的 `not-recorded`、敏感字段和 dirty-source 安全拒绝能力。该偏差不修改批准的 `plan.md`，仅在本执行报告记录。

已完成：

- `ProviderRunMetadata` sidecar 由 testhost 从真实 Provider 程序集、数据库 scalar version、ADO.NET driver、Runtime、OS 和 TFM 写入；
- runner 绑定唯一 RunId 的 sidecar，拒绝缺失字段、占位值、敏感文本和 TFM 不匹配；
- `ProviderReleaseEvidenceValidator` 拒绝 `SourceState != clean`，并校验 summary/request 元数据一致；
- `ProviderCapabilityMatrix.ToMarkdown()` 表头与新增 Runtime/OS/SourceState 数据列对齐；
- 新增 `ProviderRunMetadataTest`，覆盖完整元数据、TFM 标准化、缺失字段、敏感字段和 UTF-8 无 BOM。

## 部分/未完成事项

- T03/T04 最终公共 API 决策、PublicAPI baseline/inventory 和迁移一致性仍需独立复核。
- T09-T12 的本地真实 Provider 运行均为 dirty source，不能代替 clean-source Release Evidence。
- T19 FormalHost 未形成完整正式制品；T20 文档导航未完全闭环；T21/T22 不能给出 RC READY。

## 修改文件

本阶段新增/修改：

- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/execution.md`
- `artifacts/reports/rs0026-build-20260904/`（构建日志和 summary）
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`（修复摘要哈希表重复字段）
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Infrastructure/DatabaseScript.cs`（新增原生 Function fixture）
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/SqlQuery/PostgreSqlProcedureContractTest.cs`（新增 Function 结果集和预取消合同）
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`（绑定 PostgreSQL Function 合同并保留 OUT/INOUT 实现缺口）
- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Infrastructure/MySqlIntegrationDatabaseFixture.cs`（声明应用提供 Guid 主键不由数据库生成）
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/SqlQuery/SqlServerExecutionContractTest.cs`（修正 SQL Server Identity/NOCOUNT 语义，新增 T12 IN 参数边界合同）

此前工作树中的前置任务文件保持不变，详见 T01 Git 基线。

## API/数据/配置变化

当前阶段未修改 API、数据库或配置；未读取或输出本地 Provider 配置中的秘密。

## 测试结果

PostgreSQL 双 TFM protected lane 各为 43/43 passed、0 failed、0 core skipped；MySQL 双 TFM protected lane 各为 59/59 passed、0 failed、0 core skipped；SQL Server 双 TFM protected lane 各为 21/21 executed passed、0 failed、0 core skipped、1 optional skipped。三个 Provider 的 runner 均随后因 dirty source 拒绝发布级证据。PostgreSQL/MySQL/SQL Server 集成项目 Release 双 TFM 编译通过；共享 Evidence/metadata 测试 net8.0 和 net6.0 各 78/78 通过；七个生产项目的 Release build 已通过，RS0026 均为 0。

## Build/Typecheck/Lint/Format

- Release build：7/7 通过。
- RS0026：0。
- `git diff --check`：基线阶段通过。
- `Bing.Dapper.MySql.Tests.Integration` Release build：net8.0/net6.0 通过，0 warning/0 error。
- `Bing.Test.Shared`：net8.0 78 passed、net6.0 78 passed。
- PostgreSQL：net8/net6 各 43/43 通过；MySQL：net8/net6 各 59/59 通过；SQL Server：net8/net6 各 21/21 核心通过并各有 1 optional skip。所有本地运行均因 dirty source 只能保留为 `TestGenerated`。

## 计划偏差

- T14 的元数据基础实现提前于计划依赖 T09-T13 开始。原因是 sidecar 完整性、占位值拒绝和 dirty-source 门禁是后续真实 Provider 证据进入报告前的安全前置条件。批准的 `plan.md` 未修改，偏差和风险已记录于本报告。

## 基线问题

- 工作树非 clean，包含前置任务未提交变更；本地运行证据不能直接升级为 Release Evidence。
- SQL Server 真实 lane 的已知 startup 配置隔离已修复并完成局部验证；完整 protected lane 尚待运行。
- 当前任务仅产生 CI/E2E Dry smoke 以及 FormalHost 元数据组的部分日志；正式 Benchmark 报告已更新为部分数据、不可比较且阻塞。

## 已知问题

- MySQL 新增真实合同尚未在受保护数据库 lane 运行，当前不能宣称 MySQL Batch/Multiple Result 已获得 Release Evidence。
- PostgreSQL Function 真实行为已在 dirty 本地 protected lane 通过；PostgreSQL OUT/INOUT 继续为明确 `ImplementationGap`，不是伪造通过。
- SQL Server CRUD/Batch/Procedure/Multiple Result/2100 边界已完成本地真实 lane 验证；仍缺 clean-source protected CI 证据和最终聚合报告。
- `ProviderVersion` 当前取 Provider assembly version，是否与发布 package version 完全等价仍需在 T14/T15 复核；在复核前不得写 RC READY。

## 风险与回归关注点

- 后续 Public API 决策可能引起 Shipped/Unshipped baseline 与下游调用方变更。
- Provider 集成测试会操作专用测试数据库，执行前必须满足计划中的安全 gate。

## Reviewer 注意事项

- 本报告为最终 BLOCKED 执行结论，不能作为 RC READY 结论；后续应在 clean protected commit 上重新执行未完成门禁。
- 未自动 git commit、git push、创建 PR 或自动 merge。

## Git 状态

- 分支：`dev_v6.0-refactor-sqlquery`
- HEAD：`ea006172207927bae0ebc9da2ea9c634b4045e68`
- 工作树：存在前置任务未提交修改；本任务仅追加执行制品。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：must
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/review.md`
- 说明：本轮只处理 `MUST_FIX`；`FIX-004` 为 `SHOULD_FIX`，未纳入 scope。未修改 `review.md`。

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：PARTIAL
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- `appveyor.yml`
- 根因：单 Provider/单 TFM writer 矩阵曾直接暴露 `ReleaseReady`，且聚合器没有强制完整核心 Provider、双 TFM、逐方法能力、Unit、FormalHost、RS0026 和 API 输入。
- 修复：
	- 单 Provider 矩阵的 `IsReleaseReady` 固定为 `false`，新增 `IsProviderRunReady` 表示局部矩阵状态；
	- 新增 `ProviderReleaseReadiness`，显式要求 MySQL/PostgreSQL/SQL Server/SQLite 与 `net6.0`/`net8.0` 的唯一运行记录、零失败、零核心跳过、受信 Release Evidence 和目录方法覆盖；
	- 聚合 CLI 重新验证 Release Evidence，并以实际 FormalHost manifest、RS0026 inventory、API gate 文件作为输入；缺任一输入保持 `ReleaseReady=false`；
	- AppVeyor Provider lane 改为分别执行 net6.0 与 net8.0。
- 验证：
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo -v minimal`：`83/83` PASS；
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore --nologo -v minimal`：`83/83` PASS；
	- 聚合 CLI 当前制品输出 `ReleaseReady=false`、`ProviderRunsValidated=0`、`FormalHostComplete=false`；
	- `ProviderReleaseReadiness` 单测覆盖缺失 Provider/TFM、ImplementationGap/FormalHost 缺失保持 false。
- 备注：完整受信四核心 Provider/双 TFM 证据尚未在本地生成，故本 FIX 不宣称整体 RC 已 ready。

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：PARTIAL
- 修改文件：
	- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
- 根因：Runner 直接信任 CI SHA，且使用既有输出 `--no-build`，没有把实际 Provider 测试程序集、Provider 程序集和 `Bing.Data.Sql.dll` 绑定到当前运行。
- 修复：
	- `Get-SourceIdentity` 先解析 `git rev-parse HEAD`，再比较 `GITHUB_SHA`、`BUILD_SOURCEVERSION`、`APPVEYOR_REPO_COMMIT`、`CI_COMMIT_SHA`，不一致直接拒绝；
	- Provider runner 在测试前执行当前 TFM Release build，生成 `provider-binary-manifest.json`，记录三类程序集 SHA-256、RunId、SourceIdentity 和 SourceState；
	- Request、summary、Validator、CLI 和能力矩阵传递 manifest 路径；Validator 重新计算每个文件 hash，并拒绝缺失、重复、陈旧或替换程序集；
	- Runner self-test 覆盖 CI SHA mismatch；共享测试覆盖 manifest missing、hash mismatch 和 binary changed after manifest。
- 验证：
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo -v minimal`：`83/83` PASS；
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore --nologo -v minimal`：`83/83` PASS；
	- `& .\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest`：PASS，包含 mismatched CI source identity 拒绝；
	- `dotnet build .\eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj -c Release --no-restore --nologo -v minimal`：0 warning/0 error；
	- `git diff --check`：PASS。
- 备注：当前工作树 dirty，未执行真实 clean protected lane；本地历史制品未含新 manifest，聚合器按设计将其判为未验证。

#### FIX-003

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：BLOCKED
- 修改文件：
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- 本执行报告
- 根因：当前工作树包含前置任务及本轮未提交修改，且 FormalHost 只有 DryJob/部分日志，没有完整 raw/CSV/Markdown/HTML 受信制品。
- 修复：
	- 聚合器要求工作区内 `formal-host-complete.json`，并校验 `FormalHost`、3 launch、6 warmup、15 iteration 及 raw/CSV/Markdown/HTML 报告集合；
	- 聚合器要求结构化 RS0026 inventory 覆盖 7 个项目并逐日志校验零错误，要求 API gate 文件包含 PublicAPI Analyzer 与 Reflection Contract PASS；
	- 缺少 clean protected evidence、完整 FormalHost 或任一 gate 时保持 `ReleaseReady=false`，不使用 dirty/TestGenerated/DryJob 冒充发布证据。
- 验证：
	- 当前聚合结果：`ReleaseReady=false`、`FormalHostComplete=false`、`Rs0026GatePassed=false`、`ApiGatePassed=false`、`ProviderRunsValidated=0`；
	- `artifacts/benchmarks/benchmark-report.md` 仍诚实标记 `BLOCKED / NOT COMPARABLE`；
	- 当前 Provider 运行均为 dirty，未伪造 clean protected Release Evidence；
	- 受保护 clean commit、完整四 Provider 双 TFM 运行和完整 FormalHost 仍需在 CI/受保护环境完成。

### Round 1 汇总

- MUST_FIX：`FIX-001`、`FIX-002`、`FIX-003`
- 已完成：FIX-001/002 的代码级安全门禁与测试覆盖；
- PARTIAL：FIX-001/002 的完整受信输入尚未在当前工作树取得；
- BLOCKED：FIX-003 的 clean protected evidence、完整 FormalHost 和最终 RC 序列；
- FAILED：无；
- 回归验证：共享测试 net6.0 `83/83`、net8.0 `83/83`；Bing.Data.Sql.Tests net8.0 `1282/1282`；CLI build 通过；Runner self-test 通过；`git diff --check` 通过；
- 下一步：交接回 `code-reviewer`，对本轮代码与真实证据重新独立验收；不得将本执行终态解释为 Reviewer 已通过或 RC READY。

### Round 2

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/review.md`
- 说明：本轮只处理 `MUST_FIX` 的 `FIX-003`、`FIX-004`；未修改 `review.md`，未处理 `FIX-005`。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- `appveyor.yml`
	- 本执行报告
- 根因：聚合 CLI 虽已具备 FormalHost、RS0026、API gate 和 Provider Evidence 的 fail-closed 检查，但 AppVeyor 仍未提供真实跨 job artifact 收集、SQLite 受信 Release Evidence、完整 FormalHost 制品及 clean protected Provider/TFM 输入，完整 RC 正向工作流无法在当前工作树证明。
- 修复：
	- 聚合 CLI 继续要求同一 `EvidenceSessionId`，重新执行归档 Provider Evidence 的完整路径、TRX/摘要/manifest/hash/source/count 校验，并拒绝 dirty/TestGenerated 运行进入 Release readiness。
	- 聚合输出继续校验 FormalHost manifest、RS0026 inventory 和 API gate；任一输入缺失或失败时保持 `ReleaseReady=false`。
	- AppVeyor aggregate lane 传递统一 session、Provider/SQLite/Unit/FormalHost/RS0026/API 输入，并仅在结构化聚合结果为 `ReleaseReady=true` 时成功；修正可选参数循环，避免重复注入 SQLite 目录参数。
- 验证：
	- `dotnet run --project .\eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj -c Release --no-build -- --workspace-root H:\Bing_Framework\Bing.NetCore --aggregate-output-directory artifacts/test-results/provider-aggregate-review-fix-round2-smoke --provider-results-directory artifacts/provider-test-results --unit-results-directory artifacts/test-results/unit-20260904 --evidence-session-id ci-review-fix-round2`：PASS（命令成功生成报告）。
	- 聚合结果：`ReleaseReady=false`、`ProviderRunsValidated=0`、`FormalHostComplete=false`、`Rs0026GatePassed=false`、`ApiGatePassed=false`，未将当前 dirty/TestGenerated 制品升级为发布证据。
	- 当前仍未取得 clean protected commit、八个 Provider/TFM 受信运行、SQLite Release Evidence、完整 FormalHost 和真实跨 job artifact 收集，因此 FIX-003 不能标记为 `COMPLETED`。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
- 根因：聚合阶段复用当前运行的 15 分钟 freshness 检查，导致合法的已归档 Provider Evidence 在多 lane 后置汇总时失效；同时缺少统一 `EvidenceSessionId` 的归档验证和可达的完整八运行正向测试。
- 修复：
	- 保留 `Validate()` 的当前运行 freshness 和当前文件时间窗口检查。
	- 新增 `ValidateArchived()`，跳过短时 freshness，但继续校验固定 RunId/路径、TRX、摘要 identity/count、source state、Provider/数据库/driver/runtime/OS 元数据、binary manifest、SHA-256 和 `EvidenceSessionId`。
	- CLI 聚合改用 `ValidateArchived()`，要求所有 Provider 运行属于同一聚合 session；SQLite 仍明确保持 `TestGenerated`，不得冒充 Release Evidence。
	- 全局 readiness 增加统一 session 检查；新增完整能力目录下四 Provider × 双 TFM 正向测试，同时保留默认目录中 `ImplementationGap` 的 fail-closed 语义。
- 验证：
	- `ValidateArchived_WhenCompletedMoreThanFreshnessWindowAgo_ShouldAcceptMatchingEvidence`：PASS。
	- `ValidateArchived_WhenEvidenceSessionDoesNotMatch_ShouldRejectArtifact`：PASS。
	- `ReleaseReadiness_WhenEvidenceSessionsDiffer_ShouldRemainFalse`：PASS。
	- `ReleaseReadiness_WhenCompleteCatalogAndAllCoreRunsMatch_ShouldReturnTrue`：PASS。
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore --nologo -v minimal`：`87/87` PASS。
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo -v minimal`：`87/87` PASS。
	- SQLite integration project Release/net8.0 build：PASS，0 warning/0 error。
	- Evidence CLI Release build：PASS，0 warning/0 error。
	- `& .\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest`：PASS。
	- `git diff --check`：PASS。

### Round 2 汇总

- MUST_FIX：`FIX-003`、`FIX-004`。
- 已完成：`FIX-004` 的 current/archive validation 分离、统一 session 绑定、归档超 freshness 测试及完整 readiness 正向/负向覆盖。
- PARTIAL：`FIX-003` 的代码级 aggregate lane 和 fail-closed 校验已完成，但真实跨 job artifact 收集、clean protected Evidence、SQLite Release Evidence、完整 FormalHost、RS0026/API gate 受信输入仍缺失。
- BLOCKED：受保护 CI/远端环境提供上述不可在当前 dirty 工作树生成的发布级输入。
- FAILED：无。
- 回归验证：共享测试双 TFM 各 `87/87`；SQLite 集成项目构建、Evidence CLI 构建、runner self-test、fail-closed aggregate smoke、静态错误检查和 `git diff --check` 均通过。
- RC 结论：保持 `RC NOT READY`；本终态不代表 Reviewer 已通过。
- 下一步：交还 `code-reviewer` 进行独立再次验收；由受保护 CI 提供完整 artifact/matrix/FormalHost/gate 输入后再复核 `FIX-003`。

### Round 3

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/review.md`
- 说明：本轮仅处理当前 Reviewer 报告中的 `FIX-003`，未修改 `review.md`，未处理 `FIX-005`。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `appveyor.yml`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
	- `eng/ci/Invoke-SqliteContractTests.ps1`
	- `eng/ci/Invoke-ReleaseCandidateValidation.ps1`
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：Provider manifest 原先引用 Provider job 的 build 输出目录，归档 aggregate 无法重新计算 DLL hash；聚合器只绑定 EvidenceSessionId，未约束所有 Provider/gate 来自相同 source identity；SQLite 只有 TestGenerated 入口；FormalHost、RS0026、API gate 缺少统一 provenance 和 artifact hash contract。
- 修复：
	- Provider runner 在写 manifest 前把测试程序集、Provider 程序集和 `Bing.Data.Sql.dll` 复制到当前 run artifact 目录，并对复制后的不可变副本计算 SHA-256；Validator 拒绝 run 目录之外的 manifest 路径，SQLite Provider 名称按 `Sqlite` 稳定解析。
	- readiness run 增加 `SourceIdentity`，全局 readiness 拒绝跨提交拼接；CLI aggregate 要求 Provider、FormalHost、RS0026、API 输入使用同一 EvidenceSessionId 和 SourceIdentity。
	- FormalHost manifest、RS0026 inventory 和 API gate 均要求受信 provenance；gate 引用的日志/TRX/报告必须存在且 SHA-256 匹配。
	- FormalHost manifest、RS0026 inventory 和 API gate 均要求受信 provenance、`SourceState=clean`；gate 引用的日志/TRX/报告必须存在且 SHA-256 匹配。
	- 新增受保护顺序编排入口 `Invoke-ReleaseCandidateValidation.ps1`，在 AppVeyor `release` lane 中按 unit、Provider 双 TFM、SQLite、RS0026、API、FormalHost、aggregate 顺序执行，并发布 release-candidate artifacts。缺少 CI build identity、clean source、数据库 secret、完整能力或 gate 输入时保持拒绝。
	- SQLite 默认路径仍要求并验证 `TestGenerated`；`-ReleaseEvidence` 仅允许 clean protected CI、run-local binaries、真实 metadata 和统一 CLI 验证，不把本地受控合同升级为发布证据。归档 validator 拒绝 `TestGenerated` matrix，aggregate 只通过 SQLite 专用 loader 读取 SQLite matrix，避免普通 summary 扫描绕过该边界。
- 验证：
	- `Bing.Test.Shared` Release/net8.0：`90/90` PASS。
	- `Bing.Test.Shared` Release/net6.0：`90/90` PASS。
	- Evidence CLI Release build：0 warning / 0 error。
	- SQLite integration Release build：net8.0、net6.0 均 0 warning / 0 error。
	- `Invoke-ProviderIntegrationTests.ps1 -SelfTest`：PASS。
	- `ValidateArchived_WhenMatrixUsesTestGenerated_ShouldRejectArtifact`：PASS。
	- 三份 CI PowerShell 脚本 AST parse：PASS。
	- SQLite 受控 `TestGenerated` net8.0 合同：1/1 PASS，Matrix Entries=2。
	- 独立 aggregate smoke：`ReleaseReady=false`、`ProviderRunsValidated=0/8`、`FormalHostComplete=false`、`Rs0026GatePassed=false`、`ApiGatePassed=false`；未将 dirty/缺失输入升级为 RC ready。
	- 本地 release orchestrator 前置门禁：在非 protected CI 环境立即拒绝。
	- `git diff --check`：PASS。
- 剩余阻塞：当前工作树 dirty，未取得 clean protected commit、三类外部 Provider 的六个双 TFM Release Evidence、SQLite 完整 capability coverage、FormalHost 正式报告以及受保护 gate artifact；因此 FIX-003 只能记录为 `PARTIAL`，RC 结论保持 `RC NOT READY`。

### Round 3 汇总

- MUST_FIX：`FIX-003`。
- 已完成：run-local binary manifest、跨 source/session fail-closed 校验、FormalHost/RS0026/API provenance/hash 验证、受保护顺序 release orchestrator 和 AppVeyor release lane 接入；SQLite TestGenerated 语义保持不变。
- PARTIAL：`FIX-003` 的真实 clean protected CI 正向路径仍未在当前工作树执行，且默认 catalog 存在明确 ImplementationGap，不能生成可信 `ReleaseReady=true`。
- BLOCKED：clean protected source、Provider 数据库凭据、完整 FormalHost/gate artifacts 和最终受保护聚合输入均属于当前环境不可提供的外部条件。
- FAILED：无。
- 回归验证：共享测试双 TFM 各 `90/90`；CLI/SQLite 双 TFM build；Provider runner self-test；SQLite TestGenerated 合同；CI script parse；fail-closed aggregate smoke；`git diff --check` 均通过。
- RC 结论：保持 `RC NOT READY`；本终态表示 Executor 已完成本轮可执行修复，不代表 Reviewer 已通过。
- 下一步：交还 `code-reviewer` 进行独立再次验收；由受保护 clean CI 运行 `PROVIDER_TEST_LANE=release` 后复核 `FIX-003`。

### Round 4 收口说明

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/review.md`
- 执行状态：`PARTIAL`

#### FIX-003

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：`PARTIAL`
- 已确认根因：RC orchestrator 尚未把 SQLite release-results 目录传入 aggregate；SQLite producer 与 loader 的根目录约束不一致；AppVeyor 的 `release` 仅存在 switch 分支而未证明实际调度；FormalHost 默认程序集扫描仍可能混入 DryJob，而 validator 要求全部 CSV 为 FormalHost。
- 本轮已完成：重新核对 producer、loader、aggregate、FormalHost validator、Benchmark 入口和 AppVeyor 配置；确认现有负向 fail-closed smoke 不能替代正向八运行证据。
- 未完成：尚未提交对应源码/配置修复，也未生成 clean protected source 下的完整八运行 ReleaseEvidence、FormalHost 和 aggregate 正向证据。
- 验证：现有 Evidence CLI、共享测试、runner self-test、脚本 AST parse 和 fail-closed aggregate smoke 仍保持此前通过；不能据此宣称 FIX-003 已解决。

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 当前状态：`BLOCKED`
- 未完成项：`artifacts/reports/rs0026-inventory.md`、完整当前任务符号追溯、受保护执行生成的真实 `api-gate.json`、`docs/sql/` 导航及最终报告引用尚未全部落地。
- 阻塞原因：本轮未完成 FIX-003 的受保护发布链修复；API gate 和 FormalHost 只能在真实受保护执行中作为本次 session/source 的制品被验证，不能使用手工或历史制品替代。
- 验证：未生成虚假 gate、FormalHost 或 ReleaseReady 证据；当前 RC 结论继续为 `RC NOT READY`。

### Round 4 汇总

- MUST_FIX：`FIX-003`，`PARTIAL`。
- SHOULD_FIX：`FIX-005`，`BLOCKED`。
- 已完成：完成证据复核并保留 fail-closed 结论；未修改 `review.md`。
- 未完成：SQLite producer/loader/aggregate 正向闭环、release lane 实际调度证明、FormalHost 正式 job 过滤、workflow 正负测试、RS0026 inventory、API gate provenance artifact、SQL 文档导航和最终报告引用。
- 回归验证：未新增代码修复；沿用并确认已有 CLI build、共享测试双 TFM、runner self-test、PowerShell AST parse、aggregate fail-closed smoke 与 `git diff --check` 结果。
- RC 结论：`RC NOT READY`；本终态仅表示本轮 Executor 在当前环境安全收口，不代表 Reviewer 已通过。
- 下一步：切换到实现/`review-fixer` 模式后继续完成 Round 4 Fix，再执行独立 `/review-plan`。
