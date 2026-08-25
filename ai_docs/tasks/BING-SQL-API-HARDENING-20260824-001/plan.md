# Bing.Data.Sql 查询体系 API 收敛与发布硬化实施计划

Status: APPROVED_FOR_EXECUTION

## 1. 任务信息

- Task ID：`BING-SQL-API-HARDENING-20260824-001`
- 计划日期：2026-08-24
- 任务类型：查询 API 收敛、发布阻断修复、Runtime SPI 重构、测试补齐、性能验证、发布准备
- 重点程序集：`Bing.Data.Sql`、`Bing.Dapper.Core`、`Bing.Dapper.MySql`、`Bing.Dapper.PostgreSql`、`Bing.Dapper.SqlServer`、`Bing.Dapper.Sqlite` 及对应 Tests/Integration/Benchmarks
- Breaking Change：允许；但仓库存在 NuGet、`PublicAPI.Shipped.txt` 和版本基线，实施时仍必须维护迁移说明和 Public API 门禁，不能仅以“尚未正式发布”为由删除证据
- 自动提交和发布：禁止；不得执行 `git add`、`git commit`、`git push`、Tag、PR、NuGet 发布或自动修改版本号
- Planner 写入边界：本轮仅创建本 `plan.md`，未修改业务代码、测试、配置、数据库或上一任务报告

## 2. 输入、规范与冲突处理

### 2.1 已读取依据

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `.github/prompts/create-plan.prompt.md`
- `.github/skills/chinese-comments/SKILL.md`
- 上一任务：
  - `ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/plan.md`
  - `ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/execution.md`
  - `ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/review.md`
- 设计与治理：
  - `ai_docs/sql-lambda-query-design.md`
  - `ai_docs/sql-public-api-governance.md`
  - `ai_docs/sql-metadata-test-traceability.md`
  - `docs/sqlquery-usage.md`
  - `docs/sqlquery-lambda-usage.md`
  - `docs/integration-testing.md`
  - `README.md`
- 当前生产源码、Public API 基线、Unit/Integration Tests 和 Benchmark 源码

### 2.2 冲突与裁决

1. 用户要求“计划后直接实施”，但当前会话处于 `plan-writer` 模式，唯一允许写入目标是本 `plan.md`。本轮以模式约束为准，计划完成后停止，由 `/execute-plan`、`/run-plan` 或 `$execute-plan` 进入实施。
2. 用户要求执行期间至少维护 `progress.md`、`api-migration.md`、`test-report.md`、`benchmark-report.md`、`review-report.md`、`final-summary.md`。这些文件是后续 Executor 的明确交付物；Planner 本轮不提前创建空文件。
3. 用户声明项目尚未正式发布，但仓库 README 列出 NuGet 包，且 `Bing.Data.Sql`、`Bing.Dapper.Core` 存在 Shipped API 基线。计划接受 Breaking Change 目标，同时要求按已发布风险处理：先导出符号和消费者矩阵，再迁移调用方、维护 Shipped/Unshipped 和迁移指南。
4. 用户要求删除 `SqlQuery<TResult>`，但当前该类型同时承担旧查询描述和 Builder 子查询组合合同，`IFrom`、`IJoin`、`IWhere`、`ICte`、`IUnion` 等扩展仍引用它。实施不得直接删除；必须先把低层子查询消费者迁移到 `SqlSubquery<TProjection>`、非泛型计划或职责等价的新合同，再删除公开泛型描述。
5. 用户要求异常信息包含 Provider、数据类型或 Operator、当前能力和推荐处理方式。TypeConverter 当前接口只接收类型名称，实施应使用各 Provider 固定名称生成明确消息；不得为了消息引入全局可变上下文。
6. 用户要求每个 Phase 内部 Review 后继续执行。Executor 应持续推进，但不得把内部自检报告冒充独立最终 Review；最终 `review-report.md` 是实施自审，正式验收仍应由独立 Reviewer 完成。
7. Source Generator 仅进入决策阶段。禁止为 1～10 表 Lambda 生成类型；Dapper 2～7 多映射只有在手写迁移完成、重复度和维护收益被数据证明后才允许实施生成方案。

## 3. 技术栈与执行边界

- .NET SDK 风格项目，当前验证环境证据为 SDK `10.0.300`、MSBuild `18.6.3`，测试主要覆盖 `net6.0`、`net8.0`。
- 测试框架：xUnit；仓库同时使用 Moq、Shouldly，新增测试沿用所在项目现有风格。
- API 门禁：`Microsoft.CodeAnalysis.PublicApiAnalyzers`；不得关闭 `RS0016/RS0017/RS0018` 规避 Breaking Change。
- Benchmark：BenchmarkDotNet `0.14.0`、`MemoryDiagnoser`，已有 Lambda Root/Join、参数快照和 SQL Metadata 场景。
- SQLite Integration 默认可运行；MySQL/PostgreSQL/SQL Server/Oracle/Doris 必须通过现有环境 Gate 和测试数据库安全校验，不猜测凭据、不连接生产库、不执行未授权数据库重置。
- 所有文本文件按 UTF-8 处理；Windows PowerShell 命令必须显式设置 UTF-8 输出，禁止使用默认编码写入中文文件。
- 当前工作区包含上一任务的大规模未提交修改。本任务必须在其上继续工作，不回滚、不覆盖、不假设 HEAD 等于当前有效实现。

## 4. 当前真实实现与完成度

### 4.1 已实现并应锁定不回退

| 能力 | 当前源码/测试证据 | 判断 |
| --- | --- | --- |
| 唯一 Root 主路径 | `ISqlQuery` 已公开非泛型 `Query/Sql/SqlInterpolated/Procedure/From/FromTable/FromSubquery` | 主入口已实现 |
| 结果类型后置 | `SqlLambdaQuery`、`SqlFluentQuery`、`SqlTextQuery`、`SqlProcedureQuery` 的单对象终结方法在调用处选择 `TResult` | 主路径已实现 |
| Lambda API 收敛 | 仅保留方法级一元/二元表达式；Roslyn 与反射测试拒绝三元和泛型 Lambda 描述 | 已实现 |
| 多根来源与 Join | 连续 `From<TEntity>`、二元 Join、显式 alias、自连接、来源原子性已接入真实 Builder/Core | 已实现 |
| 来源歧义修复 | `ResolveSources`、`ResolveTwoSources`、条件组来源解析均 fail-fast；上一任务独立 Review 为 `PASS_WITH_ISSUES` | 已解决 |
| Unit 1～10 矩阵 | Data.Sql Unit 覆盖 1～10 根来源、2～10 Join、完整 SQL 和完整参数 | 已实现 |
| SQLite 1～10 真实执行 | `SqliteExecutionIntegrationTest` 已真实执行 1～10 类型化根来源和 2～10 连续 Join | 已实现，不重复造轮子 |
| 生命周期与隔离 | Clone、缓存失效、动态过滤、并发拒绝、取消、流式 Dispose、QueryContext/ExecutionId 已有 Unit/SQLite 证据 | 基础能力较完整 |
| 多结果集 | `SqlMultipleQueryExecutorBase` / `SqlMultipleQueryResult` 与 SQLite 集成测试已存在 | 已实现基础路径 |
| 外部 Provider Gate | MySQL/PostgreSQL/SQL Server/Oracle 集成项目和安全 Gate 已存在 | 基建已实现，查询共享合同不足 |

上一轮最新独立验证证据：专项 `134/134`、Data.Sql `2514/2514`、Analyzer `25/25`、SQLite Unit `222/222`、SQLite Integration `284/284`、SQL Server Unit `564/564`、全方案 Release Build 0 error。该证据是本任务起点，不替代 Executor 重新建立基线。

### 4.2 部分完成或未完成

| 范围 | 当前证据 | 状态 |
| --- | --- | --- |
| 动态编译契约 | `Compile()` 只显式引用 `typeof(ISqlBuilder).Assembly`，Analyzer 测试项目不引用 `Bing.Dapper.Core` | 存在假阴性；P0 |
| Advanced 泛型入口 | `SqlAdvancedQueryExtensions` 仍公开 `Query<TResult>/Sql<TResult>/SqlInterpolated<TResult>/Procedure<TResult>`，并强转 `SqlQueryBase` | 与目标冲突；P0/P1 |
| 非泛型多映射 | 2～7 映射只在 `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>` 和 Executor SPI，非泛型描述尚未公开对应终结方法 | 未实现；删除 Advanced 的前置依赖 |
| 泛型描述类型 | `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>`、`SqlProcedureQuery<TResult>`、`SqlQuery<TResult>` 和 Advanced Runtime Factory 仍存在于源码/API 基线 | 未删除 |
| 旧调用方迁移 | Provider Integration 与 Core Tests 仍有 `query.Query<TResult>()`、`Procedure<TResult>()` 等调用 | 未完成 |
| 异常语义 | MySQL/PostgreSQL/SQL Server TypeConverter、`SqlConditionFactory.ValidateSupported`、`SqlConditionBase.AppendSqlBuilder` 仍抛 `NotImplementedException` | 发布阻断 |
| 冗余 API | `ISqlConditionGroup.Group` 无生产消费者；`FromClause.SetRoots` 仅定义；高层 ToDictionary 已删但内部链仍存在；`SqlLambdaQuery` 复制构造函数无已发现消费者 | 待证据化删除 |
| Runtime SPI 结构 | `ISqlQueryBuilderSource.cs` 聚合 Executor/Accessor/Binding 合同；`SqlMultiLambdaQuery.cs` 文件名与内部 Core 不一致；`SqlBuilderRuntimeBridge` 职责密集 | 部分完成 |
| 大文件测试 | `SqlQueryLifecycleTest.cs`、`SqliteExecutionIntegrationTest.cs` 仍承担过多职责 | 未完成拆分 |
| 跨 Provider 查询合同 | 外部项目存在但 MySQL/PostgreSQL/SQL Server 仍大量使用旧泛型/低层入口，没有共享 1～10 Root/Join 合同 | 未完成 |
| 同步释放异步结果 | `SqlMultipleQueryResult.Dispose()` 在仅有异步完成回调时使用 `GetAwaiter().GetResult()`；现有 SQLite 测试未直接锁定该行为 | 需审计与测试 |
| Benchmark 覆盖 | Lambda 仅有 1/2/5/10 Root/Join、10/100/1000 参数；缺 20、50、GetPlan、诊断组合、Dapper 绑定、分页、流、多结果集等 | 部分完成 |
| 性能优化结论 | 只有当前 ShortRun/观察数据，没有同机旧/新基线和统计显著优化证据 | 未完成 |
| XML 与文档 | 上一任务已更新主文档，但本次将删除更多 API、移动 Runtime 命名空间并新增多映射终结 | 必须再次同步 |

### 4.3 完成度判断

- 查询构建、来源正确性、单对象终结和 SQLite 主链：约 **80%～85%**。
- 本次新增的发布硬化目标（彻底删除 Advanced 泛型层、契约盲区、Runtime 物理拆分、跨 Provider 共享合同、性能前后证据）：约 **40%～50%**。
- 综合完成度约 **62%～68%**。剩余工作不是接口骨架，而是跨程序集 Breaking Change、消费者迁移、真实执行合同和性能证据，风险集中在 Phase 1～3。

## 5. 目标 API 与不变量

### 5.1 唯一 Root API

最终普通用户只从以下非泛型入口创建描述：

```csharp
ISqlQuery.Query();
ISqlQuery.Sql(...);
ISqlQuery.SqlInterpolated(...);
ISqlQuery.Procedure(...);
ISqlQuery.From<TEntity>(alias: ..., schema: ...);
ISqlQuery.FromTable(...);
ISqlQuery.FromSubquery(...);
```

- 原始表名只使用 `FromTable`；不得恢复语义不清晰的 Root `From(string)`。
- `alias`、`schema` 同为字符串时，文档和示例优先命名参数。
- 不恢复 `.As<TResult>()`、Root 泛型入口或 `EditorBrowsable(Never)` 兼容扩展。

### 5.2 终结泛型与多映射

- 单对象结果继续由 `ToEntity<TResult>`、`ToList<TResult>`、分页、基数、标量和流式方法决定。
- 在非泛型 `SqlFluentQuery`、`SqlTextQuery` 增加 Dapper 2～7 对象映射的同步/异步 `ToList<T1,...,TResult>(map, ...)`；复用当前 `ISqlQueryPlanExecutor`，不新增高元数 Lambda 查询描述。
- 是否向 `SqlLambdaQuery` 暴露同一多映射终结必须以仓库消费者和开发体验原型决定；默认不扩大，除非有真实结构化查询消费者需要。
- Procedure 继续使用 `Execute*<TResult>`；不引入 Procedure 多映射，除非 Dapper/仓库已有真实需求。

### 5.3 泛型描述删除策略

按依赖顺序删除：

1. 非泛型描述补齐多映射；
2. 迁移生产、测试、样例、文档；
3. 删除 `SqlAdvancedQueryExtensions` 和 `SqlQueryBase` 的 Advanced 工厂方法；
4. 删除 `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>`、`SqlProcedureQuery<TResult>`；
5. 迁移 Builder 子查询扩展对 `SqlQuery<TResult>` 的依赖；
6. 删除 `SqlQuery<TResult>` 与 Advanced Runtime Factory；
7. 更新 Public API 基线和负向编译契约。

不得通过隐藏、Obsolete 包装或强转第三方 `ISqlQuery` 保留旧入口。

### 5.4 正确性与资源不变量

- public mutator 成功后只 Touch 一次；失败、空组、`WhereIf(false)` 不改变 SQL、参数、来源、alias、缓存或 ShapeVersion。
- Clone、分页 Count/Data、子查询、参数快照和动态过滤不共享可变状态。
- 同一描述禁止并发执行；异常、取消、流式提前退出、同步/异步 Dispose 后必须释放租约和连接/事务资源。
- 无 Activity/Listener/Trace 时不创建完整诊断消息和参数快照；仅 Activity 时只创建最小身份。
- 不为微小分配牺牲正确性或引入未经 Benchmark 证明的池化复杂度。

## 6. 文件范围

### 6.1 已确认生产文件

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.Terminals.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlProcedureQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlProcedureQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOfT.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/ISqlQueryBuilderSource.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/ISqlQueryPlanExecutor.PagingStreaming.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryPlan.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Conditions/SqlConditionFactory.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Conditions/SqlConditionBase.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlBuilderRuntimeBridge.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeFactory.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlAdvancedQueryExtensions.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.QueryPlan.*.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryResult.cs`
- `framework/src/Bing.Dapper.MySql/Bing/Data/Metadata/MySqlTypeConverter.cs`
- `framework/src/Bing.Dapper.PostgreSql/Bing/Data/Metadata/PostgreSqlTypeConverter.cs`
- `framework/src/Bing.Dapper.SqlServer/Bing/Data/Metadata/SqlServerTypeConverter.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- `framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt`
- `framework/src/Bing.Dapper.Core/PublicAPI.Unshipped.txt`

### 6.2 已确认测试与 Benchmark 文件

- `framework/tests/Bing.Data.Sql.Analyzers.Tests/Bing.Data.Sql.Analyzers.Tests.csproj`
- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/WhereGroupAtomicityTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Conditions/SqlConditionFactoryTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Conditions/EqualConditionTest.cs`
- `framework/tests/Bing.Dapper.Core.Tests/**`
- `framework/tests/Bing.Dapper.MySql.Tests/Metadata/MySqlTypeConverterTest.cs`
- `framework/tests/Bing.Dapper.PostgreSql.Tests/Metadata/PostgreSqlTypeConverterTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerTypeConverterTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteMultipleQueryIntegrationTest.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaBenchmarkColumns.cs`

### 6.3 候选文件

仅在消费者矩阵证明受影响后修改：

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IFrom.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IJoin.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.ICte.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IUnion.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/WhereClauseExtensions.cs`
- MySQL/PostgreSQL/SQL Server/SQLite/Oracle/Doris Unit 与 Integration 项目
- `Bing.Test.Shared`、`Bing.TestShare*` 中现有共享 Provider 测试基建
- `modules/admin/**`、`samples/**` 和其它仓库消费者
- `README.md`、`docs/sqlquery-usage.md`、`docs/sqlquery-lambda-usage.md`、`docs/integration-testing.md`、`docs/ReleaseNotes.md`
- `ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`
- Source Generator 候选项目或生成文件；只有 Phase 6 决策批准后进入范围

## 7. 执行工作文档

Executor 启动后创建并持续维护：

- `progress.md`：按 Task ID 记录 `TODO/IN_PROGRESS/COMPLETED/PARTIAL/BLOCKED`、证据和下一步。
- `api-migration.md`：最终 API、删除符号、替代调用、Breaking Change 和迁移示例。
- `test-report.md`：环境、命令、TFM、测试数、失败、警告、外部门控状态。
- `benchmark-report.md`：硬件/Runtime/BDN、优化前后 artifact、统计和不适用项。
- `review-report.md`：每 Phase 内部 Review 和最终自审；不得替代独立 Reviewer。
- `final-summary.md`：完成度、发布门槛、未验证项和剩余风险。
- `source-generator-decision.md`：仅记录 Dapper 2～7 多映射生成方案决策；未批准生成时不得创建 Generator 代码。

状态必须随实施更新，不得在结束时一次性补写虚假过程记录。

## 8. 分阶段实施计划

### Phase 0：基线、边界与迁移图

#### P0-T01（P0）工作区和发布基线

- 目标：固定当前未提交状态、构建/test/benchmark 基线和包发布证据。
- 依赖：无。
- 修改范围：仅任务工作文档。
- 步骤：
  1. 记录 `git status --short`、`git diff --stat`、相关文件 diff；标记上一任务修改，禁止回滚。
  2. 记录 SDK、Runtime、OS、CPU、BenchmarkDotNet、TFM、Public API Analyzer 配置。
  3. 导出 `Bing.Data.Sql`、`Bing.Dapper.Core` 和 Provider 的 public types、extension methods、Shipped/Unshipped 矩阵。
  4. 运行 restore、全方案 Release build、核心 Unit、SQLite Integration 和当前 Benchmark ShortRun 基线。
  5. 检查外部 Provider Gate 与安全测试连接；缺失则标 `NOT_RUN_EXTERNAL_GATE_MISSING`。
- 测试：不修改行为；建立可复现基线。
- 风险：当前工作区不是干净 HEAD；Benchmark 必须记录当前 commit/diff 指纹，防止前后样本不可比。
- 验收：`progress.md`、`test-report.md`、`benchmark-report.md` 有完整环境和基线，所有既有失败均被记录。

#### P0-T02（P0）公共 API 和消费者迁移图

- 目标：区分 Root 泛型描述、Dapper 多映射、Builder 子查询合同和 Runtime SPI，确定可删顺序。
- 依赖：P0-T01。
- 步骤：
  1. 搜索源码、测试、样例、文档和 Public API 中全部 `Query<TResult>`、`Sql<TResult>`、`SqlInterpolated<TResult>`、`Procedure<TResult>`、四类泛型描述和 Advanced Factory。
  2. 单独追踪 `SqlQuery<TResult>` 在 From/Join/Where/CTE/Union 子查询扩展中的真实用途。
  3. 生成旧 API -> 非泛型描述/`SqlSubquery<TProjection>`/终结泛型替代矩阵。
  4. 标记第三方 `ISqlQuery` 场景，确保最终不依赖 `SqlQueryBase` 强转。
- 验收：每个拟删除符号都有生产、测试、文档、Public API 消费者和替代方案。

### Phase 1：发布阻断正确性

#### P1-T01（P0）修复动态编译契约引用盲区

- 目标：契约测试按真实消费者引用集合编译，能看到 `Bing.Dapper.Core` 扩展方法。
- 证据：当前 Analyzer 测试项目只引用 Data.Sql，`Compile()` 只显式加入 `typeof(ISqlBuilder).Assembly.Location`。
- 修改范围：Analyzer Tests csproj、`SqlOperationCompileContractTest.cs`，必要时新增职责级 Compile Reference helper。
- 步骤：
  1. 增加 `Bing.Dapper.Core` ProjectReference；按真实 consumer 最小集合加入 Data.Sql、Dapper Core 及必需依赖程序集的 metadata reference。
  2. 引用去重，禁止从当前 AppDomain 无边界加入所有程序集掩盖缺依赖问题。
  3. 先证明当前 `query.Query<Item>()` 在含 Dapper Core 时能够编译，形成会失败的回归测试。
  4. Advanced 删除后将其改为负向契约；正向验证 Root 非泛型入口和终结泛型。
  5. 增加 2～7 多映射正向编译、泛型描述类型负向编译、第三方 `ISqlQuery` 正向编译契约。
- 测试：Analyzer Tests 全量；Public API Analyzer build。
- 风险：错误 reference 集合可能产生与真实 NuGet 消费者不同的诊断。
- 验收：测试可在 Advanced 存在时发现其可见，并在删除后可靠拒绝旧入口。

#### P1-T02（P0）统一“不支持”异常语义

- 目标：查询范围内不再以 `NotImplementedException` 表示已知不支持能力。
- 修改范围：三种 TypeConverter、`SqlConditionFactory`、`SqlConditionBase` 及直接 Unit。
- 步骤：
  1. 未知 Provider 类型改抛 `NotSupportedException`，消息包含 Provider、原始类型、支持能力和“扩展映射/使用受支持类型”的建议。
  2. 未知/越界 Operator 改抛 `NotSupportedException`，消息包含数值、名称（可解析时）、当前条件工厂能力和替代建议。
  3. 基类默认 `AppendSqlBuilder` 改为明确 `NotSupportedException`，说明具体条件类型不支持子查询 Builder，并建议使用 In/NotIn/Exists 或支持该能力的条件。
  4. 覆盖 null/empty/whitespace、大小写、未知类型、非法 enum cast、未知 Operator、子查询传入不支持条件、异常前后参数/StringBuilder 原子性。
  5. 仅处理本任务查询链的五处生产异常；FreeSQL、DDD 等无关模块不扩大范围。
- 测试：Data.Sql Conditions Unit；MySQL/PostgreSQL/SQL Server TypeConverter Unit；对应 Provider Unit。
- 验收：目标五处无 `NotImplementedException`，异常类型和完整消息有直接断言。

#### P1-T03（P0）public mutator 原子性和缓存矩阵

- 目标：以最终 API 表面锁定所有 mutator 的成功/失败状态转换。
- 修改范围：`SqlLambdaQuery`、`SqlFluentQuery`、`SqlTextQuery`、`SqlProcedureQuery`、`SqlQuery`、相关 Core/Clause 和职责级 Unit。
- 步骤：
  1. 导出 public mutator 清单：From、Select/Append、Where/WhereIf/Group、Join、GroupBy/OrderBy/Having、Distinct、Aggregate、Skip/Take、SplitOn、子查询组合。
  2. 为每项记录 SQL、参数、来源、alias、ShapeVersion、缓存命中/失效影响。
  3. 补齐缺失的成功 Touch、失败不 Touch、参数超限、重复 alias、未知来源、Provider 不支持和 Clone 隔离测试。
  4. 对候选 Builder/参数 Probe 的异常顺序做源码审计，确保提交点唯一。
- 测试：完整 SQL和完整参数；不得只 `Contains`，异常消息可针对关键字段精确断言。
- 验收：矩阵中每个 public mutator映射到直接测试，失败状态与调用前快照一致。

### Phase 2：非泛型多映射与 Advanced 删除

#### P2-T01（P0）非泛型描述补齐 Dapper 2～7 多映射

- 目标：在删除泛型描述前保留 Dapper 原生多映射能力。
- 依赖：P1-T01、P0-T02。
- 修改范围：`SqlFluentQuery.NonGeneric.cs`、`SqlTextQuery.NonGeneric.cs`、`SqlQuery`、`ISqlQueryPlanExecutor` 拆分前合同、Dapper Core QueryPlan 实现、Public API、Unit/SQLite Integration。
- 步骤：
  1. 为 `SqlFluentQuery` 和 `SqlTextQuery` 增加同步/异步 2～7 映射终结方法，签名最终结果类型位于方法泛型末尾。
  2. 所有方法复用同一 `SqlQueryPlan`、SplitOn、timeout、CancellationToken 和 Executor SPI，不复制 Dapper 执行逻辑。
  3. 参数和 map null 校验在进入执行前完成；同步/异步异常保持一致。
  4. 用 SQLite 真实 Join/Raw SQL 验证 2、3、7 对象映射，至少覆盖 splitOn、同步、异步、取消和映射异常资源恢复。
  5. 增加 Roslyn 正向调用契约和 Public API 反射测试。
- 风险：可选参数重载可能触发 RS0026/RS0027；通过合理重载设计处理，不关闭规则。
- 验收：用户示例 `Query().ToList<T1,T2,TResult>` 与 `Sql(...).ToList<T1,T2,TResult>` 可编译并真实执行。

#### P2-T02（P0）迁移旧 Root 泛型调用

- 目标：仓库生产、测试、样例和文档不再调用 Advanced Root 泛型入口。
- 依赖：P2-T01。
- 步骤：
  1. 迁移 Dapper Core Tests、Provider Unit/Integration、modules、samples 和文档。
  2. 普通结果迁移到非泛型描述 + 终结泛型；多映射迁移到 P2-T01 新终结；Procedure 使用 `Execute*<TResult>`。
  3. 更新 `api-migration.md`，给出 Query/Sql/Interpolated/Procedure 和多映射对照。
  4. 搜索排除 Dapper 自身 `connection.Query<TResult>`，不得误删底层 ORM 调用。
- 验收：仓库查询 Root 消费者无旧入口；迁移后行为和完整 SQL不变。

#### P2-T03（P0）删除 Advanced 扩展和泛型描述

- 目标：发货程序集不再导出用户列出的泛型 Root/Advanced 查询层。
- 依赖：P2-T02。
- 修改范围：Advanced extension、SqlQueryBase internal Advanced 方法、三类泛型描述、Runtime Factory、Public API、契约测试。
- 步骤：
  1. 删除 `SqlAdvancedQueryExtensions`，消除第三方 `ISqlQuery -> SqlQueryBase` 强转。
  2. 删除 `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>`、`SqlProcedureQuery<TResult>` 和 `CreateAdvanced*`。
  3. 删除 `SqlQueryBase.Query<TResult>/Sql<TResult>/SqlInterpolated<TResult>/Procedure<TResult>`。
  4. 更新 Shipped/Unshipped；不得保留 `EditorBrowsable(Never)`、Obsolete 或转发层。
  5. 动态编译同时引用 Data.Sql/Dapper Core，明确断言旧入口和旧类型不可编译。
- 验收：反射、Public API、Roslyn 和源码搜索都证明旧 Root/Advanced 层不存在；第三方 `ISqlQuery` 不发生运行时强转失败。

#### P2-T04（P1）迁移并删除 `SqlQuery<TResult>` 子查询合同

- 目标：删除公开泛型查询描述，同时保留 From/Join/Where/CTE/Union 子查询能力。
- 依赖：P2-T03。
- 步骤：
  1. 按 P0-T02 消费者矩阵为 Builder 子查询引入或复用 `SqlSubquery<TProjection>`/不可变 QueryPlan 合同。
  2. 迁移 `IFrom/IJoin/IWhere/ICte/IUnion` 扩展及 tests，不让普通 Builder 依赖 Root 泛型查询描述。
  3. 验证参数重命名、Provider/数据源兼容、Clone、CTE/Union/Exists/In/NotIn 完整 SQL和失败原子性。
  4. 删除 `SqlQuery<TResult>`、`SqlQueryOfT.cs` 及相关 Public API。
- 风险：该类型历史用途广，属于本任务最高 blast radius 之一；必须分能力迁移，不做机械替换。
- 验收：公开 `SqlQuery<>` 不存在，所有子查询组合测试和 Provider SQL 回归通过。

#### P2-T05（P1）删除无消费者的重复 API

- 目标：移除无意义兼容或内部遗留。
- 依赖：P2-T04。
- 候选：`ISqlConditionGroup.Group`、`FromClause.SetRoots`、`SqlQuery.ToDictionary`、`SqlLambdaQueryCore.ToDictionary`、未使用的 `SqlLambdaQuery(SqlLambdaQuery)`。
- 步骤：逐项使用源码引用、测试、Public API 和反射确认；迁移唯一必要消费者；删除无消费者项；保留 `AndGroup/OrGroup`。
- 验收：每个删除项有搜索证据和替代语义；不降低条件组、子查询或内部 Executor 覆盖。

### Phase 3：Runtime SPI 与物理结构重构

#### P3-T01（P1）查询文件和类型归位

- 目标：主要 public 类型一个文件，文件名与实际类型一致。
- 依赖：Phase 2 API 稳定。
- 步骤：
  1. `SqlMultiLambdaQuery.cs -> SqlLambdaQueryCore.cs`。
  2. `SqlLambdaQuery.NonGeneric.cs -> SqlLambdaQuery.cs`，保留按职责合理的 partial 终结文件。
  3. 泛型文件删除后将 `.NonGeneric` 后缀移除，避免“非泛型”成为永久命名噪音。
  4. 拆分 `SqlQueryLifecycleTest`、`SqliteExecutionIntegrationTest` 到来源/终结/生命周期/诊断/资源/子查询等职责文件，保持类可 partial 或独立 fixture。
- 验收：文件名、主类型和职责一致；仅移动不改变完整 SQL和行为。

#### P3-T02（P1）拆分 Runtime Abstractions 与 Plans

- 目标：缩小跨程序集 SPI，目录与命名空间表达职责。
- 步骤：
  1. 将 `ISqlQueryBuilderSource.cs` 拆为 Executor、Builder Source、Binding Controller、Builder Accessor 独立文件。
  2. 将必要公共合同归入 `Runtime/Abstractions`，计划/快照归入 `Runtime/Plans`，实现归入 `Runtime/Internal` 或 `Queries/Internal`。
  3. 对命名空间迁移建立 Public API/消费者矩阵；只公开 Dapper Core 真正需要的合同。
  4. 检查所有 public Runtime 类型的 `EditorBrowsable(Never)`、不可变性和资源泄漏边界。
  5. 不新增生产 `InternalsVisibleTo`；当前友元仍只能指向 Tests/Benchmarks。
- 风险：目录移动若伴随 namespace 变化会产生 Breaking Change；必须一次性迁移官方消费者和 Public API。
- 验收：每个 SPI 有唯一消费者职责；普通用户不会接触 Builder、连接、事务或诊断内部状态。

#### P3-T03（P1）拆分 Runtime Bridge 与执行职责

- 目标：消除 `SqlBuilderRuntimeBridge` 和 `SqlQueryBase` 的大杂烩职责。
- 步骤：按来源注册、Join 图、参数原子提交、SQL 渲染、计划派生、诊断、事务、流式执行拆分；跨程序集必需入口保持窄 public，其他 helper internal/private。
- 测试：API 契约、Runtime Binding、Plan 快照、Dapper Core Unit、SQLite Integration。
- 验收：无第二套实现、无生产友元、无行为变化；复杂文件职责可由目录和类型名直接识别。

### Phase 4：测试体系发布级补齐

#### P4-T01（P0）最终 API 契约门禁

- 目标：发货程序集、扩展方法和负向 API 契约均被真实引用集合覆盖。
- 覆盖：Root 非泛型、全部终结、2～7 多映射、旧 Root 负向、旧类型负向、高元数负向、第三方 `ISqlQuery`、Runtime SPI 可见性、无生产友元。
- 验收：契约测试不能因漏引用发货程序集而假通过。

#### P4-T02（P1）Unit 职责矩阵

- 目标：补齐用户列出的 Root、终结、1～10 Root/Join、自连接、schema/alias、重复 alias、空来源、From+Join、子查询、条件组、参数限制、无效输入、原子性、Clone、缓存、身份、并发、取消、Dispose 和 internal Parser/Resolver/Factory/Core。
- 原则：复用现有覆盖，不重复测试；每个最终生产符号映射到直接测试；SQL 和参数均完整相等断言。
- 验收：`ai_docs/sql-metadata-test-traceability.md` 更新为最终符号，不包含已删除 API。

#### P4-T03（P1）SQLite 真实执行缺口

- 目标：在已有 1～10 Root/Join 基础上补混合 From+Join、非泛型多映射、同步/异步终结对称、同步 Dispose 异步创建多结果集、取消/失败资源恢复。
- 步骤：
  1. 保留现有 1～10 真执行，不复制矩阵。
  2. 增加多根 FROM + Join 混合、自连接 alias、FromSubquery/JoinSubquery、DTO、分页、流式和事务组合。
  3. 异步创建结果默认 `await using`；单独测试调用同步 `Dispose()` 时是否发生阻塞、异常聚合和租约释放。
  4. 审计 `SqlMultipleQueryResult.Complete()` 的 `GetAwaiter().GetResult()`；若可避免，设计同步/异步完成回调对称路径；若合同明确允许同步等待，必须文档化并用受控测试锁定，不允许 UI/ASP.NET 上下文死锁。
- 验收：SQLite 默认运行，全链路资源在异常、取消、提前退出后可重试。

#### P4-T04（P1）共享跨 Provider 1～10 表合同

- 目标：MySQL、PostgreSQL、SQL Server、SQLite 复用同一查询合同，不创建第二套测试框架。
- 步骤：
  1. 在现有 `Bing.Test.Shared`/`Bing.TestShare*` 或最接近的 Integration shared helper 中定义 Provider 能力参数化合同。
  2. 覆盖 identifier quote、schema、alias、参数前缀、1/2/5/10 Root、1/2/5/10 Join、分页、物化。
  3. SQLite 始终执行；外部 Provider 使用现有 `IntegrationFact`、连接变量和安全数据库校验。
  4. Oracle/Doris 根据能力 profile 和 Gate 选择成功或明确 NotSupported 合同。
- 风险：外部数据库缺凭据时不能宣称 PASS；报告需区分 `CONTRACT_COMPILED`、`GATE_SKIPPED`、`EXECUTED_PASS`。
- 验收：至少 SQLite 真执行；MySQL/PostgreSQL/SQL Server 合同已接入各自门控项目，环境可用时通过。

### Phase 5：性能、GC 与数据驱动优化

#### P5-T01（P0）建立可比较前基线

- 目标：任何性能改动前保存当前同机基线。
- 场景：1/2/5/10/20 Root 和 Join；10/50/100/1000 参数；重复 ToSql；WhereIf true/false；动态过滤；Clone；GetPlan；参数快照；Dapper 参数绑定；分页 Count/Data；同步/异步流；多结果集；Activity/Listener/Trace 四种诊断组合。
- 步骤：扩充 Benchmark 参数；确保 setup 不计入目标方法；使用固定数据和 ShortRun/正式 Job 分层；保存 BDN artifact 和任务报告。
- 指标：Mean、Error、StdDev、P50/P95（可由 artifact/自定义列提供）、Allocated、Gen0/1/2、LOH/大对象阈值说明。
- 验收：前基线带代码/diff 指纹、硬件、OS、Runtime、BDN 和命令。

#### P5-T02（P1）热点归因与最小优化

- 目标：只优化 Benchmark 和 allocation profile 证明的热点。
- 候选：`SqlQuery.GetPlan` Builder Clone、Text/Procedure 参数重复快照、WhereGroup 全 Builder Clone、From/Join 参数 Probe、来源解析 LINQ 临时集合、诊断消息/参数列表、ExecutionId 创建边界。
- 步骤：
  1. 将诊断分为最小 Activity 身份与 Listener/Trace 完整快照。
  2. 明确参数所有权，在构造、getter、计划之间避免重复深复制，同时保持外部不可变。
  3. 评估冻结计划、写时复制、结构共享和只读渲染缓存；每项先补正确性测试。
  4. 只有数据证明后才考虑 Span、ArrayPool、Object Pool、FrozenDictionary 或 ValueTask。
- 验收：至少一个真实热点有量化改善；没有改善的候选记录“不实施”及原因。

#### P5-T03（P1）后基线与回归判定

- 目标：同机同配置重跑全部场景并与前基线对比。
- 验收：`benchmark-report.md` 给出绝对值、百分比、噪声/方差、分配和 GC；任何显著回退要修复或明确拒绝发布。

### Phase 6：注释、文档、生成策略和发布准备

#### P6-T01（P1）中文 XML 文档

- 目标：按 `chinese-comments` Skill 完成本次修改范围的 public/protected/interface，以及必要 internal/private 约束注释。
- 要求：接口先补契约，实现优先 `inheritdoc`；多映射说明泛型顺序和 splitOn；异常、线程安全、取消、Dispose 和资源所有权必须明确。
- 验收：XML 与签名一致，无旧泛型 Root/高元数/As 示例。

#### P6-T02（P1）用户文档和迁移

- 更新 README 查询快速开始、查询使用、Lambda 使用、Integration、Release Notes、设计、API 治理和迁移文档。
- 必含：唯一 Root、命名 alias/schema、FromTable、2～7 多映射、ToEntity 基数语义、不可并发共享、CancellationToken、`await using`、Breaking Change、Runtime SPI 扩展边界。
- 验收：源码/样例/文档搜索无旧入口；所有示例可由 Roslyn 契约编译。

#### P6-T03（P2）Source Generator 决策

- 目标：比较手写、Incremental Generator、MSBuild 生成和检入生成代码。
- 决策门槛：只有 2～7 多映射重复确实造成维护成本，且 Generator 可生成 XML、检测重复签名、失败即构建失败、具备 Roslyn/快照测试时才实施。
- 默认策略：本任务优先手写稳定 API；Generator 为可选后续，不得阻塞发布，也不得生成 1～10 Lambda 查询类型。
- 验收：`source-generator-decision.md` 有明确 `ADOPT/DEFER/REJECT` 和证据；DEFER/REJECT 时不创建 Generator 项目。

### Phase 7：最终验证与发布验收

#### P7-T01（P0）完整回归

按依赖顺序执行并记录：

```powershell
dotnet restore .\Bing.All.sln
dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.MySql.Tests\Bing.Dapper.MySql.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests\Bing.Dapper.PostgreSql.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal --no-build
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal --no-build
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*"
git diff --check
```

- 外部 Gate 可用时追加 MySQL/PostgreSQL/SQL Server Integration；Oracle/Doris 按能力和 Gate。
- 如果 build 后输出路径/TFM导致 `--no-build` 不适用，应使用真实项目配置调整并在报告说明，不得伪造通过。

#### P7-T02（P0）最终 API/安全/维护性审计

- 检查 public types/extensions、Public API baseline、`NotImplementedException`、`.Result/.Wait/GetAwaiter().GetResult`、生产友元、CancellationToken、XML、未使用代码、完整 SQL断言和参数断言。
- 检查 SQL 外部值均参数化；表名/schema/alias 原始入口只接受受信结构名并沿用现有验证，不新增注入路径。
- 检查任务工作文档与实际命令一致。

#### P7-T03（P0）发布结论

- `review-report.md` 给出内部 Review；`final-summary.md` 给出修改/删除/新增 API、Breaking Change、迁移、测试、Benchmark、未验证项、完成度和发布结论。
- 只有无 P0/P1 未处理缺陷、SQLite 默认集成通过、Public API 门禁通过、性能无已知显著回退时，才可标记 `READY_FOR_INDEPENDENT_REVIEW`。
- 外部 Gate 缺失可以标记 `READY_WITH_EXTERNAL_VALIDATION_REQUIRED`，不能标记所有 Provider 已执行通过。

## 9. Phase 内部 Review 规则

每个 Phase 完成后立即自检并继续，不等待人工确认：

- 是否新增兼容入口或重复 API；
- 是否扩大 public SPI；
- 是否增加生产 `InternalsVisibleTo`；
- 是否遗漏 null/invalid 参数校验；
- 是否保持同步/异步、timeout、CancellationToken 和异常一致；
- 是否出现 `.Result`、`.Wait()` 或不受控同步等待异步；
- 是否破坏 Clone、缓存、来源图、参数和诊断隔离；
- 是否把完整 SQL断言降为 `Contains`；
- 是否遗漏 XML 注释或保留过期示例；
- 是否产生未使用代码、第二套实现或无数据性能优化。

发现问题后在当前 Phase 范围内修复并重跑最小验证；阻塞项记录后继续不依赖任务。

## 10. 风险与缓解

| 风险 | 等级 | 缓解 |
| --- | --- | --- |
| 当前工作区含上一任务未提交大改动 | HIGH | 基线记录 diff 指纹；只增量修改；禁止 restore/reset/checkout |
| 删除 `SqlQuery<TResult>` 影响 Builder 子查询生态 | HIGH | 先迁移每类子查询合同和直接测试，再删除类型 |
| 动态编译测试 reference 失真 | HIGH | 使用明确 ProjectReference 和去重 MetadataReference，模拟真实 consumer |
| 多映射重载导致 API 爆炸和 Analyzer 警告 | MEDIUM | 仅 2～7 Dapper 原生范围；统一签名；Source Generator 仅决策后实施 |
| Runtime namespace 迁移扩大 Breaking Change | HIGH | 导出消费者矩阵，一次迁移官方调用和 Public API，提供 migration |
| 外部 Provider 无凭据 | MEDIUM | 接入门控合同，状态区分编译/跳过/执行，不连接生产库 |
| 同步 Dispose 等待异步完成回调 | HIGH | 增加受控死锁/租约测试，优先建立同步完成回调，不静默阻塞 |
| 参数快照优化造成可变状态泄漏 | HIGH | 所有优化前补快照/Clone/并发测试，Benchmark 与正确性双门槛 |
| 大文件拆分产生低价值 diff | MEDIUM | API 稳定后按职责移动；不混入行为改动；分项目验证 |
| 文档与源码再次漂移 | MEDIUM | 文档示例进入 Roslyn 编译契约，最终源码搜索旧 API |

## 11. 最终验收 Checklist

- [ ] 非泛型 Root + 终结泛型是唯一普通查询路径。
- [ ] `SqlAdvancedQueryExtensions` 和 Root 泛型扩展已删除。
- [ ] `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>`、`SqlProcedureQuery<TResult>`、`SqlQuery<TResult>` 已删除。
- [ ] `.As<TResult>()` 和 3+ 参数 Lambda API 不存在。
- [ ] 非泛型 Query/Sql 支持同步/异步 2～7 Dapper 多映射。
- [ ] 动态编译契约同时引用 Data.Sql、Dapper Core 和真实必需程序集。
- [ ] 目标查询链无不合理 `NotImplementedException`。
- [ ] 所有 public mutator 有缓存和失败原子性直接测试。
- [ ] Unit 1～10 Root、2～10 Join 完整 SQL和参数通过。
- [ ] SQLite 1～10 Root/Join 和补充混合/资源场景真实执行通过。
- [ ] MySQL/PostgreSQL/SQL Server 共享合同已接入门控 Integration。
- [ ] 无新增生产 `InternalsVisibleTo`。
- [ ] Runtime SPI、文件、目录、命名空间完成职责拆分。
- [ ] CancellationToken、并发拒绝、异常和 Dispose/DisposeAsync 资源恢复有直接证据。
- [ ] Benchmark 有同机优化前后报告和量化结论。
- [ ] public API 中文 XML、README、示例、迁移和 Release Notes 与最终代码一致。
- [ ] 全方案 Release build、核心 Unit、SQLite Integration、Public API Analyzer 和 `git diff --check` 通过。
- [ ] 没有 P0/P1 未处理缺陷；外部未验证项明确记录。

## 12. 完成与交接

本计划完成后不在 Planner 阶段实施。下一步使用“开始实施” Handoff 进入 `plan-executor`，或执行：

```text
/execute-plan BING-SQL-API-HARDENING-20260824-001
```

Executor 必须持续完成实现、测试、Benchmark 和工作文档，不自动 commit/push/PR/发布。
