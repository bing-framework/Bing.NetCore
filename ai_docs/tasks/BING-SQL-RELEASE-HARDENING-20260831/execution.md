<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RELEASE-HARDENING-20260831
AI_EXECUTION_FINISHED_AT: 2026-09-03T00:56:28.5979503Z

# 实施执行报告

## 执行结论
本轮 Review Fix 已完成执行报告和最终报告的一致性修复；任务发布结论仍为 `PARTIAL`，发布准入继续受真实环境条件阻塞。当前 `global.json` 与实际解析 SDK 均为 `8.0.424`；Provider Profile 能力失败分类、P8 职责审计、Data.Sql/Dapper Core 全量单元回归、SQLite 受控 Matrix/TRX 接入和报告同步已完成。真实外部 Provider 集成、完整 Release Gate、FormalHost Benchmark 和独立 Review 仍未完成，不能将任务标记为 `COMPLETED`。

## 任务信息
- Task ID：`BING-SQL-RELEASE-HARDENING-20260831`
- 执行器：Copilot，`review-fix`
- 分支：`dev_v6.0-refactor-sqlquery`
- 基线 HEAD：`ee4688dedf3ef7c11efbd78989c596aac5f6529b`
- 上一执行回合终态时间：`2026-09-02T13:45:00.000Z`
- 当前基线与最终本地验证：`2026-09-02`，SDK `8.0.424`
- 工作树：包含本任务及已有未提交修改

## 计划执行情况
| 范围 | 状态 | 说明 |
| --- | --- | --- |
| Phase 0 基线、状态和报告 | `COMPLETED` | 已记录当前 HEAD、工作树、当前 SDK `8.0.424` 并建立四类报告。 |
| Phase 1 Provider Integration Contract | `PARTIAL` | 已建立共享 Contract runner 和六态 Matrix 输出；完整外部 Provider 场景矩阵仍未建立。 |
| Phase 2 Oracle/SQL Server 真实合同 | `BLOCKED` | 缺少安全外部数据库、授权和 Oracle 固定前缀 fixture。 |
| Phase 3 Transaction Capability/Diagnostics | `PARTIAL` | 生产调用链、Profile 字段、模式诊断和直接测试已完成，并在当前 SDK `8.0.424` 下完成相关本地回归；真实驱动运行验证未执行。 |
| Phase 4 Cancellation | `PARTIAL` | 事务预取消和回退前取消已有直接测试；完整 Provider 执行中取消矩阵未执行。 |
| Phase 5 Procedure | `BLOCKED` | 外部 Provider Procedure 矩阵未执行，静态声明不计为真实证明。 |
| Phase 6 Provider SPI | `PARTIAL` | 五个 optional SPI、默认实现桥接和核心迁移已完成；第三方 Consumer 编译合同已有当前源码身份证据，但完整发布门禁未闭环。 |
| Phase 7 Public API/IVT | `PARTIAL` | Unshipped 与 IVT 相关静态审计已完成；完整解决方案 Analyzer Gate 未重新执行。 |
| Phase 8/9 复杂度和直接测试 | `PARTIAL` | 本任务相关事务、Profile、SPI 和诊断测试已补；全计划测试闭环未执行。 |
| Phase 10 Integration | `PARTIAL` / `BLOCKED` | SQLite 合同双 TFM 当前 TRX 已生成并通过；外部 Provider 环境和完整集成门禁仍缺失。 |
| Phase 11 Benchmark | `BLOCKED` / `NOT COMPARABLE` | 未运行本任务 Benchmark，无同 key before/after 原始制品。 |
| Phase 12 文档和发布准备 | `PARTIAL` | 追溯、Unit/Integration/Benchmark/Final Report和数据库集成测试文档已同步；完整 SQL 文档统一未完成。 |
| Phase 13 Release Gate/Review | `BLOCKED` / `PENDING` | Release Gate 未执行；独立 Review 待下一轮验收。 |

## 已完成事项
- `SqlProviderTransactionCapabilities` 新增 `SupportsNativeAsyncBegin`、`SupportsNativeAsyncCommit`、`SupportsNativeAsyncRollback`，`CreateSnapshot()` 深复制三项配置。
- MySQL、PostgreSQL、SQL Server、Oracle Provider Profile 根据本地驱动反射证据声明异步事务能力；SQLite 保持不声明 native async。
- `SqlTransactionAsyncAdapter` 支持原生异步与同步回退模式结果，覆盖 `Task`、`ValueTask`、预取消、回退前取消和反射异常解包。
- `SqlTransactionScopeLease` 保存最近执行模式并使用 `Volatile` 读写；`SqlQueryBase` 的 After/Error 诊断在事务完成/回滚后刷新模式，避免模式丢失。
- `SqlLambdaQueryCore` 已迁移到 `ISqlMultiSourceFromClause`、`ISqlMultiSourceSelectClause`、`ISqlMultiSourceGroupByClause`、`ISqlMultiSourceOrderByClause`、`ISqlMultiSourceJoinClause` 五个 optional SPI；默认 Clause 提供对应桥接。
- 补充 Profile、SPI、事务 Adapter 和 SQL Server 替身成功/失败诊断测试；测试替身改为真实虚方法覆盖，避免 `new` 隐藏成员造成误判。
- 更新 `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt` 和 `ai_docs/sql-metadata-test-traceability.md`。

## 部分/未完成事项
- 完整 Provider Integration Contract、所有 Provider 的统一六态 Capability Matrix、结构化 capability reason model 未完成；SQLite 本地 Matrix 已生成。
- Oracle 安全集成 fixture、SQL Server Query/CRUD/Batch/Transaction/Procedure/Multiple Result/Cancellation 真实合同未完成。
- MySQL、PostgreSQL、SQL Server、Oracle、Doris 真实 Provider 执行未完成；静态 Profile、替身测试和 `TestGenerated` 制品不替代 Release Evidence。
- 完整 Public API Analyzer、全解决方案 Build/Test、完整 SQLite 门禁、FormalHost Benchmark 和覆盖率仍未完成；Round 5/9 SQLite 合同已在 `8.0.424` 下双 TFM通过。
- SQL 用户文档统一、完整 ReleaseNotes/migration 收口和独立 Review 未完成。

## 修改文件
本轮与任务直接相关的修改/新增文件：
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/SqlProviderProfile.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/ISqlMultiSourceClauseContracts.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/GroupByClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/OrderByClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/SelectClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Diagnostics/SqlTransactionDiagnosticInfo.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeLease.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Transaction.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Diagnostics.cs`
- `framework/src/Bing.Dapper.MySql/Bing/Data/Sql/Builders/MySqlSqlProvider.cs`
- `framework/src/Bing.Dapper.Oracle/Bing/Data/Sql/Builders/OracleSqlProvider.cs`
- `framework/src/Bing.Dapper.PostgreSql/Bing/Data/Sql/Builders/PostgreSqlSqlProvider.cs`
- `framework/src/Bing.Dapper.SqlServer/Bing/Data/Sql/Builders/SqlServerSqlProvider.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlProviderProfileTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Clauses/SqlClauseContractTest.cs`
- `framework/tests/Bing.Dapper.Core.Tests/Factories/SqlFactoryTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/OfficialProviderInstanceTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- `ai_docs/sql-metadata-test-traceability.md`
- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/progress.md`
- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/execution.md`
- `artifacts/test-results/unit-test-report.md`
- `artifacts/test-results/integration-test-report.md`
- `artifacts/benchmarks/benchmark-report.md`
- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`

## API/数据/配置变化
- 新增五个标记为 `EditorBrowsable(EditorBrowsableState.Never)` 的 optional Lambda 多源 SPI。
- 新增三项 Provider native async transaction capability 属性。
- 新增 `SqlTransactionDiagnosticInfo.ExecutionMode` 诊断字段。
- 未修改数据库 schema、连接配置、Secret 或生产数据；当前 `global.json` 声明 SDK `8.0.424`。
- 未新增 production-to-production `InternalsVisibleTo`。

## 测试结果
| 验证项 | 状态 | 证据/原因 |
| --- | --- | --- |
| 目标文件编辑器诊断 | `PASS` | 目标源码与测试文件均返回 `No errors found`。 |
| `git diff --check` | `PASS` | 无 whitespace error；仅有 CRLF/LF 转换提示。 |
| `dotnet --version` | `PASS` | 当前 `global.json` 与本机实际解析版本均为 `8.0.424`。 |
| .NET Unit Tests | `PASS` / `PARTIAL` | Data.Sql 与 Dapper Core 当前双 TFM 全量回归已通过；完整解决方案测试仍未执行。 |
| Analyzer Tests | `PARTIAL` | 既有 Analyzer 专项证据已通过；本轮未重新执行完整 Analyzer 门禁。 |
| SQLite Integration | `PASS` / `PARTIAL` | 受控 SQLite 合同已在 `8.0.424` 下双 TFM 各 `1/1` 并生成当前 TRX；完整集成门禁仍未闭环。 |
| 外部 Provider Integration | `NOT EXECUTED` | 缺少专用环境、授权和安全 reset。 |
| BenchmarkDotNet | `NOT EXECUTED` | 固定 SDK 阻塞；没有本任务 raw artifact。 |

## Build/Typecheck/Lint/Format
- `dotnet build`：`PASS` / `PARTIAL`，Data.Sql 与 Dapper Core Release Build 已通过；完整解决方案 Build 未执行。
- `dotnet test`：`PASS` / `PARTIAL`，Data.Sql、Dapper Core 和受控 SQLite 合同已通过；外部 Provider 与完整 Release Gate 未执行。
- Typecheck/Analyzer：`PARTIAL`，相关源码已在 `8.0.424` 下编译；完整解决方案 Analyzer 门禁未重新执行。
- Lint/Format：`NOT EXECUTED`，未发现独立适用命令，未运行格式化器。
- `git diff --check`：`PASS`。

## 计划偏差
- 计划要求的真实 Provider Contract、Oracle/SQL Server 深度集成、Cancellation/Procedure 矩阵、FormalHost Benchmark 和 Release Gate 未在缺少环境时伪造完成，按 `PARTIAL`/`BLOCKED` 记录。
- 为测试真实运行时虚方法覆盖，`NativeDbConnection` 改用 `BeginDbTransactionAsync` override；ValueTask Rollback 使用独立最小 `IDbTransaction` 替身，避免不同返回类型的隐藏成员歧义。
- 未修改 `global.json`，未使用本机 SDK 替代仓库固定 SDK。

## 基线问题
- 工作树在任务开始时已有未提交修改；本轮未回退、重置或清理，并在其上继续实现。
- 历史任务报告和 Benchmark 制品不作为本任务当前测试计数或性能 Delta。

## 已知问题
- `SqlTransactionAsyncAdapter` 的 Provider async member 识别仍需固定 SDK 编译、驱动版本和真实 Provider 运行验证。
- Profile flags 当前是静态能力声明，完整的运行时 Profile mismatch/Implementation Gap 结构化错误模型尚未落地。
- `PublicAPI.Unshipped.txt` 尚未通过 Public API Analyzer 实际验证。
- 无本任务同 key before/after Benchmark 原始数据，性能结论为 `NOT COMPARABLE`。

## 风险与回归关注点
- 重点复核 `DbConnection.BeginTransactionAsync` 基类包装、`BeginDbTransactionAsync` 覆盖、事务 Commit/Rollback 的 Task/ValueTask 边界。
- 固定 SDK 后验证成功、失败、回滚和诊断关闭路径中 `ExecutionMode` 是否稳定保留。
- optional SPI 属于隐藏但公开的 Provider 扩展边界，需要第三方 Consumer 编译合同和实际自定义 Clause 运行测试。
- 在真实 Provider、Analyzer、完整测试和 Benchmark 证据补齐前，本报告阻塞发布准入。

## Reviewer 注意事项
- 审查 `SqlTransactionScopeFactory.IsProviderAsyncMember` 的覆盖判断和官方驱动反射证据是否一致。
- 审查五个 optional SPI 是否最小且 `SqlLambdaQueryCore` 无相关具体 Clause 强转。
- 审查 `PublicAPI.Unshipped.txt`、XML 文档、追溯方法名和源码实际方法是否一致。
- 严格区分 Unit 替身、静态 Profile、编辑器诊断和真实 Provider 集成证据。

## Git 状态
- 保留当前工作树未提交修改。
- 未自动执行 `git add`。
- 未自动执行 `git commit`。
- 未自动执行 `git push`。
- 未自动创建 PR、tag 或 release。
- 未执行 reset、clean 或其他破坏性 Git 操作。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理 Reviewer 标记为 `MUST_FIX` 的 FIX-001 至 FIX-004；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Transaction.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/Factories/SqlFactoryTest.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
- 根因：Provider Profile 的三项 native async transaction flags 已声明，但此前未进入 Begin、Commit、Rollback 决策链。
- 修复：事务 Scope、Query-owned transaction 和 Async Adapter 现在传递 Provider Profile 与 Provider Key；声明 native 且运行时成员存在时报告 `NativeAsync`，声明非 native 时执行 `SynchronousFallback`，声明 native 但成员缺失时报告 `ProviderImplementationGap/ProfileMismatch`，缺失 Profile 时 fail-closed。
- 验证：
	- `SqlFactoryTest` 已增加三阶段 native、fallback、mismatch、missing profile、取消和 Task/ValueTask 直接测试源码。
	- SQL Server 测试替身已按正式 SQL Server Profile 的同步回退声明修正 Begin/Commit/Rollback 计数与 Scope 生命周期断言。
	- 相关文件编辑器诊断：`PASS`。
	- 固定 SDK Unit/SQLite 执行：`NOT EXECUTED`，本机缺少 `8.0.405`。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/ISqlMultiSourceClauseContracts.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/SelectClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/GroupByClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/OrderByClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
	- `framework/tests/Bing.Data.Sql.CustomProvider.Tests/Samples/CustomSqlBuilder.cs`
	- `framework/tests/Bing.Data.Sql.CustomProvider.Tests/CustomProviderBuilderTest.cs`
	- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
	- `framework/tests/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- 根因：Lambda 多源路径依赖具体 Clause 实现，缺少第三方程序集边界的可实现、可编译和可执行证据。
- 修复：五个 optional Lambda 多源 SPI 已由核心路径消费；Custom Provider 使用独立 wrapper 实现 From、Select、GroupBy、OrderBy、Join；增加完整多源 Lambda SQL、Clone 保留 SPI 和 Roslyn 第三方 Consumer compile contract；追溯表已登记准确方法名。
- 验证：
	- `CustomProviderBuilderTest.Lambda_WhenCustomProviderClausesAreUsed_ShouldRenderCompleteMultiSourceSql`、`CustomLambdaClauses_WhenBuilderIsCloned_ShouldPreserveAllOptionalSpi` 和 `SqlOperationCompileContractTest.LambdaMultiSourceSpi_WhenConsumedByThirdPartyProvider_ShouldCompile` 已存在。
	- 相关文件编辑器诊断：`PASS`。
	- Custom Provider、Analyzer Consumer 和 Public API Analyzer 执行：`NOT EXECUTED`，受固定 SDK 阻塞。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 修复：补充正式 SQL Server Profile 的同步回退断言，增加 `NativeAsync`、`SynchronousFallback`、无事务 `null` 模式、Begin/Commit/Rollback 生命周期及诊断关闭路径的具体方法级追溯。
- 具体覆盖：
	- `ExecuteSqlAsync_WhenPrimaryReadTransactionCompletes_ShouldPublishSynchronousFallbackExecutionMode`
	- `ExecuteSqlAsync_WhenPrimaryReadTransactionFails_ShouldPublishSynchronousFallbackExecutionMode`
	- `ExecuteSql_WhenNoTransactionIsBound_ShouldPublishEmptyTransactionDiagnostics`
	- `ExecuteSql_WhenLoggerIsRegisteredButTraceIsDisabled_ShouldSkipDiagnosticsAndScope`
	- `ExecuteSql_WhenAllDiagnosticsAreDisabled_ShouldNotCreateExecutionMessage`
	- `ExecuteScalar_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`
	- `ExecuteScalarAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`
	- `StreamQuery_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`
	- `StreamQueryAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`
	- `GetCount_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql`
- 历史 Round 2 验证边界：当时 `global.json` 要求 `8.0.405`，本机仅有 `10.0.300`；固定 SDK Build、Unit、Analyzer 和 SQLite 双 TFM Integration 均未执行。编辑器诊断不能替代这些验证，因此当时本 FIX 不宣称全绿。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`BLOCKED` / `PARTIAL`
- 已完成：更新 SQL Server 替身事务语义、第三方 SPI 运行/编译合同源码及最终生产符号到测试方法的追溯映射；保留现有报告对未执行项的诚实结论。
- 仍阻塞：共享 Provider Contract runner、Oracle/SQL Server 安全集成 fixture、真实 Provider Procedure/Cancellation/资源复用矩阵、同 key FormalHost Benchmark、完整用户文档统一、严格 18 节 Final Report 和 Release Gate。
- 原因：当前环境没有仓库固定 SDK、授权的外部 Provider 测试数据库及安全 reset 条件；没有真实 TRX、Provider Gate 日志或同 key Benchmark raw artifact 可作为证据。
- 发布结论：不能标记发布准入通过；任何静态 Profile、测试替身、Skip、历史制品或编辑器诊断均不计为真实 Provider/Release Gate/Benchmark 通过。

### Round 1 汇总

- MUST_FIX：FIX-001 至 FIX-004 均已逐项处理并记录状态。
- 已完成：FIX-001、FIX-002 的源码修复和直接测试/合同补充；FIX-003 的可执行范围内测试与追溯修正。
- PARTIAL：FIX-003 的固定 SDK Build/Test/Analyzer/SQLite 验证；FIX-004 的报告、文档和本地可执行门禁闭环。
- BLOCKED：固定 SDK、真实 Provider、授权数据库、Oracle/SQL Server 集成、FormalHost Benchmark 和 Release Gate。
- 回归验证：关键修改文件编辑器诊断均为 `No errors found`；当前源码未获得固定 SDK 编译或测试通过证据。
- 下一步：交由独立 Reviewer 重新验收；本执行器不修改 `review.md`。

### Round 2

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理 Reviewer 标记为 `MUST_FIX` 的 FIX-001 至 FIX-004；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 根因：上一轮只有静态 Profile 和本地反射探测结果，缺少仓库内锁定包版本的直接 Driver API contract；固定 SDK 仍不可用。
- 修改文件：
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Bing.Dapper.SqlServer.Tests.csproj`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/OfficialProviderInstanceTest.cs`
- 修复内容：测试项目显式锁定 `MySqlConnector 2.1.2`、`Npgsql 6.0.11`、`Microsoft.Data.SqlClient 2.1.7`、`Oracle.ManagedDataAccess.Core 3.21.90` 和 `Microsoft.Data.Sqlite 6.0.4`；新增具体官方连接/事务类型的 `DeclaredOnly` 公开成员反射合同，并逐项断言 Begin、Commit、Rollback 与 Profile flags 一致。测试不建立网络连接、不读取凭据。
- 验证结果：文件编辑器诊断为 `No errors found`；独立反射基线确认锁定程序集版本和实际成员存在性。仓库 Unit/SQLite Integration 未运行，不能宣称合同测试通过。
- 历史 Round 2 环境阻塞：当时 `global.json` 要求 SDK `8.0.405`，本机仅有 `10.0.300`；固定 SDK Build、Unit 和 SQLite Integration 无法执行。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 根因：第三方合同测试此前只消费 SPI，Custom Provider 样例还残留无调用点的 `DispatchProxy`。
- 修改文件：
	- `framework/tests/Bing.Data.Sql.CustomProvider.Tests/Samples/CustomSqlBuilder.cs`
	- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 修复内容：移除 `CustomClauseProxy<TClause>` 及其反射代理代码；Roslyn 动态源码新增第三方风格的 From、Select、GroupBy、OrderBy、Join 实现，显式实现各自稳定 Clause 完整合同和五个 optional SPI，不引用具体默认 Clause 实现；同步更新追溯表方法名。
- 验证结果：相关文件编辑器诊断为 `No errors found`；源码检查确认无 `DispatchProxy` 残留。Roslyn Analyzer、Custom Provider 和 Public API Analyzer 未运行，不能宣称外部程序集编译合同通过。
- 环境阻塞：固定 SDK `8.0.405` 缺失，无法运行对应测试和 Analyzer。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 根因：公开事务诊断已有 Fallback/null 断言，但缺少使用 Profile=true 与原生异步替身产生 `NativeAsync` 的成功和错误阶段断言。
- 修改文件：
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 修复内容：新增离线 `NativeAsyncTestProvider`，声明 Begin/Commit/Rollback 原生异步能力并复用 SQL Server 方言/Builder；新增 `ExecuteSqlAsync_WhenProviderDeclaresNativeAsync_ShouldPublishNativeAsyncExecutionMode` 和 `ExecuteSqlAsync_WhenNativeAsyncTransactionFails_ShouldPublishNativeAsyncRollbackExecutionMode`，直接断言 Before/After 或 Before/Error 公共诊断载荷，以及异步 Begin/Commit/Rollback 计数。保留原有 SQL Server Fallback 与无事务 null 断言。
- 验证结果：相关文件编辑器诊断为 `No errors found`；`git diff --check` 无 whitespace error。固定 SDK Build、Unit、Analyzer 和 SQLite 双 TFM Integration 未运行。
- 环境阻塞：缺少 `8.0.405` SDK，无法生成当前源码身份的 TRX 或证明完整 Release Gate。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`BLOCKED` / `PARTIAL`
- 根因：该项要求共享 Provider Contract runner、真实 Provider/Oracle/SQL Server fixture、Procedure/Cancellation/资源复用矩阵、FormalHost Benchmark、文档、严格 Final Report 和 Release Gate，当前环境不具备固定 SDK、授权外部数据库和安全 reset 条件。
- 修改文件：
	- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/execution.md`
- 修复内容：记录本回合已完成的离线合同测试补强和未完成项，明确不把静态 Profile、本地替身、历史制品或编辑器诊断计为真实 Provider、Benchmark 或 Release Gate 证据。
- 验证结果：未伪造 Shared Contract runner、六态 Capability Matrix、真实 Procedure/Cancellation/资源复用、Benchmark raw artifact 或 Release Gate 通过结果；发布准入继续阻塞。
- 环境阻塞：固定 SDK、外部 Provider 授权环境、可安全清理的测试数据库和同 key FormalHost 执行证据均缺失。

### Round 2 汇总

- MUST_FIX：FIX-001 至 FIX-004 均已按当前环境可执行范围处理并记录。
- 已完成：锁定官方驱动依赖和直接反射合同源码、删除无用代理、第三方 Clause 完整实现合同源码、NativeAsync 公共诊断测试源码。
- PARTIAL/BLOCKED：固定 SDK 下的 Build、Unit、Analyzer、SQLite Integration、真实 Provider/Release Gate、Benchmark 和完整报告闭环。
- 回归验证：变更文件编辑器诊断均为 `No errors found`；`git diff --check` 无 whitespace error；未获得固定 SDK 测试通过证据。
- 下一步：交由独立 Reviewer 再次验收；本执行器不修改 `review.md`。

### Round 3

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理 Reviewer 标记为 `MUST_FIX` 的 FIX-001 至 FIX-004；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。
- 执行环境：工作树仍缺少 `global.json`，`dotnet --version` 为 `10.0.300`；未恢复该用户既有删除，也未回滚 `Bing.All.sln` 的既有修改。以下测试均明确记录实际 SDK 环境，不作为仓库固定 SDK `8.0.405` 的替代证明。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
	- `framework/src/Bing.Dapper.PostgreSql/Bing/Data/Sql/Builders/PostgreSqlSqlProvider.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/OfficialProviderInstanceTest.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Bing.Dapper.SqlServer.Tests.csproj`
- 根因：`ISqlMultiSourceFromClause.AppendRoot` 的 Public API 基线漏记 `System.Type` 参数名，Analyzer 同时报告 `RS0016` 和 `RS0017`；Dapper Core 事务文件缺少 `Bing.Data.Sql.Builders` 引用；PostgreSQL `Npgsql 6.0.11` 直接声明带隔离级别的 `BeginTransactionAsync`，原 Profile 误标为 false。
- 修复：将 Unshipped 条目修正为 `AppendRoot(System.Type entityType, string alias = null, string schema = null)`；补齐事务能力类型命名空间；将 PostgreSQL Begin native async flag 与锁定驱动声明同步为 true；Oracle 反射类型使用全局命名空间，避免测试命名空间遮蔽。
- 验证：
	- `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release --no-restore -nologo -v minimal`：PASS；成功，57 条既有警告，无新增 `RS0016`/`RS0017`。
	- `dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~OfficialProviderInstanceTest|FullyQualifiedName~SqlServerRoutingAndExecutionTest"`：PASS，`374/374`，net6.0 与 net8.0。
	- 结果包含官方 MySQL、PostgreSQL、SQL Server、Oracle、SQLite 锁定类型的 `DeclaredOnly` 反射合同；未建立网络连接或读取凭据。
- 未完成边界：尚未在仓库固定 SDK `8.0.405` 下复验；当前通过结果来自工作树实际解析的 SDK `10.0.300`。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
	- `framework/tests/Bing.Data.Sql.CustomProvider.Tests/Samples/CustomSqlBuilder.cs`
	- `framework/tests/Bing.Data.Sql.CustomProvider.Tests/CustomProviderBuilderTest.cs`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/Factories/SqlFactoryTest.cs`
- 根因：Public API Gate 修复后，动态第三方合同、Custom Provider 样例和事务测试夹具继续暴露与当前公共接口不一致的命名空间、泛型约束、表达式构造、查询入口、内部覆写签名及替身构造调用。
- 修复：补齐动态合同所需的 `Metadata`、`Bing` 引用；使第三方 GroupBy、OrderBy、Select 泛型约束与接口准确一致；使用强类型表达式树调用 Lambda SPI；修正 Custom Provider 多源 Clone 测试入口和 GroupBy/OrderBy 对象数组；同步 JoinClause 当前 `CommitParameterManager` 签名及 Dapper Core 测试替身构造函数。
- 验证：
	- `dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release --no-restore`：PASS，`32/32`；TRX：`artifacts/test-results/release-hardening-round3/round3-analyzers-full.trx`。
	- `dotnet test .\framework\tests\Bing.Data.Sql.CustomProvider.Tests\Bing.Data.Sql.CustomProvider.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~CustomProviderBuilderTest"`：PASS，`42/42`，net6.0 与 net8.0；TRX：`artifacts/test-results/release-hardening-round3/round3-custom-provider-pass3.trx`。
	- 第三方 Clause/SPI 单项 TRX：`artifacts/test-results/release-hardening-round3/round3-analyzers-spi-pass6.trx`，`1/1` PASS。
	- `DispatchProxy` 生产测试样例残留已删除；第三方实现不依赖默认具体 Clause 类型。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
	- `framework/src/Bing.Dapper.PostgreSql/Bing/Data/Sql/Builders/PostgreSqlSqlProvider.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/Factories/SqlFactoryTest.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/OfficialProviderInstanceTest.cs`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`
- 修复：完成 Profile-aware Begin/Commit/Rollback 调用链、native async 与 synchronous fallback 执行模式记录、Profile mismatch fail-fast、SQL Server NativeAsync 成功/失败回滚诊断测试，以及相关测试替身和并发释放测试的可编译修正。
- 验证：
	- `dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release --no-restore`：PASS，`300/300`，net6.0 与 net8.0；事务专项 `92/92`；TRX：`artifacts/test-results/release-hardening-round3/round3-dapper-core-full.trx`。
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore`：PASS，`2536/2536`，net6.0 与 net8.0；受影响筛选 `90/90`；TRX：`artifacts/test-results/release-hardening-round3/round3-data-sql-full.trx`。
	- SQL Server 官方合同及 NativeAsync/Fallback 诊断：`374/374` PASS，net6.0 与 net8.0。
	- SQLite Integration 完整项目：net6.0 `151/151` PASS，net8.0 `151/151` PASS；TRX：`round3-sqlite-full-net6.trx`、`round3-sqlite-full-net8.trx`。
	- net8.0 SQLite 测试保留既有 `NETSDK1206` RID 警告；未使用 `NoWarn` 隐藏。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`BLOCKED` / `PARTIAL`
- 已完成：Public API Gate 解除；Provider 官方类型合同、第三方 Clause 编译合同、Custom Provider 行为、事务 Adapter、SQL Server NativeAsync/Fallback/null 诊断和 SQLite 双 TFM 本地真实集成均取得当前源码身份的通过 TRX。
- 仍未完成：共享 Provider Contract runner、统一六态 Capability Matrix、Oracle/SQL Server 授权集成 fixture、外部 Provider Procedure/Cancellation/资源复用、同 key FormalHost before/after Benchmark、用户文档统一、严格 18 节 Final Report 以及 Release Gate。
- 原因：当前环境没有可确认授权的外部数据库、安全 reset 条件、仓库固定 SDK `8.0.405` 和本任务同 key Benchmark 原始制品；不把本地替身、静态 Profile、Skip、历史制品或现有报告当作真实发布级证据。
- 发布结论：发布准入继续阻塞；不能将 FIX-004 标记为完成。

### Round 3 汇总

- FIX-001：`PARTIAL`；Public API `RS0016/RS0017` 已修复并在当前 SDK 下通过 Data.Sql Build 和 SQL Server 专项，但固定 SDK 复验仍缺失。
- FIX-002：`COMPLETED`；Analyzer、第三方 SPI、Custom Provider 和相关全量单元回归已取得当前源码身份的通过结果。
- FIX-003：`COMPLETED`；Dapper Core、Data.Sql、SQL Server 专项和 SQLite 双 TFM 集成均通过。
- FIX-004：`BLOCKED` / `PARTIAL`；发布级共享 Contract、外部 Provider、Benchmark、文档和 Release Gate 未闭环。
- 回归验证：`git diff --check` PASS，无 whitespace error；相关文件编辑器诊断均为 `No errors found`；`review.md` 无 Git Diff。
- 工作树保护：保留 `D global.json` 和 `M Bing.All.sln` 等未裁决变化；未执行 commit、push、PR、tag、release、reset 或 clean。
- 下一步：交由 `code-reviewer` 再次验收；本执行器不修改 `review.md`。

### Round 4

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：处理 Reviewer 仍标记为 `MUST_FIX` 的 `FIX-001` 和 `FIX-004`；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。
- 执行时间：`2026-09-02T11:01:28.3844608+08:00`

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 修改文件：
	- `global.json`
	- `artifacts/test-results/unit-test-report.md`
	- `artifacts/test-results/integration-test-report.md`
	- `artifacts/benchmarks/benchmark-report.md`
- 根因：Round 3 复审发现仓库固定 SDK 文件处于删除状态，导致通过结果只能代表 SDK `10.0.300`，不满足计划声明的 SDK `8.0.405` 固定语义。
- 修复：恢复 `global.json` 的仓库声明：`8.0.405`；不修改为本机可用的 `10.0.300`，不删除该文件，也不绕过 SDK 选择机制。同步报告，将 Round 4 固定 SDK验证和 Benchmark 标记为 `BLOCKED`，并明确 Round 3 TRX 属于既有当前源码身份、非 Round 4 固定 SDK 复验。
- 验证：
	- `global.json`：确认内容要求 SDK `8.0.405`。
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release --no-restore --nologo`：`BLOCKED`；本机仅安装 SDK `10.0.300`，缺少 `8.0.405`。
	- `dotnet --version`：`BLOCKED`；同上，未使用其他 SDK 替代验证。
	- 固定 SDK 下的 Build、Unit、Analyzer、Integration 和 Benchmark：未执行，不能宣称通过。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunner.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
	- `docs/testing/database-integration-tests.md`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `artifacts/test-results/unit-test-report.md`
	- `artifacts/test-results/integration-test-report.md`
	- `artifacts/benchmarks/benchmark-report.md`
	- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
- 根因：上一轮缺少共享 Provider 合同证据模型，且正式报告没有吸收已有 SQLite 双 TFM TRX；仍不具备外部 Provider、Procedure/Cancellation/资源复用、FormalHost 和完整 Release Gate 条件。
- 修复：
	- 增加六态 `ProviderCapabilityEvidenceState`、去重的 `ProviderCapabilityMatrix` 和无密 Markdown 输出。
	- 增加最小组合式 `ProviderContractRunner`；真实执行委托完成后才创建 `RealIntegrationProven`，固定状态禁止伪造该状态。
	- 将 SQLite Scalar 和预取消真实场景接入 runner；保留 Round 3 net6.0/net8.0 独立 TRX作为既有证据，Round 4 历史上新增入口待固定 SDK 可用后执行。
	- 追溯共享模型、runner、SQLite 合同测试方法，并在集成测试文档中说明六态和安全 gate 边界。
	- 修正 Unit、Integration、Benchmark 报告；Final Report 更新为当前 `PARTIAL`、SDK 阻塞、SQLite 既有 TRX、外部 Provider 未执行和性能 `NOT_COMPARABLE` 的一致结论，并明确计划要求的严格 18 节最终报告尚未完成。
- 验证：
	- `get_errors`：共享模型、runner、runner 单元测试和 SQLite 合同测试均为 `No errors found`。
	- Round 3 既有 TRX核对：Analyzer `32/32`、Custom Provider `21/21` 单文件（双 TFM执行记录 `42/42`）、Dapper Core `150/150` 单文件、Data.Sql `1268/1268` 单文件、SQLite net6.0 `151/151`、SQLite net8.0 `151/151`；未将单文件计数冒充双 TFM新执行。
	- Round 4 历史记录：新增 `Bing.Test.Shared` runner 测试和 SQLite 合同入口尚未执行，原因是固定 SDK `8.0.405` 缺失；Round 5 已在实际 SDK `8.0.424` 下执行并生成当前 TRX。
	- 外部 Provider、Oracle 安全 fixture、SQL Server 深度真实合同、Procedure/Cancellation/资源复用矩阵、FormalHost before/after、完整 18 节 Final Report 和 Release Gate：仍为 `BLOCKED` / `NOT_COMPARABLE`。

### Round 4 汇总

- `FIX-001`：`PARTIAL` / `BLOCKED`；已恢复固定 SDK 声明，固定 SDK复验等待本机安装 `8.0.405`。
- `FIX-004`：`PARTIAL` / `BLOCKED`；已建立最小共享六态模型和 runner、接入 SQLite、同步报告/文档/追溯；完整 Provider矩阵、外部真实证据、FormalHost、严格 18 节 Final Report 和 Release Gate 未完成。
- 回归验证：编辑器诊断无错误；Round 3 TRX事实已核对；固定 SDK命令按要求诚实阻塞；未使用 SDK10 结果作为固定 SDK证明。
- 发布结论：继续 `PARTIAL`，发布准入阻塞；不能标记 `FIX-004` 或本任务为完成。
- 下一步：交由独立 `code-reviewer` 再次验收；本执行器不修改 `review.md`。

### Round 5

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：处理 Reviewer 标记为 `MUST_FIX` 的 `FIX-001` 和 `FIX-004`；保留历史 Round 1–4 记录，未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。
- 执行时间：`2026-09-02T13:28:24+08:00`

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 修改文件：
	- `global.json`
	- `artifacts/test-results/unit-test-report.md`
	- `artifacts/test-results/integration-test-report.md`
	- `artifacts/benchmarks/benchmark-report.md`
	- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
	- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/progress.md`
- 根因：Reviewer 发现 `global.json` 再次处于删除状态，实际 SDK 漂移到 `10.0.400`，与计划声明的固定 SDK `8.0.405` 不一致。
- 修复：恢复唯一仓库 SDK策略文件并声明 `8.0.405`；同步所有本轮报告，明确本机安装列表、实际解析 SDK `8.0.424` 和精确 SDK缺失，不以补丁版本冒充固定 SDK证据。
- 验证：
	- `global.json` 存在且声明 `8.0.405`：PASS。
	- `dotnet --version`：实际 `8.0.424`，精确 `8.0.405` 未安装：BLOCKED。
	- 固定 SDK下完整 Build/Test/Analyzer/Benchmark：BLOCKED，未绕过配置执行。

#### FIX-004

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`PARTIAL` / `BLOCKED`
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunner.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
	- `docs/testing/database-integration-tests.md`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `artifacts/test-results/unit-test-report.md`
	- `artifacts/test-results/integration-test-report.md`
	- `artifacts/benchmarks/benchmark-report.md`
	- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
- 根因：上一轮 runner 允许任意成功委托生成 `RealIntegrationProven`，且 Matrix 未形成机器制品；报告、TRX和 XML 警告未闭环。
- 修复：
	- 使执行委托与固定状态互斥；无完整真实集成元数据的成功委托降级为 `UnitProven`。
	- 为 `RealIntegrationProven` 增加 Provider/数据库/驱动版本、真实连接类别、测试方法、TRX、artifact、开始/完成 UTC 时间和源码身份；路径拒绝连接信息，`TestGenerated` 不满足 `ReleaseReady`。
	- SQLite Scalar 与预取消合同在真实本地 SQLite 上生成双 TFM JSON Matrix 和独立 TRX；两份 Matrix 均为 `ReleaseReady=false`。
	- 补齐 `SqlTransactionScopeFactory` 新增参数 XML 文档，Dapper Core 当前构建为 `0 warning/0 error`。
	- 同步 Unit/Integration/Benchmark/Final Report、progress 和追溯文档，不把静态 Profile、替身、Skip或历史制品当作发布级真实证据。
- 验证：
	- `Bing.Test.Shared` Provider runner：net6.0 `6/6`、net8.0 `6/6`，独立 TRX已生成：`artifacts/test-results/provider-capability/runner/net6/provider-runner-net6.trx`、`artifacts/test-results/provider-capability/runner/net8/provider-runner-net8.trx`。
	- SQLite 合同：net6.0 `1/1`、net8.0 `1/1`，独立 TRX已生成：`artifacts/test-results/provider-capability/sqlite/net6/sqlite-contract-net6.trx`、`artifacts/test-results/provider-capability/sqlite/net8/sqlite-contract-net8.trx`。
	- Matrix：`artifacts/test-results/provider-capability/sqlite-contract-net6.0.json`、`artifacts/test-results/provider-capability/sqlite-contract-net8.0.json` 均存在，含版本/连接类别/方法/TRX/artifact/时间/源码身份，无凭据，`ArtifactKind=TestGenerated`，`ReleaseReady=false`。
	- Dapper Core Release Build：PASS，`0 warning/0 error`。
	- 外部 Provider、Procedure/Cancellation/资源复用完整矩阵、授权安全 reset、FormalHost 同 key before/after和完整 Release Gate：BLOCKED / NOT EXECUTED / NOT COMPARABLE。

### Round 5 汇总

- MUST_FIX：`FIX-001`、`FIX-004` 均已按当前环境可执行范围处理。
- 已完成：固定 SDK 配置恢复；Provider 证据模型收紧；SQLite 机器 Matrix/TRX接入；XML 参数警告修复；四类报告、progress和追溯同步。
- PARTIAL：`FIX-001` 精确 SDK `8.0.405` 安装和全量复验；`FIX-004` 外部 Provider、完整 Matrix、FormalHost和 Release Gate。
- BLOCKED：精确固定 SDK、授权外部数据库和安全 reset、完整 Procedure/Cancellation/资源复用合同、同 key Benchmark。
- FAILED：无。
- 回归验证：核心文件编辑器诊断 `No errors found`；Dapper Core `0 warning/0 error`；runner/SQLite专项通过；`git diff --check`待最终收口复核。
- Review 文件保护：`review.md` 未修改，仍保留 Reviewer 的 `NEEDS_FIX` 原始结论。
- 发布结论：`PARTIAL` / `BLOCKED`，不能标记 Release Gate 或任务完成。
- 下一步：交由独立 `code-reviewer` 再次验收。

### Round 6

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理当前 Reviewer 标记为 `MUST_FIX` 的 `FIX-001`；保留历史 Round 1–5 记录，未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。
- 执行时间：`2026-09-02T15:00:00+08:00`

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：SQLite 合同元数据把 `TrxPath` 写为 `artifacts/test-results/provider-capability/sqlite-contract-{targetFramework}.trx`，与实际测试命令的 `--results-directory` 子目录和 `LogFileName` 不一致。
- 修复：为 `net6.0` 和 `net8.0` 增加显式路径映射，分别写入：
	- `artifacts/test-results/provider-capability/sqlite/net6/sqlite-contract-net6.trx`
	- `artifacts/test-results/provider-capability/sqlite/net8/sqlite-contract-net8.trx`
	同时保留 Matrix JSON 的 `ArtifactPath`、`ArtifactKind=TestGenerated` 和 `ReleaseReady=false`；测试内增加 Matrix 条目结构、artifact 路径、源码身份和非模板 TRX 路径断言。
- 验证：
	- 隔离输出编译 SQLite 合同测试 `net8.0`、`net6.0`：PASS；未覆盖现有被锁定的默认测试程序集输出。
	- SQLite 合同 `net8.0`：`1/1` PASS；TRX：`artifacts/test-results/provider-capability/sqlite/net8/sqlite-contract-net8.trx`。
	- SQLite 合同 `net6.0`：`1/1` PASS；TRX：`artifacts/test-results/provider-capability/sqlite/net6/sqlite-contract-net6.trx`。
	- 独立 Matrix/TRX 解析：两份 Matrix 的每个条目均解析到存在的 TRX 和 JSON；TRX 均为 `total=1`、`passed=1`、`failed=0`、`notExecuted=0`，并包含 `ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence`；源码身份为 `1.0.0+ee4688dedf3ef7c11efbd78989c596aac5f6529b`。
	- Matrix 安全与发布边界：无连接信息；`ArtifactKind=TestGenerated`；`ReleaseReady=false`。
	- `review.md`：未修改。

### Round 6 汇总

- MUST_FIX：`FIX-001` 已完成，Matrix 的 SQLite 双 TFM `TrxPath` 已与实际 TRX 制品一致。
- 已完成：修正路径映射、重新生成双 TFM Matrix/TRX、完成逐项可追溯性验收和最小编译验证。
- PARTIAL：精确 SDK `8.0.405` 仍未安装；完整 Release Gate、外部 Provider、完整六态矩阵、FormalHost Benchmark 仍未完成。
- BLOCKED：固定 SDK下的全量门禁、授权外部数据库和安全 reset、完整 Procedure/Cancellation/资源复用合同。
- FAILED：无。
- 回归验证：目标 C# 文件编辑器诊断 `No errors found`；双 TFM合同 `1/1` 通过；独立 Matrix/TRX验证通过；`review.md` 未修改。
- 发布结论：仍为 `PARTIAL` / `BLOCKED`，本轮不代表 Reviewer 已通过或 Release Gate 已通过。
- 下一步：交由独立 `code-reviewer` 再次验收。

### Round 7（Review Round 7 修复）

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理当前 Reviewer 标记为 `MUST_FIX` 的 `FIX-001`；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
	- `eng/ci/Invoke-SqliteContractTests.ps1`
	- `docs/testing/database-integration-tests.md`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `artifacts/test-results/integration-test-report.md`
	- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
- 根因：SQLite 合同测试在测试程序集内固定返回 canonical TRX 路径；VSTest 的实际 `--results-directory` 与 `LogFileName` 由外部命令决定，导致 Matrix 可在新结果目录生成但仍引用历史 TRX。
- 修复：
	- `SqliteExecutionIntegrationTest` 现在必须读取受控的结果目录、TRX 文件名和 Matrix 文件名环境变量；结果目录限定在工作区 `artifacts/test-results` 下，文件名限定为单层 `.trx`/`.json` 文件，并拒绝越界、缺失目录和已存在制品。
	- `Invoke-SqliteContractTests.ps1` 使用同一 `RunName` 派生 VSTest `LogFileName` 和 Matrix 文件名，将两者写入同一隔离结果目录；测试完成后读取实际 TRX 与 Matrix，校验路径、`total=1/passed=1/failed=0/notExecuted=0`、目标测试方法、源码身份、UTC 时间窗口、`TestGenerated`、`ReleaseReady=false` 和无敏感连接字段。
	- 集成测试文档、追溯表、Integration Report 和 Final Report 已改为描述受控运行上下文，不再把固定历史 canonical TRX 作为当前执行制品。
- 验证：
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release --no-restore -f net8.0 --filter FullyQualifiedName~ProviderContractRunnerTest`：`6/6` PASS；TRX：`artifacts/test-results/review-fix-round8/runner/net8/review-fix-round8-provider-runner-net8.trx`。
	- 同一 runner net6.0：`6/6` PASS；TRX：`artifacts/test-results/review-fix-round8/runner/net6/review-fix-round8-provider-runner-net6.trx`。
	- `Invoke-SqliteContractTests.ps1` 第一组隔离结果目录：net8.0 `1/1`、net6.0 `1/1`；目录前缀为 `artifacts/test-results/review-fix-round8-c/sqlite/`。
	- `Invoke-SqliteContractTests.ps1` 第二组隔离结果目录：net8.0 `1/1`、net6.0 `1/1`；目录前缀为 `artifacts/test-results/review-fix-round8-e/sqlite/`。
	- 四次 SQLite 运行均生成同目录 Matrix/TRX；Matrix 两个条目均为 `RealIntegrationProven`，TRX 均包含目标合同方法且计数为 `total=1/passed=1/failed=0/notExecuted=0`，源码身份为 `1.0.0+ee4688dedf3ef7c11efbd78989c596aac5f6529b`，`ArtifactKind=TestGenerated`，`ReleaseReady=false`。
	- 脚本安全回归：已存在制品拒绝 PASS；目录穿越输入拒绝 PASS；Matrix 敏感字段扫描 PASS。
	- 目标 C# 文件编辑器诊断：`No errors found`；`git diff --check`：无 whitespace error。
	- `review.md` SHA-256 在本轮保持 `23B85E08547410B877CBE0B52423ED1FDB75E7E1A7D46A58BAB60F4BC9E0C792`，`git diff --name-only -- review.md` 为空。

### Round 7 汇总

- MUST_FIX：`FIX-001` 已完成同次 Matrix/TRX 运行上下文绑定和双 TFM 隔离回归。
- 已完成：移除固定 canonical TRX 假设；统一结果目录、TRX 文件名和 Matrix 文件名；增加 post-test 制品校验、路径安全校验和文档/报告同步。
- PARTIAL：仓库固定 SDK `8.0.405` 仍未安装，本轮验证使用实际 SDK `8.0.424`；完整 Release Gate、外部 Provider、FormalHost Benchmark 和全量固定 SDK 门禁未完成。
- BLOCKED：精确固定 SDK、授权外部数据库和安全 reset、完整 Procedure/Cancellation/资源复用合同。
- FAILED：无。
- Review 文件保护：`review.md` 未修改，仍保留 Reviewer 的 `NEEDS_FIX` 原始证据。
- 发布结论：Executor 已完成当前 `MUST_FIX` 修复，但任务继续保持 `PARTIAL` / `BLOCKED`，不代表 Reviewer 已通过或 Release Gate 已通过。
- 下一步：交由 `code-reviewer` 再次验收。

### Round 8（REL-P6-02 / REL-P8-01 收口）

- Review 状态：`NEEDS_FIX`；本轮不修改 `review.md`，保留 Reviewer 原始证据。
- 执行范围：继续处理 `REL-P6-02` 能力失败分类和 `REL-P8-01` 职责审计；同步验证结果，不重新规划整体方案。

#### REL-P6-02

- `SqlCapabilityFailureReason` 继续保持四类结构化原因：`DatabaseUnsupported`、`ProviderImplementationGap`、`ProviderProfileMissing`、`ProviderProfileMismatch`。
- `SqlProviderCapabilityResolver.HasCompleteProfile(...)` 已用于识别声明但不完整的 Profile；查询能力、Returning、Update From、Delete Using、Multi Row Values 和原生 SQL 分页路径不再把不完整 Profile 误报为实现缺口。
- `ValuesClause.Validate(...)` 现在显式拒绝 null context，并按 Profile 未声明、不完整、已声明但关闭分别分类；`DeleteClause`、参数限制和查询能力读取路径对缺失能力域保持安全默认，不触发空引用。
- 执行态仍使用 Required Profile；描述/构建兼容路径保留 `GetProfile(...)`，但不把其默认空 Profile 作为执行通过条件。
- 新增直接测试：
	- `ValuesClause_WhenProviderProfileIsIncomplete_ShouldClassifyProfileMismatch`
	- `ToSql_WhenProviderProfileIsIncomplete_ShouldClassifyProfileMismatch`
	- `TryGetReason_WhenExceptionDoesNotContainValidReason_ShouldReturnFalse`
	- 只读数据源同步/异步写入、批量、事务路径补充 `DatabaseUnsupported` reason 断言。

#### REL-P8-01

- 完成职责审计，未进行机械拆分或无证据的文件移动：
	- `SqlBuilderBase` 已使用 partial，查询渲染、参数、过滤、校验和快照已有边界；继续拆分会扩大回归面。
	- `SqlQueryBase` 已按生命周期、执行、事务、流式等职责拆分；本轮只收紧 Profile 执行入口。
	- `SqlExecutorBase` 已按实体 Mutation、批量规划/执行等职责使用 partial；未发现安全且内聚的新拆分边界。
	- `WhereClause`、`JoinClause` 仍保持各自参数/谓词和连接拓扑内聚；`JoinClause` 的 optional SPI 与具体实现迁移已在前序回合完成。
	- `MutationClauseExtensions` 虽较大，但当前没有足够独立且可由直接测试锁定的拆分责任；保持现状以避免制造第二套扩展入口。
- 结论：public API、现有 SQL 输出和 Provider 依赖方向不因 `REL-P8-01` 发生结构性变化。

#### 本轮验证

| 验证项 | 结果 | 证据 |
| --- | --- | --- |
| Data.Sql Profile 分类专项 | `PASS` | net8.0 `11/11`；net6.0 `11/11`；包括四类 reason round-trip、ProfileMissing、ProfileMismatch、Profile 无效值读取。 |
| Dapper Core 能力门禁专项 | `PASS` | net8.0 `69/69`；net6.0 `69/69`；覆盖 Provider Profile、只读数据源、多结果集、批量 Mutation。 |
| Bing.Data.Sql Release Build | `PASS` | `0 warning/0 error`，`--no-restore`。 |
| Bing.Dapper.Core Release Build | `PASS` | `0 warning/0 error`，`--no-restore`。 |
| 编辑器诊断 | `PASS` | 本轮修改的源码和测试文件均为 `No errors found`。 |
| `git diff --check` | `PASS` | 无 whitespace error；Git 仅报告既有 CRLF/LF 转换提示。 |

#### 外部范围与发布结论

- SQL Server、MySQL、PostgreSQL、Oracle 真实 Provider 集成仍为 `NOT EXECUTED` / `BLOCKED`：相关 gate、专用连接变量和 `ALLOW_DATABASE_RESET_FOR_TESTS=true` 均未提供；未读取连接值、未连接数据库、未执行 reset。
- `REL-P2-03` 仍为 `PARTIAL/BLOCKED`：现有 SQL Server fixture 保留且可继续扩展，但本轮不以替身测试或静态 Profile 代替 CRUD、Batch、Transaction、Procedure、Multiple Result、Cancellation 的真实合同。
- `REL-P5-02` 仍为 `PARTIAL/BLOCKED`：MySQL 既有真实 Output/InputOutput 用例可引用；PostgreSQL、SQL Server、Oracle 未取得当前真实 Procedure 矩阵，不能标记 Proven。
- 历史 Round 1–7 曾按计划文本中的 `8.0.405` 记录固定 SDK 偏差；本轮重新读取 `global.json` 后确认当前仓库声明和实际解析版本均为 `8.0.424`。本轮结果是当前源码身份的本地回归证据。
- FormalHost 同 key before/after Benchmark、完整 Release Gate 和独立 Reviewer 复验仍未完成；任务保持 `PARTIAL`，发布准入继续阻塞。

#### 工作树与安全边界

- 未修改 `plan.md` 或 `review.md`。
- 未执行 `git add`、`git commit`、`git push`、PR、tag、release、reset、clean。
- 未新增 Secret、连接配置、数据库 schema 或生产数据操作。

### Round 9（最终本地回归）

- 当前 `global.json` 内容和 `dotnet --version` 均为 `8.0.424`；未修改用户 SDK 配置。
- `Bing.Data.Sql.Tests` 全量：net6.0 `1276/1276`、net8.0 `1276/1276`，失败/跳过均为 0。
- `Bing.Dapper.Core.Tests` 全量：net6.0 `150/150`、net8.0 `150/150`，失败/跳过均为 0。
- `Bing.Data.Sql` 与 `Bing.Dapper.Core` Release Build：均 `0 warning/0 error`。
- 通过 `eng/ci/Invoke-SqliteContractTests.ps1` 运行 SQLite 受控合同：net8.0 `1/1`、net6.0 `1/1`；Matrix/TRX 同目录绑定并通过脚本校验，当前制品位于 `artifacts/test-results/round9-sqlite/net8/` 与 `artifacts/test-results/round9-sqlite/net6/`。
- 直接 SQLite 集成项目未带受控环境变量时曾正确拒绝运行合同入口；该失败不计入测试回归，随后通过受控脚本完成合法合同运行。
- `review.md` 未修改；`git diff --check` 无 whitespace error，仅保留 Git 的 CRLF/LF 转换提示。

### 最终终态

- 任务状态：`PARTIAL`。
- `REL-P6-02` 与 `REL-P8-01` 的当前可执行范围已完成；核心本地验证通过。
- `REL-P2-03`、`REL-P5-02` 的外部 Provider 真实合同、完整六态外部 Matrix、FormalHost 同 key before/after Benchmark、完整 Release Gate 和独立 Reviewer 复验仍未完成。
- 外部数据库相关 gate、连接配置和安全 reset 条件未提供；未连接外部数据库、未读取 Secret、未执行数据库 reset。

#### 当前基线校正与最终验证

- 重新读取 `global.json` 确认仓库当前声明 SDK 为 `8.0.424`；此前 Round 1–7 中关于 `8.0.405` 的描述属于历史计划/报告偏差，本轮不修改用户当前 SDK 配置。
- 当前 SDK 解析：`8.0.424`。
- `Bing.Data.Sql.Tests` 全量：net8.0 `1276/1276`、net6.0 `1276/1276`。
- `Bing.Dapper.Core.Tests` 全量：net8.0 `150/150`、net6.0 `150/150`。
- `Bing.Data.Sql` Release Build：`0 warning/0 error`。
- `Bing.Dapper.Core` Release Build：`0 warning/0 error`。
- 受控 SQLite 合同脚本：net8.0 `1/1`、net6.0 `1/1`；当前制品分别为 `artifacts/test-results/round9-sqlite/net8/round9-sqlite-net8.0.trx`、`artifacts/test-results/round9-sqlite/net8/round9-sqlite-net8.0.json`、`artifacts/test-results/round9-sqlite/net6/round9-sqlite-net6.0.trx`、`artifacts/test-results/round9-sqlite/net6/round9-sqlite-net6.0.json`，脚本已完成路径、计数、方法名、源码身份、时间窗口和敏感字段校验。
- 此次最终 SQLite 证据仍为 `TestGenerated`、`ReleaseReady=false`；不替代外部 Provider 真实证据或完整 Release Gate。

### Review Fix Round 1（FIX-001）

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：仅处理 `FIX-001`（`MUST_FIX`）；`FIX-002`、`FIX-003` 为 `SHOULD_FIX`，按本轮明确的 `must` 范围暂不处理。
- `review.md` 未修改，未执行 commit、push、PR、tag、release、reset 或 clean。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 根因：Mutation、Execution、Procedure 和 Transaction Profile 只有布尔能力位，关闭能力的 `DatabaseUnsupported` 与 `ProviderImplementationGap` 来源无法由 Provider 声明；各 Gate 只能使用调用点硬编码原因。
- 修复：保留既有布尔属性以维持兼容，新增可选失败原因元数据并纳入 Profile 深复制。Returning、Multiple Result Sets、Stored Procedures、Output Parameters、Streaming、Cancellation 及相关 Mutation/Transaction Gate 均使用显式 Profile 原因，未声明原因时沿用原有兼容默认值；MySQL、SQLite、Oracle 的已知数据库限制显式声明 `DatabaseUnsupported`。
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/SqlProviderProfile.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/Clauses/ReturningClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/Clauses/UpdateFromClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/Clauses/DeleteUsingClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/Clauses/ValuesClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/DefaultSqlEntityMutationCommandBuilder.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/DefaultSqlParameterBinder.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlExecutorBase.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryExecutorBase.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.QueryPlan.Streaming.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Transaction.cs`
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
	- `framework/src/Bing.Dapper.MySql/Bing/Data/Sql/Builders/MySqlSqlProvider.cs`
	- `framework/src/Bing.Dapper.Sqlite/Bing/Data/Sql/Builders/SqliteSqlProvider.cs`
	- `framework/src/Bing.Dapper.Oracle/Bing/Data/Sql/Builders/OracleSqlProvider.cs`
	- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlProviderProfileTest.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/ProviderProfileExecutionGateTest.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryExecutorTest.cs`
- 验证：
	- `dotnet build framework/src/Bing.Data.Sql/Bing.Data.Sql.csproj --no-restore`：PASS，0 error；保留 57 条既有 RS0026/RS0027 警告。
	- `dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj --no-restore`：PASS，0 warning/0 error。
	- `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj --no-restore`：PASS，net6.0 `1276/1276`、net8.0 `1276/1276`。
	- `dotnet test framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj --no-restore`：PASS，net6.0 `161/161`、net8.0 `161/161`。
	- `dotnet test framework/tests/Bing.Dapper.SqlServer.Tests/Bing.Dapper.SqlServer.Tests.csproj --no-restore --filter "FullyQualifiedName~OfficialProviderInstanceTest"`：PASS，net6.0 `6/6`、net8.0 `6/6`。
	- Profile、六类 Gate 和多结果集专项：PASS，Data.Sql 双 TFM `12/12`，Dapper.Core 双 TFM `33/33`；两类 reason 均断言连接/命令未访问。
	- 编辑器诊断：PASS；`get_errors` 未发现变更源码和测试错误。
	- `git diff --check`：PASS；仅报告既有 CRLF/LF 转换提示。

### Review Fix Round 1 汇总

- `FIX-001`：`COMPLETED`，Profile 已能表达关闭能力的显式失败来源，六类指定 Gate 已完成双 reason 直接测试。
- `FIX-002`、`FIX-003`：`DEFERRED`，本轮 `fixScope=must` 明确排除 SHOULD_FIX，不修改 Reviewer 证据文件。
- 回归验证：Data.Sql、Dapper.Core 全量双 TFM及官方 Provider Profile 专项均通过。
- 发布边界：外部 Provider 真实集成、完整六态外部 Matrix、FormalHost Benchmark 和完整 Release Gate 仍保持 `PARTIAL` / `BLOCKED`，本轮不将其伪造为已完成。
- 下一步：交由 `code-reviewer` 再次独立验收。

### Review Fix Round 1（recommended）

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- 处理边界：处理 Reviewer 标记为 `SHOULD_FIX` 的 `FIX-001`、`FIX-002`；未修改 `review.md`，未执行 commit、push、PR、tag、release、reset 或 clean。

#### FIX-001

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/execution.md`
- 根因：报告顶部混合了历史 Round 的 SDK、Build 和测试状态，当前消费者无法直接识别最终执行事实。
- 修复：当前状态统一为 SDK `8.0.424`；当前 Data.Sql/Dapper Core 单元回归、受控 SQLite 合同和生产项目 Release Build 的已验证结果按当前事实记录；旧 SDK `8.0.405` 仅保留在历史 Round 说明中；外部 Provider、FormalHost Benchmark 和完整 Release Gate 继续明确为未完成或阻塞。
- 验证：
	- `global.json`：当前声明 `8.0.424`。
	- 报告顶部：当前 SDK、单元测试、Analyzer、SQLite 和 Build 状态与 Round 9 记录一致。
	- `8.0.405`：仅出现在历史 Round 记录，不再出现在当前状态表或当前执行结论中。

#### FIX-002

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
- 根因：最终报告的 Round 补充使用二级标题，导致标题数量为 20，不符合严格 18 节契约。
- 修复：将 `Round 8 FIX-001 同次运行证据` 和 `Round 8 补充` 调整为三级标题；保留所有真实失败、外部阻塞、SQLite `TestGenerated` 和 `ReleaseReady=false` 证据。
- 验证：
	- UTF-8 标题统计：二级标题 `18`。
	- `review.md` Git 差异：无。
	- `git diff --check`：通过；仅有既有 CRLF/LF 转换提示。

### Review Fix Round 1 汇总（recommended）

- `FIX-001`：`COMPLETED`，execution.md 当前事实已统一，历史 SDK 记录已限定在历史 Round。
- `FIX-002`：`COMPLETED`，最终报告二级标题已严格收口为 `18`，未删除真实阻塞证据。
- 回归验证：最终报告标题统计通过；`review.md` 未修改；`git diff --check` 通过。
- 发布边界：真实外部 Provider、FormalHost 同 key Benchmark、完整 Release Gate 和独立 Reviewer 复验仍为 `PARTIAL` / `BLOCKED`；本轮完成状态仅表示本 fixScope 的两个 SHOULD_FIX 已完成，不表示 Reviewer 或发布门禁通过。
- 下一步：交由 `code-reviewer` 再次验收。
