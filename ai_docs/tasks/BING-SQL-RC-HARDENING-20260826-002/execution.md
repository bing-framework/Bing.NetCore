<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260826-002
AI_EXECUTION_FINISHED_AT: 2026-08-26T22:22:52.7263412+08:00

# 实施执行报告

## 执行结论

任务以 `PARTIAL` 终态结束。Phase 1 的 Fluent mutation gateway、多结果集 callback 生命周期修复和 Phase 2 的 Breaking API 收敛已真实落地并通过核心验证；外部数据库、有效 FormalHost before/after、完整 CI 现代化和性能准入没有可审计环境证据，因此未将任务标记为 `COMPLETED`。

## 任务信息

- task-id: `BING-SQL-RC-HARDENING-20260826-002`
- branch: `dev_v6.0-refactor-sqlquery`
- HEAD: `5c9bc739f944a98953da597b931a6b761c012caa`
- SDK: `.NET SDK 10.0.300`
- runtime: `.NET 8.0.27`、`.NET 6.0.36`

## 计划执行情况

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Phase 0 | `completed` | 基线、消费者矩阵和历史 Benchmark 有效性裁决已记录。 |
| Phase 1 | `completed` | mutation gateway 和 CompleteAsync 双 callback 清理已实现。 |
| Phase 2 | `completed` | FromTable/ClearSelect 删除、Select 替换、Join options 和消费者迁移已完成。 |
| Phase 3 | `partial` | 核心测试及受影响项目通过；完整 Provider/CI 矩阵未完成。 |
| Phase 4 | `blocked` | 没有有效 FormalHost before/after。 |
| Phase 5 | `blocked` | 无有效 before，未进行性能优化或收益声明。 |
| Phase 6 | `partial` | 文档、ReleaseNotes、Public API 和报告已同步；目标 CI 远程执行和发布制品仍待确认。 |

## 已完成事项

- Raw Fluent 查询扩展统一通过 `SqlQueryOperationAccessor.Mutate(...)`，成功变更触发查询缓存失效；空白原始追加和 false 条件保持 no-op。
- `CompleteAsync()` 开始时原子清空同步、异步 completion callback；`SqlMultipleQueryResult` 构造函数收窄为 internal。
- 删除 `ISqlQuery.FromTable`、`SqlLambdaQuery.FromTable` 和高层 `SqlLambdaQuery.ClearSelect`；保留底层 Builder 的独立 `ClearSelect`。
- `Select<TEntity>(bool)` 使用原子投影替换，`AppendSelect` 保持追加语义。
- 新增仅含 `RightAlias`、`LeftAlias`、`Schema` 的 `SqlJoinOptions`，并更新 Public API、Analyzer contract、测试、Benchmark 和文档消费者。
- 新增 Raw Fluent 缓存失效和空白 no-op 回归；迁移 SQLite 元数据连续 Join 矩阵及 SQL Server/Benchmark 调用。

## 部分/未完成事项

- 未建立独立源码身份、独立输出和同一 Job 的有效 FormalHost before/after。
- 未执行需要安全数据库连接和 Gate 的 MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实集成矩阵。
- `appveyor.yml` 已更新为 Visual Studio 2022，并加入固定 SDK、Benchmark smoke 和制品收集配置；远程作业仍未执行。
- 多结果集 WeakReference/异常聚合/SQLite 取消边界以及完整 mutation family 直接回归仍需后续补齐。

## 修改文件

生产源码涉及 `Bing.Data.Sql` mutation gateway、Raw Fluent extensions、Lambda query API、Join options、Public API 基线，以及 `Bing.Dapper.Core` 多结果集生命周期。测试涉及 Data.Sql、Analyzer、SQLite、SQL Server、MySQL、Benchmark；文档涉及 SQL 查询指南、设计/追溯文档和 ReleaseNotes。完整文件清单以当前 `git status --short` 为准。

## API/数据/配置变化

- Breaking API：删除高层 `FromTable`、高层 `ClearSelect`；类型化实体 Join 的左 alias/schema 改用 `SqlJoinOptions`。
- 无数据库 schema 或生产数据变更。
- 未新增 production `InternalsVisibleTo`。
- CI 配置已修改；`global.json` 固定已验证的 .NET SDK 10.0.300，AppVeyor 远程兼容性和实际制品上传仍作为 blocked 风险记录。

## 测试结果

- Data.Sql Unit net8：1251 passed，0 failed，0 skipped。
- Data.Sql Analyzer Unit net8：27 passed，0 failed，0 skipped。
- Dapper Core Unit net8：131 passed，0 failed，0 skipped。
- SQLite Unit net8：112 passed，0 failed，0 skipped。
- SQLite Integration build：通过，0 warnings，0 errors。
- SQL Server Tests build：通过，0 warnings，0 errors。
- Data.Sql Benchmarks build：通过，0 warnings，0 errors。

## Build/Typecheck/Lint/Format

- 受影响项目 Release build/test 已通过。
- `git diff --check` 通过；Git 仅输出工作树换行转换提示。
- 未执行独立格式化工具；未发现适用的额外 lint/typecheck 命令。
- 完整 `Bing.All.sln` Release build 本轮重新通过，0 errors、79 warnings；警告主要来自 net6.0 EOL 和 .NET 10 包对 net6.0 的支持提示。

## 计划偏差

计划要求继续完成完整 CI、外部 Provider、FormalHost 和性能矩阵；当前环境未提供安全连接、CI 目标镜像和有效 before provenance。按计划将这些项目标记为 `blocked`，没有使用历史 partial artifact 或弱断言替代。

## 基线问题

前序 Round 3-10 FormalHost artifact 不完整或 invalid，不能作为本任务 before。当前 benchmark-baseline.md 明确记录该裁决。

## 已知问题

AppVeyor 远程作业尚未执行，无法在本地证明 VS2022 镜像上的固定 SDK、全解决方案测试和制品上传；外部 Provider 与独立 before/after 仍 blocked。当前 `SqlQueryOperationAccessor.Mutate` 对已进入 gateway 的非空扩展统一标记变更，底层原子 clause 已保护失败状态；更完整的 no-op 分支审计仍是后续测试项。

## 风险与回归关注点

- Join options 是 Breaking API，调用方需要将原双字符串参数迁移为对象初始化。
- 外部 Provider 和 FormalHost 未验证，不能据此宣称跨数据库或性能结果。
- 多结果集异常/取消和 callback 捕获对象释放仍需补充直接证据。

## Reviewer 注意事项

- 重点审查 `SqlLambdaQuery` Join overload、`PublicAPI.Unshipped.txt` 条目和 SQLite 连续 Join 矩阵。
- 确认底层 Builder `ClearSelect` 保留与高层 API 删除边界符合计划。
- 确认没有新增 production IVT、提交机密或自动化 Git 操作。

## Git 状态

- 未自动执行 `git add`。
- 未自动执行 `git commit`。
- 未自动执行 `git push`。
- 未自动创建 PR。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮仅处理 `MUST_FIX`；`FIX-004`、`FIX-005`（`SHOULD_FIX`）保持未处理。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IUnion.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.ICte.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/SqlParameterExtensions.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：Union family、CTE 和参数扩展直接修改独立查询持有的 Builder 状态，没有统一触发查询缓存版本失效。
- 修复：新增 `MutateBuilder` 与统一 `MarkChanged` 边界；Union/UnionAll/Intersect/Except、CTE 和 `AddParam` 在有效 mutation 成功后只通知一次；空输入保持 no-op；克隆失败和参数数量上限失败在提交前退出。
- 验证：
	- `Bing.Data.Sql.Tests` net8.0：`1258/1258`，PASS。
	- `Bing.Data.Sql.Tests` net6.0：`1258/1258`，PASS。
	- 直接测试覆盖 SQL、参数、shape/cache version、缓存命中、no-op、Union/CTE 克隆失败和参数上限失败。
	- 已审计 `Extensions.ISqlBuilder`：仅接收公开 `ISqlBuilder`，不属于独立 `SqlFluentQuery` mutation 路径。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryResultLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：生产生命周期修复缺少直接的 callback retained delegate、exactly-once 和异常顺序职责级测试证据。
- 修复：增加直接构造 `SqlMultipleQueryResult` 的可控生命周期测试；在异步完成开始后验证同步、异步 callback 字段均已解除；验证异步 callback 只执行一次、租约只释放一次，并验证 callback/lease 清理异常顺序。
- 验证：
	- `Bing.Dapper.Core.Tests` net8.0：`133/133`，PASS。
	- `Bing.Dapper.Core.Tests` net6.0：`133/133`，PASS。
	- `Bing.Dapper.Sqlite.Tests` net8.0：`112/112`，PASS。
	- `Bing.Dapper.Sqlite.Tests.Integration` net8.0：`151/151`，PASS。
	- 直接测试映射已写入 `ai_docs/sql-metadata-test-traceability.md`。

#### FIX-003

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlJoinOptions.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
	- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
	- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：实体 Join 的普通 `string` 参数与高级 options 参数均为引用类型时，裸 `null` 无法在重载间消歧，且旧 API 的消费者编译负向契约缺失。
- 修复：将 `SqlJoinOptions` 收敛为值类型；四类实体 Join 保留普通 `string rightAlias = null` 与 options 两种一致入口；删除 options 空值合并；补充每类两个入口、裸 `null` 正向编译、旧 `FromTable`/高层 `ClearSelect`/旧多 string Join 负向编译契约。
- 验证：
	- `Bing.Data.Sql.Analyzers.Tests` net8.0：`29/29`，PASS。
	- `Bing.Data.Sql.Tests` net8.0/net6.0：均 PASS。
	- Data.Sql Release build：`0 errors`；既有 Public API Analyzer warnings 保持，无新增 error。
	- 直接测试映射已写入 `ai_docs/sql-metadata-test-traceability.md`。

### Round 1 汇总

- MUST_FIX：`3`
- 已完成：`3`
- PARTIAL：`0`
- BLOCKED：`0`
- FAILED：`0`
- 回归验证：Data.Sql net8/net6、Analyzer net8、Dapper Core net8/net6、SQLite Unit net8、SQLite Integration net8 全部通过；`git diff --check` 通过（仅有换行转换提示）。
- 下一步：重新进行独立 Review；`review.md` 保持 Reviewer 独立证据，不在本轮修改。

### Round 2

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮仅处理当前复审中的 `MUST_FIX`；`FIX-003`、`FIX-004`、`FIX-005` 为 `SHOULD_FIX`，按本轮范围延期。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/SqlParameterExtensions.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：`ClearParams()` 直接调用参数管理器清空操作，绕过 `SqlQueryOperationAccessor.MutateBuilder()`，导致独立 Fluent 查询的 ShapeVersion 和 SQL 缓存状态不更新。
- 修复：参数数量大于零时通过统一 Builder mutation gateway 清空参数并只触发一次缓存失效；参数已经为空时直接返回，保持版本、缓存和渲染次数不变。已复核公开参数 mutation，`AddParam` 两个入口和 `ClearParams` 均经过 gateway 或明确 no-op 分支。
- 验证：
	- `Bing.Data.Sql.Tests` net8.0：`1260/1260`，PASS。
	- `Bing.Data.Sql.Tests` net6.0：`1260/1260`，PASS。
	- `RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot`：PASS，断言完整 SQL、空参数快照、ShapeVersion、缓存版本和渲染次数。
	- `RawFluent_WhenParametersAreAlreadyEmpty_ShouldKeepQueryStateUnchanged`：PASS，断言空集合 no-op。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryResult.cs`
	- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryResultLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：既有职责测试绕过公开 `Dispose/DisposeAsync`，无法直接证明 reader 释放次数、公开链异常顺序和 callback 捕获对象释放。
- 修复：为内部结果对象增加不改变生产构造路径的 reader 生命周期委托 seam；公开 `Dispose` 和 `DisposeAsync` 现在由受控测试直接驱动。测试覆盖异步 reader/callback/lease exactly-once、同步 reader 主异常优先及 callback/lease 清理异常顺序、两个 callback 字段在完成开始时清空，以及 callback 捕获对象在异步完成后可回收。
- 验证：
	- `Bing.Dapper.Core.Tests` net8.0：`134/134`，PASS。
	- `Bing.Dapper.Core.Tests` net6.0：`134/134`，PASS。
	- `Bing.Dapper.Sqlite.Tests` net8.0：`112/112`，PASS。
	- `Bing.Dapper.Sqlite.Tests.Integration` net8.0/net6.0：`302/302`，PASS。
	- `DisposeAsync_WhenStarted_ShouldReleaseReaderCallbacksAndLeaseExactlyOnce`：PASS。
	- `DisposeAsync_WhenCompleted_ShouldReleaseCallbackCapture`：PASS。
	- `Dispose_WhenCleanupFails_ShouldPreservePrimaryAndCleanupExceptionOrder`：PASS。

### Round 2 汇总

- MUST_FIX：`2`
- 已完成：`2`
- PARTIAL：`0`
- BLOCKED：`0`
- FAILED：`0`
- 回归验证：Data.Sql、Dapper Core、SQLite Unit 和 SQLite Integration 目标测试全部通过；编辑器诊断无错误；`git diff --check` 通过，仅有既有换行转换提示。
- 未处理：`FIX-003`、`FIX-004`、`FIX-005`，因本轮 `fixScope=must` 保持延期，不改变 Reviewer 的独立结论。
- 下一步：重新进行独立 Review；本轮未修改 `review.md`，未执行 commit、push 或 PR。

### Round 3

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮仅处理当前复审中的 `MUST_FIX`；`FIX-003`、`FIX-004`、`FIX-005` 为 `SHOULD_FIX`，按本轮范围延期。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
- 根因：Round 2 测试只从 `query.GetParams()` 读取参数管理器状态，没有直接验证执行准备阶段创建的 `SqlBuilderExecutionSnapshot`，无法完整证明清空参数后的执行 SQL 与参数快照一致。
- 修复：在 `RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot` 中调用 `SqlBuilderRuntimeBridge.CreateExecutionSnapshot(query.GetBuilder())`，断言 execution snapshot 的完整 SQL 与 `ToSql()` 完全一致、参数集合为空；保留 ShapeVersion、cachedVersion、缓存渲染次数和成功/空集合 no-op 断言。同步更新生产符号到测试方法追溯和验证报告。
- 验证：
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --nologo --filter FullyQualifiedName~RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot`：`1/1`，PASS。
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --nologo --filter FullyQualifiedName~RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot`：`1/1`，PASS。
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --nologo`：`1260/1260`，PASS。
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --nologo`：`1260/1260`，PASS。

### Round 3 汇总

- MUST_FIX：`1`
- 已完成：`1`
- PARTIAL：`0`
- BLOCKED：`0`
- FAILED：`0`
- 回归验证：ClearParams execution snapshot 专项测试及 Data.Sql net8/net6 全量测试通过；本轮未修改 `review.md`。
- 下一步：重新进行独立 Review；未执行 commit、push 或 PR。

### Round 4

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮处理用户明确要求的全部 `SHOULD_FIX`；未修改 `review.md`、未执行 commit、push 或 PR。

#### FIX-003

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：Join options 的 `Schema` 没有消费者编译合同，四类 Join 的普通非空 alias/options alias 也没有集中正向覆盖。
- 修复：新增四类普通 alias、四类 `SqlJoinOptions` alias/schema 的 Roslyn 消费者编译合同；新增 `Lambda_WhenJoinOptionsSpecifySchema_ShouldRenderCompleteSql`，对完整 schema-qualified Join SQL 和 On alias 进行精确断言；保留裸 `null` 正向合同和旧 API 负向合同。
- 验证：
	- `Bing.Data.Sql.Analyzers.Tests` net8.0：`30/30`，PASS。
	- `Bing.Data.Sql.Tests` net8.0：`1261/1261`，PASS。
	- `Bing.Data.Sql.Tests` net6.0：`1261/1261`，PASS。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：Root 的 IN 参数与来源数共用参数矩阵，JoinCount 的值实际表达来源数，固定 Join 路径被无关参数重复执行，且历史 before provenance 不可复用。
- 修复：Root 主类移除 `ParameterCount`，IN 拆为 `SqlLambdaInBenchmarks`；新增 `SqlRawFromBenchmarks` 的 `SourceCount=20,50`；JoinCount 改为语义明确的 `SourceCount`，并将重复实体/失败路径移入无参数的 `SqlLambdaFixedJoinBenchmarks`。Benchmark case 列举已确认矩阵独立。
- 验证：
	- Benchmark Release build：PASS，0 errors；保留 SDK RID 兼容性警告 `NETSDK1206`。
	- Raw 20/50 Dry/FormalHost smoke：PASS；20 来源 FormalHost Mean 约 `3.336 us`，50 来源约 `5.083 us`，各有 `45` 个样本。
	- Raw 制品：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlRawFromBenchmarks-report.csv`、`-report-github.md`、`-report.html`。
	- provenance SHA-256：源码 `SqlLambdaRootBenchmarks.cs`=`F6B4B02C2A732F9034B60B652AA88EFB0E3FBA8C746722D58A3CA37FD4CD43B6`；`SqlLambdaJoinBenchmarks.cs`=`552F6045642281C71444B44026F9DA6CF675CC61EBD9BAB5929871B01499E0A0`；CSV=`D103E0CDAD0B317A6B31E8489E52EA25492B741770424E3642089CC945DE8FCE`；Markdown=`9834234CBC795510346F50102A1054BD6D4D78DEC313401AA6B947E8A6EFA130`；HTML=`1CA69EDB032381903ACFA635B9B58C86F9318AB3E264099DC59FADD5349838EC`。
	- Blocked：没有独立旧版源码身份和同 Job 的有效 before artifact，不能声称 Root/Join 性能 delta 或 RC 性能准入通过。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `appveyor.yml`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
- 根因：AppVeyor 使用 Visual Studio 2017，且未保留 TRX、coverage、PublicAPI 和 Benchmark 制品；本机也没有外部 Provider Gate 或安全连接配置。
- 修复：AppVeyor 镜像更新为 Visual Studio 2022；常规测试继续显式关闭外部 Provider Gate，并增加 TRX、Cobertura、PublicAPI 和 Benchmark 结果 artifact 收集。检查当前环境未发现任何 `RUN_*`、`ConnectionStrings__*` 或 `ALLOW_DATABASE_RESET_FOR_TESTS` 变量，因此未启动外部数据库测试。
- 验证：
	- `appveyor.yml` 配置哈希 SHA-256=`47A20CC4C21EB841D15A8F79BEFB53E9B8FEC503C5CC1AE79C665E238DEEC154`。
	- CI 配置级检查：PASS；AppVeyor 远程运行：BLOCKED，当前环境不可执行 AppVeyor。
	- MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实集成：逐项 BLOCKED，原因是安全连接和对应 `RUN_*_INTEGRATION_TESTS=true` Gate 缺失。
	- 发布/CI artifact 实际上传：BLOCKED，需 AppVeyor 远程作业；本地 Benchmark Raw 制品已生成并保存。

### Round 4 汇总

- MUST_FIX：`0`
- SHOULD_FIX：`3`
- 已完成：`1`
- PARTIAL：`2`
- BLOCKED：外部 Provider 真实执行、AppVeyor 远程运行、有效 before/after 性能比较和远程 artifact 上传。
- FAILED：`0`
- 回归验证：Analyzer net8 `30/30`；Data.Sql net8/net6 各 `1261/1261`；Benchmark Release build PASS；Raw 20/50 FormalHost smoke PASS；`git diff --check` 待最终收口复核。
- 下一步：交回独立 Review；Reviewer 继续独立维护 `review.md`。

### Round 5

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮处理 `FIX-004`、`FIX-005`；未修改 `review.md`，未执行 commit、push 或 PR。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `global.json`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaBenchmarkColumns.cs`（删除）
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
- 根因：IN 基准未覆盖完整边界且混合 values 创建与查询渲染；没有 SQLite/Dapper E2E 基准；自定义 Gen2 列重复原生 MemoryDiagnoser 统计。
- 修复：IN 改为 `0/1/10/100/500/1000/2100`，拆分 `CreateInValues`、`BindExistingInValuesAndRender` 和 `BuildInValuesAndRender`；移除自定义 Gen2 列和配置引用；新增临时 SQLite 文件上的 Dapper `Query().ToList` E2E 基准，RowCount 覆盖 `1/100/1000`，GlobalCleanup 删除临时数据库。
- 验证：
	- Benchmark Release build：PASS，0 errors。
	- Benchmark case list：PASS，包含全部 IN 边界、三个 IN 方法和 `SqliteDapperE2EBenchmarks.QueryToList`。
	- IN Dry/FormalHost smoke：PASS，42 cases，覆盖七个参数边界，未出现 build/process failure。
	- SQLite E2E Dry/FormalHost smoke：PASS，RowCount `1/100/1000`，FormalHost 进程退出码 0。
	- Round 5 provenance SHA-256：`global.json`=`8E7272916B97B7C032B0D07F5D50C47D7C4FC32369A28A2AEF6CFEA133FEE1B0`；`appveyor.yml`=`AF5681B329762A362B045C026DEB6E90B1A7FF482A4AB797EDEB6ABB45FE15CA`；`SqlLambdaRootBenchmarks.cs`=`C9407599ED882A2103416463A2933280A8B8A4C9277F80B9CA18892714D6A431`；`SqlLambdaJoinBenchmarks.cs`=`0B7CC978481FD4F94FC4070DD132B221191F172ED4A234ED6DF973094585CAE4`；`SqliteDapperE2EBenchmarks.cs`=`AEF992AEEA0190008502805DBFF4ABBBD031FDBD562C6AC44C41C207FA6851EC`。
	- IN artifacts：CSV=`208ED6A2735F2049DD5918700731416A920989AA3AF092C815F950D813997454`；Markdown=`2DF6B96CF7F485B9532621F8C4D0C73959E7DCF2E4969DA0C2BDC768E1C8A938`；HTML=`EBE0D5ABABE16CE12B9968EFA9C53813FCEFBE96307345D4E693C4741DCCCA1F`。
	- SQLite E2E artifacts：CSV=`1C0F354CB1A680811DE59E42590AABF7C84D38767CB649CB5D9A831789E9B4F7`；Markdown=`9B8F128774D063DB684EF73F989F7D7DDAF8299A575B1C24263F2D82599A2F5E`；HTML=`D46111CBAB130D0B41B984F052419E461282DEADCA12687212B03BFEC4CE7440`。
	- 有效 before/after：BLOCKED，仍缺独立旧源码身份和同 Job 完整 before artifact，未声明性能 delta 或 RC 性能准入。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `global.json`
	- `appveyor.yml`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：CI 未固定 SDK、未实际运行 Benchmark，过程报告保留 VS2017/CI 未修改旧描述。
- 修复：新增 `global.json` 固定 .NET SDK `10.0.300`；AppVeyor 保持 VS2022，常规测试显式关闭外部 Provider，并增加两步 Benchmark Dry smoke 和独立 `BenchmarkDotNet.Artifacts\\ci` 制品路径；同步进度、验证和基线文档中的当前状态与 Round 5 证据。
- 验证：
	- `global.json` SDK pin：PASS，本地 `dotnet --version` 为 `10.0.300`。
	- AppVeyor 配置静态检查：PASS；远程 AppVeyor 执行和实际 artifact 上传：BLOCKED。
	- 外部 Provider：BLOCKED，当前环境仍无安全连接和对应 Gate。
	- `progress.md`、`verification-report.md`、`benchmark-baseline.md`：已同步 Round 5 状态，整体仍为 `partial/blocked`。

### Round 5 汇总

- MUST_FIX：`0`
- SHOULD_FIX：`2`
- 已完成：`0`
- PARTIAL：`2`
- BLOCKED：外部 Provider、AppVeyor 远程运行、独立 before/after 性能比较和远程 artifact 上传。
- FAILED：`0`
- 回归验证：Benchmark Release build、IN 42-case smoke、SQLite E2E smoke 通过；核心 Data.Sql/Analyzer 回归沿用 Round 4 通过结果；最终 `git diff --check` 待收口复核。
- 下一步：交回独立 Review；Reviewer 继续独立维护 `review.md`。

### Round 6

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮处理 `FIX-004`、`FIX-005`；未修改 `review.md`，未执行 commit、push 或 PR。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：Round 5 只有单一 `QueryToList` SQLite/Dapper E2E，未覆盖计划要求的终结、流式、映射、多结果集、诊断、取消、异常和提前释放代表路径；异常 case 原先受 `RowCount` 影响，不能稳定证明基数失败。
- 修复：新增 `QueryToEntity`、同步/异步流式、取消、2/5/7 映射、多结果集、提前释放、基数异常、Activity、DiagnosticListener 和 Trace 共 14 个真实 SQLite/Dapper case；使用 `Union All` 独立制造 `ToEntity` 多行异常；为 Trace 路径注册无输出但启用 Trace 的 LoggerProvider；补齐生产符号到测试方法追溯。
- 验证：
	- `dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore --nologo`：PASS，0 errors。
	- 最新程序集 `QueryToEntityCardinalityFailure` 的 `RowCount=1/100/1000`、`Dry + FormalHost` 共 6 个 case：PASS，进程退出码均为 0；异常计数为预期的 2，未出现 NA 或 process failure。
	- SQLite E2E 旧程序集长矩阵已完成部分 case 后主动终止，退出码 1 属于主动停止，不作为最新代码的完整通过证据；完整同 Job before/after 仍 blocked。
	- 旧版独立源码身份和有效 before/after artifact：BLOCKED，当前环境没有可审计的旧源码工作树与同 Job 完整结果。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlCiSmokeBenchmarks.cs`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
	- `appveyor.yml`
	- `global.json`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：CI 通过带有类级 `FormalHost` 的类型执行 `--job Dry`，导致 Dry smoke 实际附带 FormalHost；原入口自动发现也不能稳定限定为 smoke 类型。
- 修复：新增独立、非 sealed 的 `[DryJob]` `SqlCiSmokeBenchmarks` 类型及 no-op `DispatchProxy` executor；`SqlMetadataBenchmarks` 增加 `--ci-smoke` 显式类型入口；AppVeyor 过滤到 `*SqlCiSmokeBenchmarks*` 并使用独立 `BenchmarkDotNet.Artifacts\ci` 输出；SDK 继续由 `global.json` 固定为 10.0.300。
- 验证：
	- `dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --ci-smoke --filter "*SqlCiSmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\round6-ci-smoke-latest"`：PASS；只发现/执行 1 个 `SqlCiSmokeBenchmarks.BuildRawQuery`，Job 仅为 `Dry`，进程退出码 0，生成 CSV/Markdown/HTML。
	- AppVeyor 静态配置：PASS；AppVeyor 远程执行和制品上传：BLOCKED，当前环境无法运行远程作业。
	- MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实集成：BLOCKED，缺少安全连接、`RUN_*_INTEGRATION_TESTS=true` Gate 和数据库重置授权。

### Round 6 汇总

- MUST_FIX：`0`
- SHOULD_FIX：`2`
- 已完成：`0`
- PARTIAL：`2`
- BLOCKED：独立 before/after、AppVeyor 远程作业与远程制品、外部 Provider 真实集成。
- FAILED：`0`
- 回归验证：Benchmark Release build、最新 CI Dry smoke、最新 ToEntity 基数异常 Dry/FormalHost 定向矩阵通过；未宣称性能 delta 或 RC 性能准入。
- 下一步：交回独立 Review；Reviewer 继续独立维护 `review.md`。

### Round 7

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮处理 `FIX-004`、`FIX-005`；未修改 `review.md`，未执行 commit、push 或 PR。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：Round 6 的 14 个 E2E 方法虽已落地，但没有在最新程序集上独立运行完整矩阵；取消和基数异常通过返回值表达分支，错误返回不会令 Benchmark 失败。
- 修复：抽取 `SqliteDapperE2EBenchmarkBase`，新增独立 `[DryJob]` `SqliteDapperE2ESmokeBenchmarks` 和 `--e2e-smoke` 类型入口；GlobalSetup 在计时前调用全部 14 个方法并强制校验预期值，错误路径直接抛出验证异常；保留 FormalHost 类型用于正式基准。SQLite E2E 方法、Trace provider 和追溯映射保持独立。
- 验证：
	- `dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore --nologo`：PASS，0 errors。
	- `dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet\round7-e2e-smoke"`：PASS；42 个唯一 case，14 个方法 × `RowCount=1/100/1000`，Job 仅为 `Dry`，无重复键、无 process failure。
	- GlobalSetup 契约验证：PASS；覆盖 Query/ToEntity/ToList、同步/异步流、取消、2/5/7 映射、多结果集、提前释放、基数异常、Activity、DiagnosticListener、Trace。
	- Round 7 源码 hash：`SqliteDapperE2EBenchmarks.cs`=`90561E1A989E76BC604E37C01107D4E9E4F33DD83D4D7F047C3D35B6E9D54E9E`；`SqlMetadataBenchmarks.cs`=`D09A1898B4CC283800568734800173B00B0988103D1A6ACB7F1FA50201B75303`。
	- Round 7 E2E 制品 hash：CSV=`6313F97067B4EBCECC7BDED8D34303C2698D3A2101CAA14989784232B983B01B`；Markdown=`BF1B9F5F95CF24B5C487B1265E88542CBCCABE1F14353ABCDD3EEA338CC667FF`；HTML=`D8D7184F3789C7101ED5D0B3FCA280FDDDAC2A7ECEDA74D57AD5A1C1AB51AFDE`。
	- 独立旧源码身份、同 Job 完整 before/after 和性能准入：BLOCKED，当前环境没有可审计旧源码工作树及匹配的完整 FormalHost 结果；未声明性能收益。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `appveyor.yml`
	- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
	- `global.json`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：本地 Dry-only CI smoke 已解决，但完整 SQLite/Dapper E2E smoke 没有接入 AppVeyor，CI 无法真实执行代表矩阵；远程作业和 Provider 环境仍不可用。
- 修复：AppVeyor 保留快速 `--ci-smoke`，新增 `--e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*"` 命令并写入独立 `BenchmarkDotNet.Artifacts\ci-e2e` 制品目录；继续固定 VS2022 和 SDK 10.0.300，并保留 TRX/Cobertura/PublicAPI/Benchmark artifact 收集。
- 验证：
	- 本地 `SqlCiSmokeBenchmarks`：PASS；只执行一个 Dry case，未附带 FormalHost。
	- 本地 `SqliteDapperE2ESmokeBenchmarks`：PASS；42 个 Dry case，生成独立 E2E CSV/Markdown/HTML 制品。
	- `appveyor.yml` hash=`84ED8619AC681D997832608BB639BF2707F2AEE0E8567E99C936DC5173935ABF`；配置静态检查：PASS。
	- AppVeyor 远程执行、目标镜像制品上传：BLOCKED，当前环境无法执行远程作业。
	- MySQL/PostgreSQL/SQL Server/Oracle/Doris：BLOCKED，缺少安全连接、对应 `RUN_*_INTEGRATION_TESTS=true` Gate 和数据库重置授权。

### Round 7 汇总

- MUST_FIX：`0`
- SHOULD_FIX：`2`
- 已完成：`0`
- PARTIAL：`2`
- BLOCKED：独立 before/after、AppVeyor 远程作业与远程制品、外部 Provider 真实集成。
- FAILED：`0`
- 回归验证：Benchmark Release build、最新 42-case SQLite/Dapper Dry smoke、快速 CI Dry smoke、静态诊断和差异检查通过。
- 下一步：交回独立 Review；Reviewer 继续独立维护 `review.md`。

### Round 8

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/review.md`
- 本轮处理 `FIX-004`、`FIX-005`；未修改 `review.md`，未执行 commit、push 或 PR。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/execution.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：Round 7 已闭环最新程序集 SQLite/Dapper 42-case Dry 正确性，但性能准入仍要求独立旧源码身份、独立构建和同 Job FormalHost before/after。
- 处理：审计 `BenchmarkDotNet.Artifacts/review-fix-round3-before-root`、`review-fix-round3-before-join`、`review-fix-round4-before-root`。这些日志和 CSV 确实存在，但 Root 日志显示 72-case 旧参数矩阵（RootCount 与 ParameterCount 交叉），Join 日志显示旧 `JoinCount` 36-case 矩阵；没有可审计旧源码工作树、dirty diff hash、独立源码 hash 或与当前调整后矩阵匹配的 before provenance。因此按 Reviewer 规则将其保留为历史/无效 evidence，不把它们升级为 before/after。
- 本地验证：
	- AppVeyor 等价快速命令：`dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --ci-smoke --filter "*SqlCiSmokeBenchmarks*" --artifacts "BenchmarkDotNet\round8-ci-equivalent"`：PASS，1 个唯一 Dry case。
	- AppVeyor 等价 E2E 命令：`dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet\round8-ci-e2e-equivalent"`：PASS，42 个唯一 Dry case，14 方法 × `RowCount=1/100/1000`，无重复键和 process failure。
	- `SqlMetadataBenchmarks.cs` hash=`D09A1898B4CC283800568734800173B00B0988103D1A6ACB7F1FA50201B75303`；`SqliteDapperE2EBenchmarks.cs` hash=`90561E1A989E76BC604E37C01107D4E9E4F33DD83D4D7F047C3D35B6E9D54E9E`。
	- Round 8 CI 等价制品 hash：快速 CSV=`1DB4917ECCF9E2249F3197E4F6B84CE03310B2F51559B5DDAB015AC3C0E918AD`；E2E CSV=`E0BB529CD3D19222EF2409BF842D7BA0894FCEF14396B3EFC9876C412710D572`。
- 阻断：独立旧源码、同 Job FormalHost before/after、Root/Join/IN/过滤/诊断逐 case 性能准入仍 `BLOCKED`；未声明性能收益或 RC 准入。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`PARTIAL`
- 修改文件：
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/execution.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/progress.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/verification-report.md`
	- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/benchmark-baseline.md`
- 根因：AppVeyor 配置已包含快速 smoke 和 E2E smoke，但当前执行环境无法提供远程 AppVeyor job、下载制品或外部 Provider 安全数据库环境。
- 处理：按 AppVeyor 当前配置逐条执行本地等价命令；快速 smoke 只发现/执行 `SqlCiSmokeBenchmarks.BuildRawQuery` 1 个 Dry case，E2E smoke 执行 42 个 Dry case；`appveyor.yml` hash=`84ED8619AC681D997832608BB639BF2707F2AEE0E8567E99C936DC5173935ABF`；`global.json` hash=`8E7272916B97B7C032B0D07F5D50C47D7C4FC32369A28A2AEF6CFEA133FEE1B0`。
- 本地验证：Benchmark Release build PASS，0 errors；Data.Sql net8 `1261/1261`；Analyzer net8 `30/30`；`git diff --check` PASS，仅 CRLF/LF 转换提示。当前进程环境没有 `RUN_*_INTEGRATION_TESTS`、`ConnectionStrings__*` 或 `ALLOW_DATABASE_RESET_FOR_TESTS` 变量。
- 阻断：AppVeyor 远程执行、远程 TRX/Cobertura/PublicAPI/Benchmark artifact 上传和 MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实集成均 `BLOCKED`；不把本地等价执行或未配置 Provider 计为远程/跨库通过。

### Round 8 汇总

- MUST_FIX：`0`
- SHOULD_FIX：`2`
- 已完成：`0`
- PARTIAL：`2`
- BLOCKED：独立旧源码与 FormalHost before/after、AppVeyor 远程作业/远程制品、外部 Provider 真实集成。
- FAILED：`0`
- 回归验证：Benchmark Release build、AppVeyor 等价快速/E2E Dry smoke、Data.Sql net8、Analyzer net8 和 `git diff --check` 通过。
- 下一步：交回独立 Review；Reviewer 继续独立维护 `review.md`。
