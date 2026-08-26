# Bing.Data.Sql RC 加固实施计划

- task-id: `BING-SQL-RC-HARDENING-20260825-001`
- 状态: `APPROVED_FOR_EXECUTION`
- 计划日期: `2026-08-25`
- 任务类型: 发布候选（RC）正确性加固、API 收敛、测试矩阵、性能基线、文档发布准备
- 自动提交: 禁止 `git add`、`git commit`、`git push`、PR、Tag、Release
- Planner 写入边界: 本轮仅创建本文件；未修改生产代码、测试、配置、数据库或已有任务材料。

## 1. 输入、约束与冲突处理

### 1.1 已读取的事实依据

- 仓库约束: `AGENTS.md`、`.github/copilot-instructions.md`、`.github/prompts/create-plan.prompt.md`。
- 注释规范: `.github/skills/chinese-comments/SKILL.md`。
- 架构/治理文档: `ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`docs/sqlquery-usage.md`、`docs/integration-testing.md`。
- 前序任务和独立复审: `ai_docs/tasks/BING-SQL-API-HARDENING-20260824-001/{plan,execution,review,final-summary}.md`。
- 当前生产、测试、集成、Analyzer、Benchmark 和 Public API 基线源码。

用户指定的 `ai_docs/codebase-analysis/Bing.Data.Sql-全面审查报告-20260825.md` 不存在；本计划以用户需求、上述仓库文档和当前源码为准，实施时不得因该报告缺失停工。

### 1.2 冲突与裁决

1. 用户要求计划后立即实施，但当前 Agent 是 `plan-writer`，唯一写入目标为本 `plan.md`。本轮结束于计划完成；实施由 `/execute-plan`、`/run-plan` 或 `$execute-plan` 启动。
2. 用户要求执行中维护计划、进度、测试、Benchmark、Review 和最终报告。Executor 必须在任务目录创建并持续维护这些带 task-id 的交付物；Planner 不创建空文件。
3. 用户允许 Breaking Change，且明确禁止无意义兼容层；仓库仍存在 `PublicAPI.Shipped.txt` 和 NuGet 发布痕迹。因此实施按 Breaking Change 执行，但必须同步维护 Public API 基线、编译契约、迁移文档和仓库消费者，不能通过关闭 API Analyzer 或保留 `[Obsolete]` 转发规避变更。
4. 用户要求收窄 runtime SPI，且禁止生产程序集间 `InternalsVisibleTo`。由于 `Bing.Dapper.Core` 跨程序集消费执行计划和执行器，收窄必须先得到消费者矩阵；确需跨程序集的最小合同保持 `public`，其余降为 `internal/private`，不得用 friend assembly 替代。
5. 外部数据库真实执行是发布要求，但当前分析无法确认 Gate、凭据和专用测试库可用。SQLite 必须始终执行；其它 Provider 必须实现 SQL 契约和门控测试，缺环境只标记外部阻塞，不得伪造通过。

## 2. 仓库认知和现状判断

### 2.1 技术栈、构建与测试

| 范围 | 当前证据 | 结论 |
| --- | --- | --- |
| 生产 TFM | `framework.props` 为 `netstandard2.0` | `Bing.Data.Sql`、`Bing.Dapper.Core` 是 SDK 风格库，使用 Public API Analyzer。 |
| 测试 TFM | `framework.tests.props` 为 `net8.0;net6.0` | xUnit 为主要测试框架；测试遵循 Unit/Integration 分层。 |
| 核心项目 | `Bing.Data.Sql`、`Bing.Data.Sql.Analyzers`、`Bing.Dapper.Core` | Analyzer 作为 Data.Sql 项目的 analyzer 引用；Dapper Core 引用 Data.Sql。 |
| Benchmark | `Bing.Data.Sql.Benchmarks`，`net8.0`，BenchmarkDotNet `0.14.0` | 已有 Root/Join/Metadata/Mutation 场景，但没有本任务要求的可比正式前后结论。 |
| Integration | SQLite、MySQL、PostgreSQL、SQL Server、Oracle、Doris 项目均存在 | SQLite 无外部依赖；外部 Provider 使用 `RUN_INTEGRATION_TESTS` 或 Provider 专用变量及安全数据库命名校验。 |

### 2.2 当前已实现且应保持的能力

| 能力 | 源码/测试证据 | 状态 |
| --- | --- | --- |
| 非泛型查询根 | `ISqlQuery.Query/Sql/SqlInterpolated/Procedure/From/FromTable/FromSubquery`；`SqlQueryApiContractTest` | 已实现。 |
| 终结方法确定结果类型 | `SqlLambdaQuery`、`SqlFluentQuery`、`SqlTextQuery` 的 `ToEntity<TResult>`、`ToList<TResult>` | 已实现。 |
| 连续来源与二元 Lambda | `SqlLambdaQuery`、`FromClause.AppendRoot`、`JoinCore`；编译契约包含连续 10 来源 | 已实现。 |
| 泛型根/高元数/As 删除 | API 契约与 Roslyn 负向编译测试 | 已实现，应防回归。 |
| Raw Fluent/Text 2～7 Dapper 映射 | `SqlQuery`、`SqlTextQuery`、`ISqlQueryPlanExecutor` 2～7 `ToList/ToListAsync` | 已实现，需深化真实错误与资源测试。 |
| 查询身份 | `SqlQueryPlan` 和 `DiagnosticsMessage` 有 QueryContextId、ParentQueryContextId、ExecutionId、Phase | 已实现，但仍保留 OperationId 兼容属性。 |
| 查询隔离基本机制 | `SqlQuery` ShapeVersion/缓存、Clone、执行租约；SQLite 生命周期测试 | 已实现，需补系统化污染和异常路径证据。 |
| SQLite 真实执行 | `SqliteExecutionIntegrationTest`、`SqliteMultipleQueryIntegrationTest` | 已有 SQL、物化、取消、分页、流式和部分多结果集真实覆盖。 |

上一轮复审记录了 Data.Sql、Analyzer、SQLite Unit/Integration 的通过历史，但本任务必须重新建立当次基线，不能仅引用历史测试数作为当前验证。

### 2.3 已证实的缺口和风险

| 问题 | 当前直接证据 | 判断 |
| --- | --- | --- |
| BINGSQL002 推荐不存在 API | `BingSqlDiagnosticDescriptors.UnsafeInterpolatedSql.MessageFormat` 使用 `SqlInterpolated<T>()` | P0 正确性和开发体验缺陷。 |
| 同步阻塞异步完成回调 | `SqlMultipleQueryResult.Complete()` 调用 `_completeAsync(...).GetAwaiter().GetResult()` | P0 生命周期风险；同步 Dispose 的正式语义未被充分锁定。 |
| OperationId 兼容 API | `DiagnosticsMessage.OperationId` 是 `[Obsolete]` 转发到 `ExecutionId` | 与本次要求冲突，必须删除而非继续隐藏。 |
| 条件组兼容 API | `ISqlConditionGroup.Group(Action<...>)` 仍公开 | 与唯一 `AndGroup/OrGroup` 语义冲突。 |
| 脱离调用链的十表限制 | `FromClause.SetRoots(IReadOnlyList<Type>)` 无生产源码调用，保留 `Count > 10` 检查 | 死路径和过时约束，应删除及移除其专属测试。 |
| 内部终结重复 | `SqlQuery` 仍有 public-internal-class `ToDictionary`、`SingleOrDefault`；`SqlLambdaQueryCore` 再转发 | 应依据消费者删除或降至最小执行层，公开描述不可重新暴露。 |
| Runtime SPI 过宽 | 七个 Runtime 类型均以 `EditorBrowsable(Never)` 公开；`ISqlQueryPlanExecutor` 同时含 builder source、所有终结和多映射 | 仅隐藏 IntelliSense 不等于 API 收敛；需按真实跨程序集消费裁剪。 |
| 文档追溯陈旧 | `ai_docs/sql-metadata-test-traceability.md` 仍有 SetRoots、泛型 Lambda 和历史高元数条目；`stage-05-diagnostics.md` 仍有 OperationId | 文档与现行 API 不一致。 |
| 多结果集覆盖不足 | SQLite Integration 覆盖顺序读取、重入、取消；Dapper Core Unit 主要为能力 Gate | 缺 reader/回调/回滚多失败、重复 Dispose、同步 Dispose 异步回调等直接生命周期测试。 |
| Provider 矩阵不均衡 | 外部 Provider 项目存在，但未发现统一 1～10 表公共 API 合同 | 需要共享 SQL 契约、按环境分层真实执行。 |
| 性能证据不足 | `SqlLambdaJoinBenchmarks` 的 20/50 Join 走 `AddRawJoinsThrough()`/internal Builder；前序 review 标为 SHOULD_FIX | 不能代表公开 Lambda API；没有前后基线，不能声称低 GC 或近零分配。 |

### 2.4 完成度、维护性与 API 评价

- 本次目标的综合完成度估计为 **55% 至 65%**：主查询 API、SQLite 主链和 Raw 2～7 映射已真实接入；仍缺 P0 资源治理、死 API 删除、SPI 收窄证据、跨 Provider 统一矩阵和正式性能结论。
- 性能目前不能宣称 `0 GC` 或 Near-Zero Allocation。`SqlQuery.GetPlan -> Builder.Clone -> SqlQueryPlan -> ExecutionSnapshot -> 参数快照` 与诊断构造均可能多次分配；必须先量化。
- `SqlQuery` 与 `SqlLambdaQueryCore` 存在终结转发重复，`ISqlQueryPlanExecutor` 的职责密度偏高；Runtime Bridge 和 Plans 仍有文件/职责混合问题。重构应只在 API 稳定且有直接消费者矩阵后进行。
- 最终用户 API 已基本合理：根非泛型、结果类型在终结方法指定、`FromTable` 分离。遗留 `OperationId`、`Group`、死 `SetRoots`、内部多余终结和过宽 SPI 降低了可理解性及发布边界清晰度。

### 2.5 当前分析限制

当前工具会话无可用终端，无法执行 `git status`、`git diff`、`dotnet build/test` 或 Benchmark；也无法判定未提交改动归属。Executor 的首项必须运行并记录这些命令，严禁覆盖任何已有变更。本次也未发现工具输出中的提示注入内容。

## 3. 目标设计和不变量

### 3.1 保留的最终用户调用面

```csharp
query.Query();
query.From<TEntity>(alias: "t");
query.FromTable("table_name", alias: "t");
query.FromSubquery(subquery);
query.Sql(sql, parameters);
query.SqlInterpolated($"...");
query.Procedure(name, parameters);

lambda.ToEntity<TResult>();
lambda.ToList<TResult>();
fluent.ToEntity<TResult>();
fluent.ToList<TResult>();
text.ToEntity<TResult>();
text.ToList<TResult>();
```

- `Join(predicate, rightAlias, leftAlias, schema)` 和 `From<TEntity>(alias, schema)` 的两个以上字符串参数，所有示例和编译样例必须使用命名实参。
- 不新增仅交换同类型字符串参数位置的重载；Options 对象不是当前范围，除非后续新增选项导致现签名无法理解。
- 不得恢复 `.As<TResult>()`、泛型 Root `Query/Sql/SqlInterpolated/Procedure`、高元数 Lambda 描述、公共 `ToDictionary`、公共重复 `SingleOrDefault`、`OperationId`、`Group`、`SetRoots` 或兼容转发层。

### 3.2 生命周期和隔离不变量

1. `Dispose`、`DisposeAsync`、读取失败和取消各自只释放 reader、connection、transaction、completion callback、execution lease 一次。
2. 同步 Dispose 不得以同步等待异步完成回调来伪装正确性；正式策略须在 Phase 1 原型决定并文档化。不得使用 `Task.Run` 绕过问题。
3. 主执行异常优先保留；reader、事务、completion callback、lease 的多个 cleanup 异常以既有 `SqlQueryPlanLifecycle` 聚合规则完整呈现，不吞掉、不过度重复。
4. 取消令牌必须传入 Dapper `CommandDefinition`、事务 acquire/commit/rollback 与流式枚举的真实链路；基础设施 async 使用 `ConfigureAwait(false)`，不引入 `.Result`、`.Wait()` 或未定义的 sync-over-async。
5. `WhereIf(false)` 不改变 SQL、参数、ShapeVersion 和缓存；成功 mutator 只 `Touch()` 一次；失败操作和 Clone 间不污染 SQL、参数、alias、租户、动态过滤或执行状态。

### 3.3 Runtime SPI 原则

- 先列出 `Bing.Dapper.Core`、FreeSQL、EF Core、Provider、Tests、Benchmarks 的真实消费者，再决定 `public/internal/private`。
- `EditorBrowsable(Never)` 不是可见性治理替代品。
- 跨程序集必要 SPI 可保留 minimal public contract；`SqlQueryPlan` 不得公开 Builder、connection、transaction、诊断 scope 或可变执行资源。
- 不添加生产程序集 `InternalsVisibleTo`。

## 4. 需求追踪矩阵

| Requirement | Public API | Core/Internal | Unit Test | Integration Test | Benchmark | Status |
| --- | --- | --- | --- | --- | --- | --- |
| BINGSQL002 推荐最终 API | Analyzer descriptor/documentation | Analyzer invocation 识别 | Analyzer 完整 ID/title/message/description 断言和安全入口编译 | 不适用 | 不适用 | 未完成 |
| 多结果集资源生命周期 | `ISqlMultipleQueryResult.Dispose/DisposeAsync` 行为说明 | `SqlMultipleQueryResult`、执行 lease、事务适配器、异常聚合 | 回调、reader、lease、重复释放、取消、失败组合 | SQLite 多结果集提前终止/重试 | 不适用 | 部分完成 |
| 删除 OperationId | `DiagnosticsMessage` | 诊断发布/克隆链 | 反射负向和 QueryContext/ExecutionId 行为 | SQLite 诊断事件 | Activity/Listener 最小路径 | 未完成 |
| 删除 Group | `ISqlConditionGroup` | `SqlConditionGroup` | AndGroup/OrGroup 语义及 Group 负向契约 | SQLite 条件组真实 SQL | 不适用 | 未完成 |
| 删除 SetRoots/十表上限 | 无 | `FromClause` | 连续 From 1～10 完整 SQL/参数；SetRoots 不存在 | SQLite 1～10 | Root 构建场景改名/对齐 | 未完成 |
| 终结 API 收敛 | Lambda/Fluent/Text 描述 | `SqlQuery`、`SqlLambdaQueryCore`、executor | public/internal 消费者矩阵、负向 API 契约 | SQLite ToEntity/多映射 | 不适用 | 部分完成 |
| Runtime SPI 收窄 | Runtime public snapshot | Factory/Binding/Bridge/Plan | 可见性、Consumer compile contract、无生产 friend | Provider consumer 构建 | 不适用 | 未完成 |
| 1～10 最终 API 覆盖 | `From`/Join/终结 | Builder/renderer/parameter snapshot | 完整 SQL、参数名/值/数量、alias/schema/组合 | SQLite 必跑；外部 Provider 门控 | Root/Join 规模 | 部分完成 |
| Fluent/Text 2～7 映射 | `ToList/ToListAsync` | executor/Dapper binding | 2～7、splitOn、map null/throw、类型转换/取消 | SQLite 同步/异步真实物化与释放 | 可选 Dapper binding 场景 | 部分完成 |
| Clone/Plan/diagnostics allocations | 无新增 API | Clone/Plan/snapshot/diagnostics | 隔离、并发、缓存、身份语义 | SQLite 执行重试/释放 | 正式 before/after | 未完成 |
| 文档与 XML | 用户 API/XML/Analyzer | 迁移和追溯文档 | 示例 Roslyn compile | package/xml 验证 | 报告措辞 | 未完成 |

## 5. 文件范围

### 5.1 已确认生产文件

- `framework/src/Bing.Data.Sql.Analyzers/Bing/Data/Sql/Analyzers/BingSqlDiagnosticDescriptors.cs`
- `framework/src/Bing.Data.Sql.Analyzers/Bing/Data/Sql/Analyzers/UnsafeInterpolatedSqlAnalyzer.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryResult.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Diagnostics/DiagnosticsMessage.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryBuilderSource.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryPlanExecutor.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryRuntimeBindingController.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Plans/SqlQueryPlan.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlBuilderRuntimeBridge.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeFactory.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeBinding.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- `framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt`
- `framework/src/Bing.Dapper.Core/PublicAPI.Unshipped.txt`

### 5.2 已确认测试、集成和基准文件

- `framework/tests/Bing.Data.Sql.Analyzers.Tests/BingSql002AnalyzerTest.cs`
- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Clauses/FromClauseTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/WhereGroupAtomicityTest.cs`
- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryExecutorTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteMultipleQueryIntegrationTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlDebugSqlBenchmarks.cs`
- 各 `Bing.Dapper.{MySql,PostgreSql,SqlServer,Oracle,Doris}.Tests(.Integration)` 项目和既有 shared fixture。

### 5.3 候选文件

仅在消费者矩阵证明确实受影响时修改：

- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase*.cs`、多结果集执行器、事务和 Dapper 参数绑定实现。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Plans/ISqlOutputParameterAccessor.cs` 与其它 Runtime/Bridge 物理拆分文件。
- `framework/src/Bing.FreeSQL/**`、`framework/src/Bing.EntityFrameworkCore/**` 及各 Provider 生产消费者。
- `framework/tests/Bing.Test.Shared/**`、`framework/tests/Bing.TestShare*` 的现有共享基建。
- `docs/sqlquery-usage.md`、`docs/sqlquery-lambda-usage.md`、`docs/integration-testing.md`、`docs/ReleaseNotes.md`、`ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`ai_docs/stage-05-diagnostics.md`。

## 6. 执行状态和任务文档

Executor 创建下列文件，每一个必须包含 `task-id: BING-SQL-RC-HARDENING-20260825-001`：

- `progress.md`：唯一当前状态使用 `DISCOVER -> PLAN -> IMPLEMENT -> VERIFY -> REVIEW -> FIX -> RE-VERIFY -> NEXT_PHASE -> FINAL_VERIFY -> COMPLETE`；同一时刻仅一个 `in_progress`。
- `execution.md`：逐任务实现、测试、Review、修复、回归证据。
- `test-report.md`：命令、TFM、结果数、失败最小错误、Gate 状态和环境限制。
- `benchmark-report.md`：before/after、硬件/OS/Runtime/GC/BDN、artifact、统计数据和结论边界。
- `review-report.md`：每 Phase review 的 MUST_FIX/SHOULD_FIX/COULD_FIX/WONT_FIX 与修复结果。
- `api-migration.md`：删除的 API、替换路径、Breaking Change、迁移样例。
- `final-summary.md`：按用户指定的最终完成报告格式汇总，不自动提交。

## 7. 分阶段实施计划

### Phase 0: DISCOVER/PLAN 基线、范围和可追溯性

#### RC-P0-01 [P0] 工作区与环境基线

- 目标: 在任何编辑前识别用户现有改动、SDK/TFM、项目边界、外部环境和既有失败。
- 依赖: 无。
- 修改范围: 仅任务工作文档。
- 步骤:
  1. 使用 UTF-8 PowerShell 环境运行 `git status --short --untracked-files=all`、`git diff --stat`、`git diff --check`；对 SQL 相关改动逐文件记录，不回滚、不覆盖、不 `git add`。
  2. 记录 `dotnet --info`、`dotnet --list-sdks`、OS、CPU、Runtime、有效 runsettings 和外部 Provider Gate 环境变量是否只存在名称而不记录敏感值。
  3. 读取 `Bing.All.sln`、公共 props、核心/Provider/测试 csproj，确认 TargetFramework、Public API Analyzer、BenchmarkDotNet 和 xUnit 入口。
  4. 扫描源码而排除 `bin/obj/output`：`TODO`、`NotImplementedException`、`.Result`、`.Wait()`、`.GetAwaiter().GetResult()`、`InternalsVisibleTo`、`Obsolete`、`EditorBrowsable`、`SqlInterpolated<T>`、`.As<`、`SetRoots`、`OperationId`、`Group`、ToDictionary、SingleOrDefault。
  5. 将本计划第 4 节追踪表转写到 `progress.md`，以实际 symbol、测试方法和 Gate 状态更新。
- 验证:
  - `dotnet restore .\Bing.All.sln`
  - `dotnet build .\Bing.All.sln -c Release -nologo -v minimal`
  - 先运行第 9 节的核心 Unit/SQLite 命令；失败记录命令、最小错误、归类，不停止。
- 风险: 工作区可能包含上一任务的未跟踪文件；所有后续 diff 必须与此基线比对。
- 验收: 未提交状态、环境、测试项目、Provider Gate 和当前失败均可追溯；每个需求对应真实文件和测试项目。

#### RC-P0-02 [P0] 消费者与公开边界矩阵

- 目标: 为删除/收窄 API 建立证据，防止机械删除破坏跨程序集官方消费者。
- 依赖: RC-P0-01。
- 修改范围: 工作文档，之后按证据修改生产/测试。
- 步骤:
  1. 为 `OperationId`、`ISqlConditionGroup.Group`、`FromClause.SetRoots`、`SqlQuery.ToDictionary*`、`SqlLambdaQueryCore.ToDictionary*`、`SqlQuery.SingleOrDefault*`、`SqlLambdaQueryCore.SingleOrDefault*` 枚举生产、测试、文档、Public API 引用。
  2. 为七个 Runtime 类型枚举 Data.Sql、Dapper Core、FreeSQL、EF Core、Provider、Tests、Benchmarks 消费成员，不以 `EditorBrowsable` 推断无消费者。
  3. 导出最终保留/删除/internal/private 表；明确每个删除 API 的迁移方式和 Public API 基线处理。
  4. 确认 `SetRoots` 无生产调用且仅服务历史 internal 测试；确认连续 `From<TEntity>()` 是 1～10 表唯一测试入口。
- 验收: 每个拟删除或缩窄符号有消费者清单、替代路径和直接回归测试位置。

### Phase 1: IMPLEMENT/VERIFY/REVIEW P0 正确性和资源生命周期

#### RC-P1-01 [P0] 修复 BINGSQL002 最终 API 指引

- 目标: Analyzer 只建议实际可用的 `SqlInterpolated(...)` 或参数对象。
- 依赖: RC-P0-01。
- 修改范围: Analyzer descriptor、Analyzer Tests、必要 XML/包说明。
- 步骤:
  1. 将 title、message、description、CodeFix（若存在）、测试期望和文档中的 `SqlInterpolated<T>()` 一律替换为最终 API，不修改 Analyzer 对插值危险流的识别范围。
  2. 扩展 `BingSql002AnalyzerTest`：断言完整 `Id`、Severity、Title、Message、Description 和诊断位置，而不是只断言 ID。
  3. 保留 `Sql(sql, parameters)` 与 `SqlInterpolated($"...")` 的零诊断正向用例；添加真实 public API Roslyn 编译契约，证明建议入口存在且接收 `FormattableString`。
  4. 检索 docs/ai_docs/Analyzer 包内容，清理仍向用户展示的旧泛型名称；历史任务审计内容可保留，但必须显式标为历史且不作为当前说明。
- 用例矩阵:

| Given | When | Then |
| --- | --- | --- |
| 直接插值 SQL | 调用普通 `Sql(string)` | 产生一条完整 BINGSQL002，message 推荐 `SqlInterpolated(...)`。 |
| 变量/拼接/条件表达式传播插值 | 调用普通入口 | 仍诊断，避免安全回归。 |
| 参数对象或 `SqlInterpolated` | 编译并分析 | 无 BINGSQL002 且推荐 API 可编译。 |
| 非框架同名 `Sql` | 分析 | 不误诊断。 |

- Mock 边界: Roslyn 编译使用最小 API stub 与真实引用集合；不 Mock Analyzer 本身。
- 验收: Analyzer 编译与所有 Analyzer Tests 通过；任何 BINGSQL002 用户文本不再出现 `SqlInterpolated<T>()`。

#### RC-P1-02 [P0] 定义并实现 SqlMultipleQueryResult 无阻塞释放策略

- 目标: 消除同步 Dispose 对异步完成回调的未定义 `.GetAwaiter().GetResult()` 阻塞，同时保证资源和异常语义。
- 依赖: RC-P0-02。
- 修改范围: `SqlMultipleQueryResult`、创建同步/异步 completion callback 的 Dapper Core 执行路径、事务 async adapter、生命周期 helper（仅确有需要时）。
- 设计决策步骤:
  1. 跟踪同步创建、异步创建、`Read/ReadAsync`、`Dispose/DisposeAsync`、读失败、取消、reader dispose、transaction commit/rollback、completion callback、execution lease 的实际所有权。
  2. 确定并记录正式合同：异步创建结果的资源性完成必须由 `DisposeAsync` 完成；若同步 `Dispose` 仍是接口要求，必须使用不阻塞且不会遗失 callback 的可验证策略。不得使用 `Task.Run`、fire-and-forget、静默吞异常或 duplicate cleanup。
  3. 仅在合同与执行器调用链允许时，使同步路径拥有同步 completion；无法同步完成的异步清理需由 API 设计/实现结构消除，而不是在线程上阻塞。
  4. 保留 `Interlocked.Exchange` 的一次性 ownership；审计 `TryBeginDispose`、读失败分支和 finally，确保重复 `Dispose/DisposeAsync` 安全、交叉并发仍明确拒绝。
  5. 复用 `SqlQueryPlanLifecycle.ThrowExceptions`/Capture 规则或在同一职责层修正，使主异常、reader、callback、transaction、lease 多重失败完整且不重复。
- 用例矩阵:

| Given | When | Then |
| --- | --- | --- |
| 同步创建、完整读取 | `Dispose()` | reader、completion、lease 各一次，成功状态为 true。 |
| 异步创建、完整读取 | `DisposeAsync()` | async completion awaited，各资源一次。 |
| 异步创建 | `Dispose()` | 符合新合同，无同步等待/死锁/后台未观察异常。 |
| 未读完/提前停止 | sync 和 async 释放 | completion 为 false，资源归还。 |
| 读取异常/预取消 | 读操作失败 | primary exception 保持，lease 可用于下一次执行。 |
| reader/回调/rollback 多重异常 | 释放 | 主异常与 cleanup 异常按既有聚合策略呈现。 |
| 连续或并发重复 Dispose | 再次释放 | 无重复 callback/lease；并发操作得到一致 InvalidOperationException。 |

- Mock 边界: Unit 使用可控 reader/transaction/lease/callback 替身验证生命周期；不得用 Mock 取代 SQLite 的真实连接与 Dapper 多结果集释放。
- 验收: 目标代码无 `.GetAwaiter().GetResult()`；同步/异步完成链均有直接测试，SQLite 多结果集在异常、取消、提前停止后可再次执行。

#### RC-P1-03 [P0] 异步和资源链路审计

- 目标: 确认目标查询路径的取消、事务和 `IAsyncDisposable` 行为实际向下传递。
- 依赖: RC-P1-02。
- 修改范围: Dapper Core 目标执行器、transaction scope lease、streaming/multiple result 测试；仅针对审计发现的真实缺陷编辑。
- 步骤:
  1. 从 `ToListAsync/ToEntityAsync/ToPageAsync/AsAsyncEnumerable/ExecuteAsync/MultipleQuery.ExecuteAsync` 跟踪 `CancellationToken` 到 Dapper `CommandDefinition`、打开连接、交易 acquire/commit/rollback 和枚举器。
  2. 搜索目标程序集 `.Result`、`.Wait()`、`.GetAwaiter().GetResult()`；每处分类为合法同步边界或要删除的阻塞，不做全仓无关重构。
  3. 为异步基础设施补齐 `ConfigureAwait(false)`，但不改变需要捕获上下文的 UI 层（本模块不应有该依赖）。
  4. 覆盖 async reader、transaction 和 lease 的 dispose once，失败后同一描述/执行器可重试。
- 验收: 所有被触及查询 async 路径有 cancellation 和释放证据；剩余同步等待仅在有明确 API 合同、直接测试和文档证明时存在，否则为 MUST_FIX。

#### RC-P1-04 [P0] Phase 1 Review/Fix/Re-verify

- 检查: 真实调用链、sync-over-async、CancellationToken、`Dispose/DisposeAsync`、异常聚合、lease/connection/transaction 一次释放、BINGSQL002 完整文案。
- MUST_FIX/SHOULD_FIX: 发现后立即进入 `FIX`，先跑最小 Unit，再跑 Dapper Core、Analyzer、SQLite Integration。
- 验收: `review-report.md` 无未修复 MUST_FIX/SHOULD_FIX；本 Phase 所有命令及失败修复记录在 `test-report.md`。

### Phase 2: IMPLEMENT/VERIFY/REVIEW API 收敛和 Breaking Change

#### RC-P2-01 [P0] 删除 OperationId 和条件组 Group 兼容 API

- 目标: 唯一诊断执行标识为 `ExecutionId`；嵌套条件组必须显式使用 `AndGroup` 或 `OrGroup`。
- 依赖: Phase 1 全绿。
- 修改范围: `DiagnosticsMessage`、诊断发布/观察器/测试、`ISqlConditionGroup`、`SqlConditionGroup`、Public API、文档/示例/追溯。
- 步骤:
  1. 迁移所有生产/测试使用者至 `ExecutionId`，删除 `OperationId` 属性和 `[Obsolete]`，同步 PublicAPI 基线。
  2. 删除 `Group(Action<...>)` 及实现；检查 `WhereGroup` 的默认连接语义是否仍需要公开，必要时 API 明确命名为 `AndGroup`，不保留隐式默认入口。
  3. 在诊断事件、Activity、分页 Count/Data、Clone/parent context 中验证 QueryContextId/ParentQueryContextId/ExecutionId 语义不变。
  4. 更新 docs、ai_docs 活动部分和 XML，历史记录仅保留为历史，不得成为当前 API 示例。
- 测试: 反射和 Roslyn 负向契约验证已删除成员不可用；AndGroup/OrGroup 验证完整 SQL、参数和原子失败状态；SQLite 验证诊断 before/after/error identity。
- Breaking Change: 删除公开成员，无 Obsolete 迁移层；迁移说明给出 `OperationId -> ExecutionId` 和 `Group -> AndGroup/OrGroup`。
- 验收: 源码、活动文档、Public API 和示例无上述兼容符号；所有 SQL 断言为完整字符串。

#### RC-P2-02 [P0] 删除 SetRoots 死路径和十表上限

- 目标: 移除未被生产调用的多根替换路径，保持最终连续 `From<TEntity>()` 1～10 表能力。
- 依赖: RC-P0-02。
- 修改范围: `FromClause`、只依赖 SetRoots 的 tests/Benchmark/追溯，Public API/XML 生成物不手改。
- 步骤:
  1. 再次源码搜索确认无生产消费者；如发现消费者，先迁移到连续 `AppendRoot/From<TEntity>` 并直接测试。
  2. 删除 `SetRoots`、其十表限制、只服务该路径的 preflight helper（若无其它调用）与专属测试。
  3. 保留并按最终 public API 补齐 1 至 10 个连续 `From<TEntity>` 的完整 SQL、参数名、参数值、数量、来源顺序、重复实体自动 alias、显式 alias、schema 测试。
  4. 覆盖 `From x,y`（连续根）与连续 Join、Where/WhereIf、Select/AppendSelect、Order/Group/Page 组合；10 表期望 SQL 保持人工可读。
- 验收: `SetRoots` 和“最多十个根来源”不在生产/当前测试/活动文档；最终连续 API 的 1～10 覆盖不降低。

#### RC-P2-03 [P1] 清理孤立终结与重复转发

- 目标: `ToEntity<TResult>()` 成为唯一公共 0/1 行入口，移除未被公共描述使用的 `ToDictionary` 和重复 `SingleOrDefault` 表层。
- 依赖: RC-P0-02、RC-P2-01。
- 步骤:
  1. 按消费者矩阵验证 `SqlQuery`/`SqlLambdaQueryCore` 的 ToDictionary 和 SingleOrDefault 是否只为内部实现或历史测试保留。
  2. 删除无消费者 ToDictionary；如内部确需字典转换，仅在具体执行路径 private 化且不创建新 API。
  3. 将 `ToEntity` 所需的 single-or-default primitive 收敛至 executor/internal 最小边界，避免 `SqlQuery` 和 Core 两层同义转发。
  4. 保留 Procedure 的 `ExecuteSingleOrDefault` 仅在其语义与 `SqlProcedureResult` 合同不同且有真实使用者时；否则作为独立 API 裁决记录。
- 测试: public reflection/Roslyn 负向、实际 ToEntity 0/1/>1 行语义、内部 executor 直接职责测试。
- 验收: public Lambda/Fluent/Text 不暴露 ToDictionary/SingleOrDefault；移除项没有转发或兼容别名。

#### RC-P2-04 [P1] Runtime SPI 最小公开面

- 目标: 将 SPI 的 public 范围缩至官方跨程序集真实需求。
- 依赖: RC-P0-02、RC-P2-03。
- 步骤:
  1. 对 `ISqlQueryBuilderSource`、`ISqlQueryPlanExecutor`、`ISqlQueryRuntimeBindingController`、`SqlQueryPlan`、`SqlBuilderRuntimeBridge`、`SqlQueryRuntimeFactory`、`SqlQueryRuntimeBinding` 按成员生成消费者表。
  2. 保留 Dapper Core/官方 Provider 真实调用所需的最小 public 类型和成员；其它成员降为 `internal/private` 或收敛到更窄的现有抽象。
  3. 不能因 `internal` 需要而新增生产 `InternalsVisibleTo`；如已有外部官方消费者需要内部状态，改造数据流为不可变 plan/snapshot 或窄的执行方法。
  4. 更新 PublicAPI、reflection snapshot、Dapper/FreeSQL/EF Core/Provider Roslyn 或项目构建契约。
- 风险: public-to-internal 是 Breaking Change，必须先迁移所有仓库消费者并维护迁移文档。
- 验收: `EditorBrowsable(Never)` 只作为剩余确需 public SPI 的辅助，不再承担治理；无生产 friend assembly；官方消费者构建通过。

#### RC-P2-05 [P1] 别名参数开发体验门禁

- 目标: 减少同类型字符串参数误用，无参数顺序重载膨胀。
- 依赖: RC-P2-02。
- 步骤:
  1. 复核 `Join` 的 `rightAlias/leftAlias/schema` 和 `From` 的 `alias/schema` 参数名、XML、异常信息和示例。
  2. 将有两个以上字符串参数的 docs、samples、Roslyn 编译示例改为命名实参。
  3. 评估 Analyzer 规则：只有能可靠区分高风险位置参数且不会误报常规单字符串调用时才实现；否则记录 `DEFER` 的证据，不增加噪声诊断。
- 验收: 参数名称稳定清晰；无只交换字符串参数位置的重载；示例均为命名调用。

#### RC-P2-06 [P0] Phase 2 Review/Fix/Re-verify

- 检查: 无 OperationId/Group/SetRoots、无 Obsolete 转发、新旧 Public API 一致、生产消费者编译、无 production `InternalsVisibleTo`、完整 SQL/参数测试、SPI 不泄露资源。
- 验收: 无未修复 MUST_FIX/SHOULD_FIX；更新 `api-migration.md` 和测试追溯。

### Phase 3: IMPLEMENT/VERIFY/REVIEW 目录、文件和实现职责重构

#### RC-P3-01 [P1] 以消费者证据驱动的文件职责拆分

- 目标: 消除大杂烩文件和多 public 类型文件，保持 API、SQL、参数、异常和诊断行为不变。
- 依赖: Phase 2 API 冻结。
- 优先检查: `SqlBuilderBase.cs`、`SqlQueryBase.cs` partial、`JoinClause.cs`、`Extensions.ISqlBuilder.cs`、`WhereClause.cs`、`SqlExecutorBase.cs`、`MutationClauseExtensions.cs`、`SqlLambdaQuery.cs`、`FromClause.cs`、`SqliteExecutionIntegrationTest.cs`。
- 拆分对象: `SqlProviderProfile.cs`、`ISqlOutputParameterAccessor.cs`、`SqlFilterTopology.cs`、`ISqlDataBoundaryContributor.cs`、`SqlWriteCommand.cs`、`ISqlReturningDialect.cs`、`ISqlTenantFilterContributor.cs` 中的多个 public 类型。
- 步骤:
  1. 对每个候选文件记录主类型、其它类型、行数、引用方向和是否存在独立职责；仅实际职责混合时拆分。
  2. 按现有 `Abstractions/Queries/Builders/Runtime/Parameters/Diagnostics/Providers/Extensions/Internal` 目录方向迁移，避免无价值 Helper/partial/抽象层。
  3. 一个主要 public 类型原则上一个文件；接口、Options/DTO、internal 实现分离，但 namespace 仅在收益超过 Breaking Change 成本时改动。
  4. 对移动的每个类型进行构建、API snapshot、直接 Unit 和 SQLite 回归；不借移动夹带行为修改。
- 验收: 无新增循环依赖、无第二实现、主要 public 类型位置清晰；`SqliteExecutionIntegrationTest` 按查询、生命周期、诊断、映射职责拆分而非继续膨胀。

#### RC-P3-02 [P1] Runtime Bridge/Plan/参数快照最小化重构

- 目标: 降低 `SqlBuilderRuntimeBridge`、Plan 和参数/诊断快照的耦合，为性能测量建立清晰边界。
- 依赖: RC-P3-01。
- 步骤:
  1. 以来源注册、Join、参数绑定/快照、渲染、Plan 派生、执行、诊断、事务为职责切分方法和文件。
  2. 删除只有一个调用者且不表达替换价值的抽象；不新增 interface -> abstract -> base -> strategy 层链。
  3. 保证 QueryPlan 始终不持有连接、事务、可变 Builder 或诊断 scope；执行快照保持不可变。
  4. 为该阶段涉及的 snapshot/cache/builder clone 更新直接 Unit 和 `Bing.Data.Sql.Benchmarks` 场景或在报告中记录不适用原因。
- 验收: 目录与 namespace 一致，跨程序集边界可由类型名识别；测试证明无 query/parameter/tenant/filter 污染。

#### RC-P3-03 [P1] Phase 3 Review/Fix/Re-verify

- 检查: 移动类型真实参与构建、无重复源、无循环依赖、API 可见性未扩张、无生产 friend assembly、功能测试无 SQL/参数漂移。
- 验收: 代码 Review 无未解决 MUST_FIX/SHOULD_FIX；所有物理移动在最终未提交清单中显式列出，防止遗漏未跟踪文件。

### Phase 4: IMPLEMENT/VERIFY/REVIEW 测试与 Provider 矩阵

#### RC-P4-01 [P0] 最终 API 和 1～10 表 Unit 合同

- 目标: 用最终公共 API 而不是 internal 捷径锁定全部根来源和 Join 行为。
- 依赖: Phase 2。
- 修改范围: `Bing.Data.Sql.Tests` 职责级测试，必要 shared sample entity。
- 要求:
  1. 分别提供 1、2、3、4、5、6、7、8、9、10 表案例；每个从 `ISqlQuery.From<TEntity>` 和相应公开扩展构造。
  2. 对每例完整相等断言 SQL、参数名、参数值、参数数量；不得只 `Contains`。
  3. 覆盖重复实体、自动 alias、显式 alias、schema、连续 From、连续 Join、From+Join 混合、Where、WhereIf、Select、AppendSelect、Order、Group、Page。
  4. 覆盖 WhereIf true/false、所有触及 mutator 的 Touch、缓存命中/未命中、动态过滤/数据边界 cache bypass、Clone 互不污染、同描述并发拒绝、Clone 并发允许、取消/异常后恢复。
  5. 为 `SqlLambdaQueryCore`、`SqlQueryPlanLifecycle`、参数 snapshot、条件组、operation accessor、transaction scope lease、MultipleQueryResult 建立直接 internal 责任测试，不仅从 façade 间接覆盖。
- 验收: 最终生产符号到测试方法映射更新至 `ai_docs/sql-metadata-test-traceability.md`；所有 SQL 断言完整。

#### RC-P4-02 [P0] SQLite 真实执行与生命周期闭环

- 目标: SQLite 作为每次 CI 必跑的真实数据库证明，不用 Mock 替代 Dapper 方言和资源生命周期。
- 依赖: RC-P1-02、RC-P4-01。
- 步骤:
  1. 在现有 fixture 上增加 1～10 表真实表/数据/同步/异步核心路径，验证 SQL、参数、行、字段映射及连接/事务释放；避免复制已有等价案例。
  2. 补 From+Join 混合、重复实体 alias、schema/附加库受控 alias、子查询、分页、流式提前退出与重试。
  3. 多结果集覆盖正常 sync/async、同步释放异步创建结果的正式行为、提前停止、读异常、取消、reader dispose 异常、completion 异常、rollback 异常、多个 cleanup 异常、重复释放和 lease 归还。
  4. 2～7 Dapper 多映射在 Fluent 和 Text 分别覆盖同步/异步、默认/custom splitOn、map null/map exception、无结果/多行、取消、类型转换失败、释放。
- 验收: SQLite Integration 每次可运行；所有生命周期异常后查询描述和执行器可再用。

#### RC-P4-03 [P1] 外部 Provider 分层 1～10 表合同

- 目标: MySQL、PostgreSQL、SQL Server、Oracle、Doris 具备和 SQLite 同一最终 API 的 SQL 契约，真实执行按安全 Gate 分层。
- 依赖: RC-P4-01。
- 步骤:
  1. 复用 `Bing.Test.Shared`/`Bing.TestShare*` 和各项目既有 fixture，建立不依赖公网、无 sleep 的 Provider capability contract；不新造平行测试基建。
  2. 每个 Provider 在无外部连接时至少编译并执行 SQL 渲染契约：1～10 Root/Join、quote、schema、alias、参数前缀、分页和 Provider 不支持行为。
  3. 外部连接可用时创建专用测试表、插入可区分数据，验证同步/异步执行、映射、参数、事务和释放。
  4. SQLite 每次 CI 必跑；外部 Provider 以 `RUN_INTEGRATION_TESTS=true` 或既有 Provider 变量门控；Release 必须完整运行 1～10。
  5. 环境缺失时返回明确 Skip 原因和已运行 SQL 契约，不伪造 PASS；禁止生产连接、硬编码连接字符串或删库。
- 验收: 报告逐 Provider 标记 `CONTRACT_PASS`、`EXECUTED_PASS` 或 `GATE_SKIPPED`；Oracle/Doris 的能力差异有明确成功/拒绝 SQL 合同。

#### RC-P4-04 [P0] Phase 4 Review/Fix/Re-verify

- 检查: 关键 internal 不是只间接覆盖、SQL/参数断言完整、无 Mock 替代真实 DB 路径、SQLite 必跑、外部 Gate 诚实、取消和资源释放可回归。
- 验收: Review 中 MUST_FIX/SHOULD_FIX 已修复；测试追溯矩阵无 SetRoots/OperationId/旧 API 项。

### Phase 5: IMPLEMENT/VERIFY/REVIEW 性能、GC 与 Benchmark

#### RC-P5-01 [P0] 可重复性能基线

- 目标: 在任何优化前获取带 task-id、同一环境指纹的性能基线。
- 依赖: Phase 3 行为稳定。
- 场景:
  - 1/2/5/10/20/50 来源，1/2/5/10 Join。
  - 10/100/1000 参数，IN 参数分别提供端到端和预构造输入版本。
  - 首次/重复渲染、WhereIf true/false、动态过滤/数据边界、Clone、Plan freeze、execution snapshot、同描述 1/10/1000 次执行。
  - 无诊断、Activity-only、DiagnosticListener、Trace logger。
  - 同步、异步、流式、分页、MultipleResult。
- 步骤:
  1. 修复 `SqlLambdaJoinBenchmarks` 的场景命名与测量路径：20/50 如果不能使用公开 API，重命名为底层 Builder/渲染冒烟并从公开 Lambda 结论剔除；最好增加实际公开 API 代表性压力场景。
  2. 不把 setup、`new object[ParameterCount]` 或测试输入构造分配计入框架热点；记录 GC 模式、CPU、OS、SDK/Runtime、commit/diff 指纹、BDN artifact。
  3. 输出 Mean、Median、Error/StdDev、适当分位数、Allocated、Gen0/1/2、LOH 说明；Benchmark 可编译或 Dry Run 不等于性能改善。
- 验收: `benchmark-report.md` 有可复跑命令和 before 基线，明确哪些场景是公开 API、哪些是内部 microbenchmark。

#### RC-P5-02 [P1] 基于证据的 Clone/Plan/诊断优化

- 目标: 仅优化 Benchmarks 和 allocation profile 已证实的热点，优先保证隔离。
- 依赖: RC-P5-01。
- 步骤:
  1. 定量拆解 `SqlQuery.GetPlan -> Builder.Clone -> SqlQueryPlan -> CreateExecutionSnapshot -> RenderSnapshot -> Parameter Snapshot`。
  2. 对比当前深 Clone、冻结后不可变 plan 共享、Copy-on-Write、构建一次重复执行的方案；每个方案先补 isolation/concurrency/cancellation 测试。
  3. Activity-only 只创建必要 tag；仅在 DiagnosticListener/Trace 真启用时构造完整 DiagnosticsMessage、参数、连接/事务对象；消除先生成又覆盖的 ExecutionId，Trace dictionary 只在 Trace 开启时创建。
  4. 不为目标强行使用 Span/ArrayPool/ObjectPool/ref struct/ValueTask/FrozenDictionary；只有后基线更好且无逃逸、脏参数、并发污染才引入。
- 验收: 至少一个真实热点有可解释的改善，或有量化的“不实施”结论；禁止 0 GC 宣称，除非对应场景实际 `Allocated = 0`。

#### RC-P5-03 [P2] Source Generator 决策

- 目标: 仅评估 2～7 Dapper 映射机械 façade/XML 的维护收益，绝不恢复高元数 Lambda API。
- 依赖: API 稳定、RC-P5-01 证据。
- 步骤: 对手写、Incremental Generator、MSBuild 生成、检入生成代码比较重复度、构建复杂度、调试性和多 TFM 风险；若提议采用，必须有生成快照、编译、增量、多 TFM、诊断和可读性测试；收益不足则写 ADR 为 `DEFER/REJECT`，不创建 Generator。
- 验收: `source-generator-decision.md` 记录证据和结论。

#### RC-P5-04 [P0] Phase 5 Review/Fix/Re-verify

- 检查: 场景是否真实走公开调用链、前后环境一致、分配是否被测试输入污染、优化是否破坏 query/parameter/tenant/filter 隔离、报告是否虚称 0 GC。
- 验收: 同机 after 结果与 before 对比完备；显著回退修复或明确不合入。

### Phase 6: IMPLEMENT/VERIFY/REVIEW 文档、XML 和发布准备

#### RC-P6-01 [P1] XML 注释和示例同步

- 目标: 按 `chinese-comments` Skill，使本任务范围 public API 注释、Analyzer 和示例与最终实现一致。
- 依赖: Phase 2 最终 API，Phase 3 文件稳定。
- 步骤:
  1. 所有新增/修改 public 类型、构造函数、方法、属性、字段按 Skill 补中文 XML；接口/override 实现优先 `<inheritdoc />`，不复制父契约。
  2. `ToEntity<TResult>()` 明确 0 行默认、1 行实体、>1 行异常；Join/From 多字符串示例使用命名实参；说明 cancellation、线程安全、Dispose/DisposeAsync 与资源所有权。
  3. 删除/修订当前 API 文档中的旧泛型 façade、`As<TResult>()`、高元数/八元表述、OperationId、Group、SetRoots。
  4. 增加 Roslyn 编译示例：单表、连续 From 2/5/10、Join 2/5/10、子查询 Join、Fluent/Text 2～7 映射、SqlInterpolated、QueryContextId/ExecutionId。
- 验收: XML 与签名一致、无无效 `<returns>`、示例可编译、当前文档不展示旧 API。

#### RC-P6-02 [P1] Public API、迁移、Pack 验证

- 目标: 发布物、迁移说明、API snapshot 和 Analyzer 包内容一致。
- 依赖: RC-P6-01。
- 步骤:
  1. 更新 Shipped/Unshipped，保持 RS0016/RS0017/RS0018 启用；删除 API 无兼容层。
  2. 在 `api-migration.md` 记录删除 API、替换调用、Breaking Change、外部 SPI 边界和示例。
  3. 执行实际 `dotnet pack` 到任务或临时安全输出位置，检查 nupkg、XML、symbols、Analyzer `analyzers/dotnet/cs` 包含关系；不得发布。
  4. 更新 Release Notes、SQL 使用/设计/追溯文档和外部测试说明。
- 验收: 包内容、XML、symbols、Analyzer、Public API snapshot 与最终 API 一致。

#### RC-P6-03 [P0] FINAL VERIFY、最终 Review 和完成报告

- 目标: 按 DoD 完成全量回归、Review 修复闭环与交付报告。
- 依赖: Phase 1 至 Phase 6。
- 步骤:
  1. 依第 9 节顺序运行可用命令；每个失败进入 `FIX`，修复后先最小回归，再所属项目，最后全量回归。
  2. 进行最终 Review：真实生产调用链、占位/兼容 façade、public API 扩张、friend assembly、sync-over-async、CT、dispose、隔离污染、无基准优化、XML、完整 SQL/参数。
  3. MUST_FIX 和 SHOULD_FIX 必须修复；COULD_FIX 根据收益处理并写理由；WONT_FIX 必须说明影响。
  4. 汇总用户要求的完成报告格式，包含 task-id、实际变更、API 表、功能/Provider/1～10 表/性能/Review/外部阻塞/git status/建议提交信息和 `code-commit: not-created`。
- 验收: 仅当 DoD 达成或只剩经证实的外部环境阻塞时，状态设为 `COMPLETE` 或 `COMPLETED_WITH_EXTERNAL_BLOCKERS`。

## 8. API 和兼容性清单

| API/类型 | 操作 | 替代/迁移 | Breaking Change |
| --- | --- | --- | --- |
| `DiagnosticsMessage.OperationId` | 删除 | `ExecutionId` | 是 |
| `ISqlConditionGroup.Group(...)` | 删除 | 明确选择 `AndGroup(...)` 或 `OrGroup(...)` | 是 |
| `FromClause.SetRoots(...)` | 删除 internal 死路径 | 连续 `From<TEntity>()` / `AppendRoot` 内部路径 | internal 行为变更 |
| `SqlQuery`/`SqlLambdaQueryCore` ToDictionary | 删除或 private 化（以矩阵为准） | `ToList<TResult>()` 后 LINQ 转换 | 不得新增 public 替代 |
| `SqlQuery`/Core 重复 SingleOrDefault | 删除或收窄 | `ToEntity<TResult>()`，executor 内最小 primitive | 可能影响 internal 消费者 |
| Runtime SPI 多余成员 | `internal/private` | immutable plan/snapshot 或窄 public contract | 是，先迁移官方消费者 |
| BINGSQL002 文案 | 改正 | `SqlInterpolated(...)`/参数对象 | 否，修复建议 |

## 9. 验证命令与运行顺序

以下命令来自仓库实际 `Bing.All.sln`、项目路径、SDK 项目和集成测试说明。Executor 在 PowerShell 先设置 UTF-8 控制台编码；命令输出和写入报告均使用 UTF-8。

```powershell
dotnet restore .\Bing.All.sln
dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release -nologo -v minimal
dotnet build .\framework\src\Bing.Dapper.Core\Bing.Dapper.Core.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test .\Bing.All.sln -c Release -nologo -v minimal
```

外部 Provider 仅在专用安全测试库、runsettings/CI secrets 和显式门控可用时运行。先运行对应 Unit，再运行以下真实集成项目；缺环境以 `GATE_SKIPPED` 记录：

```powershell
dotnet test .\framework\tests\Bing.Dapper.MySql.Tests.Integration\Bing.Dapper.MySql.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests.Integration\Bing.Dapper.PostgreSql.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests.Integration\Bing.Dapper.SqlServer.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Oracle.Tests.Integration\Bing.Dapper.Oracle.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Doris.Tests.Integration\Bing.Dapper.Doris.Tests.Integration.csproj -c Release -nologo -v minimal
```

Benchmark 使用项目实际入口执行，并记录完整 BenchmarkDotNet artifact；先 Dry 验证场景，再以同机正式 Job 生成前后对比。不得将 Dry 结果作为性能结论。

```powershell
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release -- --job Dry
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release -- --filter *SqlLambda*
```

## 10. 风险、外部阻塞和缓解

| 风险 | 等级 | 缓解与停机条件 |
| --- | --- | --- |
| 修改 MultipleResult 释放语义破坏 callback/lease | P0 | 先建立生命周期矩阵；每一异常组合都有 Unit + SQLite 真实回归；任何资源遗漏为 MUST_FIX。 |
| 删除 public API 影响未知外部消费者 | P1 | 本任务已批准 Breaking Change；维护 API 基线、迁移文档、编译契约和版本说明，不提供兼容转发。 |
| SPI internalize 导致官方生产消费者无法编译 | P1 | 先输出消费者矩阵，使用窄 public contract；禁止 production IVT。 |
| 外部 Provider 无凭据/测试库 | P1 | SQL contract/编译/fixture 先完成，记录 Gate 与安全原因；只在全部其他工作完成后作为外部阻塞。 |
| 大型重构掩盖 SQL/参数漂移 | P1 | API 稳定后再移动；每一步跑完整 SQL/参数 Unit 和 SQLite。 |
| Benchmark 噪声或错误调用链 | P1 | 固定环境、区分 public/internal 场景、保存 artifact；20/50 Join 不得描述为公开 Lambda 基准。 |
| 性能优化引入共享可变状态 | P0 | Clone/Plan/tenant/filter/参数并发和取消测试先行；无数据不采用对象池或 Copy-on-Write。 |
| 用户已有未提交更改 | P0 | Phase 0 记录；不 reset/checkout/clean，不改写不相关文件；不可安全合并时请求用户决定。 |

## 11. Definition of Done

- [ ] Phase 1 至 Phase 6 已完成，或仅剩明确且不可绕过的外部 Provider 环境阻塞。
- [ ] BINGSQL002 不再推荐 `SqlInterpolated<T>()`。
- [ ] `SqlMultipleQueryResult` 无未定义 sync-over-async，且 lifecycle/cleanup/lease 有直接 Unit 与 SQLite 证据。
- [ ] `OperationId`、`Group`、`SetRoots` 和目标孤立 API 不存在，无兼容转发层。
- [ ] 最终 public API 只有一条推荐查询路径，Runtime SPI 为实际跨程序集最小集合，无生产 IVT。
- [ ] 1～10 表 Unit 全部经最终公共 API 构造并断言完整 SQL/参数；SQLite 真实执行通过。
- [ ] 外部 Provider 有可执行 SQL/真实执行矩阵、门控和运行记录，不伪造通过。
- [ ] Fluent/Text 2～7 映射同步/异步、异常、取消和资源路径通过。
- [ ] Clone、缓存、动态过滤、租户、并发、异常后恢复无污染。
- [ ] Benchmark 有实际 before/after 结果，性能措辞不虚称 0 GC。
- [ ] XML、示例、Analyzer、API snapshot、迁移和发布文档与最终 API 一致。
- [ ] 可用环境中的全量 build/test、pack 和最终 Review 已通过；未执行 commit/push/PR/Tag/Release。
