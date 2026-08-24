# Bing SQL 查询 API 收敛、正确性与性能治理实施计划

Status: APPROVED_FOR_EXECUTION

## 1. 任务信息

- Task ID：`BING-SQL-QUERY-REFACTOR-20260824-001`
- 计划日期：2026-08-24
- 任务类型：API 收敛、正确性修复、测试补齐、性能治理、发布前整理
- 重点程序集：`Bing.Data.Sql`、`Bing.Dapper.Core`、各 Dapper Provider、Tests、Integration Tests、Benchmarks
- Breaking Change：允许；本计划按主版本破坏性收敛处理，不为无明确价值的旧查询 API 新增 `[Obsolete]` 或转发层
- 自动提交：禁止；执行器不得执行 `git add`、`git commit`、`git push`、PR、Tag、Release、stash、reset、clean 或覆盖用户已有修改
- 本轮 Planner 写入范围仅为本文件，未修改业务代码、测试、配置、数据库或现有审查报告

## 2. 输入、适用规范与冲突处理

### 2.1 已读取依据

- 根规范：`AGENTS.md`
- Copilot 规范：`.github/copilot-instructions.md`
- 计划规范：`.github/prompts/create-plan.prompt.md`
- 中文 XML 注释规范：`.github/skills/chinese-comments/SKILL.md`
- 既有任务：
  - `ai_docs/tasks/sql-query-dev-v6-api-refactor/plan.md`
  - `ai_docs/tasks/sql-query-dev-v6-api-refactor/execution.md`
  - `ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
  - `ai_docs/tasks/sql-lambda-query-api-v4/review.md`
- 设计与治理：
  - `ai_docs/sql-lambda-query-design.md`
  - `ai_docs/sql-public-api-governance.md`
  - `docs/sqlquery-lambda-usage.md`
  - `docs/sqlquery-usage.md`
  - `docs/integration-testing.md`
- 当前源码、Public API 基线、Unit/Integration Tests 与 Benchmark 源码

### 2.2 需求冲突与裁决

1. 用户提示要求计划完成后持续实施；当前会话处于 `plan-writer` 模式，唯一允许写入目标是本 `plan.md`。本轮以更高优先级模式约束为准，完成计划后停止，由 `/execute-plan`、`/run-plan` 或 `$execute-plan` 进入实施。
2. 现行 `ai_docs/sql-public-api-governance.md` 与既有 review 将 `ISqlQuery.From<TEntity>(string alias = null)`、`SqlLambdaQuery<TEntity>`、泛型 Raw/Procedure 等部分入口按 Shipped 兼容边界保留；本次最新需求明确允许 Breaking Change，并要求删除这些兼容路径。实施时以本次要求为准，但必须：
   - 先核对实际发布版本、`PublicAPI.Shipped.txt`、仓库消费者与发布策略；
   - 作为显式主版本 Breaking Change 同步迁移消费者、Public API 基线、发行说明和迁移文档；
   - 不得只删除 Shipped 条目来规避 Analyzer，也不得留下双重推荐路径。
3. `AGENTS.md` 推荐使用 Python 做 UTF-8 批量写入，但执行 Agent 的编辑规则可能要求使用受控补丁接口。实施时无论采用何种受控编辑方式，都必须保持 UTF-8，不使用 PowerShell 默认编码写中文文件。
4. 用户给出的整体完成度基线约为 70%～75%。当前源码证明此前 dev_v6 任务已完成大量正确性工作，因此该区间对“本次最终目标”仍基本合理，但不能理解为核心功能只有骨架。

## 3. 技术栈与验证边界

- C# / SDK 风格 .NET 项目；`Bing.Data.Sql` 通过仓库 props 继承目标框架配置，测试主要覆盖 `net6.0;net8.0`。
- 测试框架：xUnit；测试项目复用 `Bing.Test.Shared`。
- 公共 API 门禁：`Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4`，`RS0016/RS0017/RS0018` 不得通过关闭规则规避。
- Benchmark：BenchmarkDotNet `0.14.0`，`net8.0`，已启用 `MemoryDiagnoser`。
- SQLite 集成测试使用临时数据库并应始终可运行；MySQL、PostgreSQL、SQL Server、Oracle 使用既有环境 Gate 和专用测试库安全校验，禁止猜测凭据或使用生产库。
- SQL 输出测试必须断言完整 SQL 字符串；涉及 Provider、映射、缓存、Builder、运行时创建链时必须维护生产符号到测试方法的追溯映射。

## 4. 当前实现与完成度判断

### 4.1 已真实实现并应保留

| 能力 | 当前证据 | 判断 |
| --- | --- | --- |
| 非泛型 Lambda 查询核心 | `SqlLambdaQuery.NonGeneric.cs` 已提供连续 `From<TEntity>`、一元/二元 Select/Where/Join/GroupBy/OrderBy/Having 和终结方法 | 已真实实现，不是骨架 |
| 连续多根表 | `FromClause.AppendRoot` 与公开连续 `From<TEntity>` 已可生成逗号根表；SQLite Unit 已有双根表完整 SQL | 已实现，需补足公开 API 1～10 矩阵 |
| 原子 Join | Join 在候选状态解析参数、别名和来源，旧 V4 review 已验证失败回滚；当前非泛型 API 调用该核心 | 已实现，需补显式左右别名和 2～10 推荐 API 契约 |
| 查询生命周期 | `SqlQuery` 已有 Draft/Frozen/Executing/Completed、执行租约、失败恢复、重复执行和流式释放 | 已实现且有 Unit/SQLite Integration 证据 |
| Clone 隔离 | `SqlLambdaQuery.Clone()` 创建独立 Builder 和新 QueryContextId，Parent 指向来源 | 已实现且有 Unit/SQLite 真实执行测试 |
| 实例级 SQL 缓存 | `_shapeVersion/_cachedVersion/_cachedSql` 与 `Touch()` 已存在；动态过滤要求快照时绕过缓存 | 已实现基础正确性，不是全局缓存 |
| Context/Execution 诊断 | `SqlQueryPlan`、`DiagnosticsMessage`、Dapper 诊断链已包含 QueryContextId、Parent、ExecutionId、Phase、Trace/Span | 已实现主要链路 |
| Runtime 跨程序集解耦 | `Bing.Data.Sql` 已移除对 `Bing.Dapper.Core` 的生产友元；Dapper 经公开 Plan/Bridge/SPI 协作 | 已部分完成 |
| 非泛型 Raw | `SqlTextQuery` 已支持终结阶段选择 TResult，SQLite 覆盖 ToEntity/List/Dictionary/Page | 已实现主路径 |
| SQLite 真实执行 | 既有执行报告记录 Unit、SQLite Integration、分页、流式、取消、Clone、动态过滤均通过 | 已有较强证据，但需按最终 API 重跑 |

### 4.2 部分完成或与本次目标冲突

| 范围 | 当前问题 | 状态 |
| --- | --- | --- |
| 唯一 `From<TEntity>` | `ISqlQuery` 同时公开两参数非泛型入口和一参数默认值泛型兼容入口；普通 `From<TEntity>()` 静态解析仍返回 `SqlLambdaQuery<TEntity>` | 未达到唯一入口目标 |
| 兼容包装器 | `SqlLambdaQuery.Legacy.cs` 仍定义 `SqlMultiLambdaQuery`、`SqlLambdaQuery<TEntity>`；非泛型 façade 仍含多个 `Legacy*` 转发方法 | 应删除 |
| 早期结果泛型入口 | `ISqlQuery.Query<TResult>()`、`Sql<TResult>()`、`SqlInterpolated<TResult>()`、`Procedure<TResult>()` 仍公开，且 Public API 基线有记录 | 与目标冲突，需逐类收敛 |
| 别名解析 | Join 已区分 `alias` 与 `leftAlias`，但 `alias` 实际代表右来源；Where/Select/GroupBy/OrderBy/Having 多数仍依赖类型唯一或 Lambda 参数名匹配 | 部分完成，仍有隐式约定 |
| `WhereIf` | 当前签名仍为 predicate/value 在前、condition 在后 | 与条件优先目标冲突 |
| 条件组 | `WhereGroup(Action<ISqlConditionGroup>)` 支持 And/Or/Group，但嵌套 `Group` 固定按 AND 接入，未公开明确 `AndGroup`/`OrGroup` | 部分完成 |
| 终结 API | `ToEntity` 直接委托 `SingleOrDefault`；同时保留 First/Single/SingleOrDefault；`ToDictionary` 先完整物化 List 再转换 | 语义重复及额外峰值内存待治理 |
| 诊断分配 | `IsExecutionContextRequired()` 仍以 `Logger != NullLogger.Instance` 判断，未检查实际日志级别；可能在无启用日志时创建消息、参数快照和 Scope | 明确正确性/性能缺口 |
| SPI 可见性 | `ISqlQueryPlanExecutor` 暴露 2～7 多映射重载；`SqlBuilderRuntimeBridge`、`SqlQueryRuntimeFactory` 等公开面较大且在根命名空间 | 边界仍需收敛 |
| 生产友元 | Data.Sql 已无 Dapper Core 友元，但仍向若干 Provider 测试程序集开放 internal；Dapper Core 只向 Tests 开放 | 原则上可接受，但需逐项证明均为测试/Benchmark |
| Benchmark | Root/Join Benchmark 每次 `BuildQuery()` 都调用 `DispatchProxy.Create`；“DynamicFilterRender” 实际只是普通 `Where`；Clone 测的是 internal Builder Clone 而非公开 query Clone | 基准失真，不能用于优化结论 |
| 大文件 | `SqlBuilderBase.cs`、`JoinClause.cs`、`SqlLambdaQuery.NonGeneric.cs`、`SqlQuery.cs`、`ISqlQueryBuilderSource.cs`、`SqlBuilderRuntimeBridge.cs` 职责密集 | 可维护性需治理 |
| XML 注释 | 大量新增 API 仅有短 summary，私有/internal 成员缺注释，返回/参数/异常语义不完整；Legacy 注释与最终目标冲突 | 未达到本次规范 |

### 4.3 整体完成度

- 核心查询构建、执行、生命周期、隔离和 SQLite 链路完成度约 **80%～85%**。
- 本次要求的最终 API 单一路径、Breaking Change、显式别名、终结 API 治理、Runtime 公共面、Benchmark 和发布准备完成度约 **55%～65%**。
- 综合完成度约 **72%～78%**，与用户给出的 70%～75% 基线基本一致。剩余工作主要是破坏性收敛和发布级证据，而非从零实现 SQL 查询。

### 4.4 性能、资源和维护性判断

- 当前不是 0 GC，且不得宣传为 0 GC。
- 已确认的分配来源包括：`SqlQuery.GetPlan()`/执行快照 Builder Clone、动态过滤快照、分页 Count/Data Builder 派生、`WhereGroup` 整 Builder Clone、Join 候选集合复制、来源解析中的 `ToList/FirstOrDefault/Count`、参数快照数组、`ToDictionary` 的 List + Dictionary 双持有。
- 当前实例缓存只缓存稳定 SQL 字符串，动态环境旁路；没有证据表明存在跨查询全局缓存污染，也不应在本任务引入全局 SQL 缓存。
- `SqlLambdaQuery.NonGeneric.cs` 同时承担公开 façade、兼容桥、终结 API、来源解析和 Fluent 操作；`SqlBuilderRuntimeBridge.cs` 同时承担快照、分页、Raw 分页、Mutation 校验与 From mutation，耦合偏高。
- API 使用体验当前不合理之处最明显的是必须写 `From<TEntity>(null, null)` 才能进入非泛型主路径，以及同类多来源时部分操作仍依赖 Lambda 参数名。

## 5. 目标 API 与设计决策

### 5.1 唯一查询入口

最终仅保留：

```csharp
SqlLambdaQuery From<TEntity>(string alias = null, string schema = null);
```

`From<TEntity>()`、`From<TEntity>("o")` 和 `From<TEntity>("o", "sales")` 均返回同一非泛型类型。删除 `SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery`、Legacy 文件/工厂/转发方法。连续 `From` 无人为表数上限。

### 5.2 结果类型后置

- 删除或合并查询起始阶段的 `Query<TResult>()`、`Sql<TResult>()`、`SqlInterpolated<TResult>()`、`Procedure<TResult>()`。
- `Query()`、`Sql()`、`SqlInterpolated()`、`Procedure()` 返回非泛型描述。
- 结果类型只在 `ToEntity<TResult>()`、`ToList<TResult>()`、分页、标量和流式等终结方法确定。
- 不引入 `.As<TResult>()`。
- Dapper 2～7 多映射如确有保留价值，应移到明确的低层 Advanced/Runtime API，不继续污染常用查询 façade；若仓库消费者为零则删除。

### 5.3 显式来源别名

- Join 参数最终命名为 `leftAlias`、`rightAlias`、`schema`，不再用含义模糊的 `alias` 表示右来源。
- Where、Select、AppendSelect、GroupBy、OrderBy、Having、Aggregate 至少提供单来源显式 alias 入口；二元操作提供 `firstAlias/secondAlias` 或等价来源句柄。
- 参数名推断仅作为唯一类型来源时的便捷行为；同类型多来源必须显式定位，错误消息不得要求调用方修改 Lambda 参数名。
- 不引入携带 T1...TN 的查询对象；如 alias 参数导致重载爆炸，可引入轻量只读 `SqlSource<TEntity>` 句柄，但必须先由 API Contract 原型证明开发体验更清晰。

### 5.4 条件与终结语义

- `WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate)` 条件优先；参数条件同样把 condition 放在首位。
- 不增加 `Func<Expression<...>>` 重载；高频调用方使用普通 `if`。
- 将条件组改为明确的 `AndGroup`、`OrGroup` 或 `Group(SqlLogicalOperator, ...)`，覆盖空组、嵌套组和优先级。
- `ToEntity<TResult>()` 固定为 0 行返回 default、1 行返回结果、超过 1 行抛异常。
- 高层 façade 删除与 `ToEntity` 完全重复的 `SingleOrDefault`；`FirstOrDefault` 仅在能限制读取一行且语义确实有价值时保留。
- 高层 `ToDictionary` 若仍先 ToList 再转换则删除；只有实现直接、清晰且资源行为可验证时保留。

### 5.5 生命周期、缓存与诊断不变量

- 保留 Draft/Frozen/Executing/Completed、Clone Parent 和执行租约语义。
- 所有 mutator 统一走可审计的 mutation/version 入口；失败、空组和 `WhereIf(false)` 不递增版本。
- 动态过滤继续旁路不安全的稳定 SQL 缓存；不新增全局缓存。
- 每个逻辑描述拥有 QueryContextId；每次终结、Count/Data、流式执行拥有独立 ExecutionId；Clone 新建 ContextId 并将 Parent 指向来源。
- `IsExecutionContextRequired()` 必须基于 DiagnosticListener、Activity 与实际启用的日志级别；无消费方时不创建参数诊断、消息、字典或 Scope。
- 默认日志不记录敏感参数值。

## 6. 文件范围

### 6.1 已确认生产文件

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.Legacy.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOfT.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.NonGeneric.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlProcedureQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/ISqlQueryBuilderSource.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/ISqlQueryPlanExecutor.PagingStreaming.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryPlan.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlConditionGroup.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeFactory.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlBuilderRuntimeBridge.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeBinding.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQueryRuntimeBindingController.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
- `framework/src/Bing.Data.Sql/AssemblyInfo.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Diagnostics.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.QueryPlan.*.cs`
- `framework/src/Bing.Dapper.Core/AssemblyInfo.cs`
- `framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt`
- `framework/src/Bing.Dapper.Core/PublicAPI.Unshipped.txt`

### 6.2 已确认测试与 Benchmark 文件

- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryPlanContextTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/WhereGroupAtomicityTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj`

### 6.3 候选文件

仅在引用矩阵证明受影响后修改：

- `framework/src/Bing.Dapper.MySql|PostgreSql|SqlServer|Sqlite|Oracle/**`
- 各 Provider Unit/Integration Tests 和 Public API 基线
- `framework/src/Bing.EntityFrameworkCore/**`、`modules/admin/**`、`samples/**`
- `framework/src/Bing.Data.Sql.Analyzers/**` 与 Analyzer Tests
- `ai_docs/sql-lambda-query-design.md`
- `ai_docs/sql-public-api-governance.md`
- `ai_docs/sql-metadata-test-traceability.md`
- `docs/sqlquery-usage.md`
- `docs/sqlquery-lambda-usage.md`
- `docs/ReleaseNotes.md`
- `docs/testing/database-integration-tests.md`
- Benchmark 新场景文件与结果 artifact

## 7. 分阶段实施计划

共同执行规则：每个 Phase 先更新 `execution.md` 状态，再定位真实调用链；优先增加能暴露问题的测试；完成最小完整修改后运行定向测试和受影响项目全量测试；记录命令、TFM、测试数、警告、失败与风险；不得在单个 Task 后等待确认。

### Phase 0：基线与证据确认

#### P0-T01（P0）工作区、发布基线与消费者矩阵

- 目标：确认当前未提交修改、实际发布 API 状态、仓库消费者和外部 Gate，避免覆盖用户工作或错误删除 Shipped API。
- 依赖：无。
- 证据：当前治理文档与本次 Breaking Change 要求冲突；Planner 无实时 Git 命令输出，必须由 Executor 重采集。
- 步骤：
  1. 读取 execute-plan Skill；记录 `git status --short`、`git diff --stat`、相关文件 diff。
  2. 导出 `ISqlQuery`、`SqlLambdaQuery*`、Raw/Procedure、Runtime SPI 的 Shipped/Unshipped/消费者矩阵。
  3. 搜索仓库所有旧 API 调用，区分生产、测试、文档、生成物和 bin/obj。
  4. 记录 SDK、Runtime、CPU、OS、TFM、BDN 版本和外部 Provider Gate。
  5. 运行当前 build、核心 Unit、SQLite Integration 和 Benchmark Dry，保存历史失败而不掩盖。
- 测试：不修改行为；建立基线结果。
- 风险：工作区可能包含上一任务未提交的大范围改动；只与其协作，不回滚。
- 验收：形成 API/消费者/发布/验证矩阵，每个用户事实基线标记为 Confirmed、Changed 或 Not Verifiable。

#### P0-T02（P0）测试追溯与缺口盘点

- 目标：建立最终生产符号到测试方法映射，识别哪些 1～10 表测试仍走兼容 API/internal/`SetRoots`。
- 依赖：P0-T01。
- 步骤：扫描 Unit、SQLite Integration、外部 Provider Integration、诊断与 Benchmark；逐例记录入口类型、是否 public API、是否完整 SQL、是否真实执行。
- 验收：`execution.md` 有 P0/P1/P2 测试矩阵和明确缺口，不以测试文件名推断覆盖。

### Phase 1：正确性、隔离与诊断

#### P1-T01（P0）统一 mutation/version 与缓存失效矩阵

- 目标：所有成功 mutator 必须失效实例 SQL 缓存，失败/空操作不得 Touch。
- 依赖：P0-T02。
- 修改范围：`SqlQuery.cs`、`SqlLambdaQuery.NonGeneric.cs`、From/Join/Select/Where/Group/Order/CTE/Page 相关修改器及直接测试。
- 步骤：
  1. 枚举全部 public/internal mutator，建立 `MutationKind -> Touch -> SQL/Parameter impact` 表。
  2. 将散落的 `Touch()` 收敛为可审计的 mutation API；失败候选仅成功提交后更新版本。
  3. 覆盖参数、别名、Select、Join、OrderBy、GroupBy、Having、CTE、分页、From、Clear、Distinct、Skip/Take。
  4. 保持动态过滤环境旁路缓存，不引入全局缓存。
- 测试：每个 mutator 命中/失效/失败不失效；完整 SQL、参数名称/值/顺序；Clone、Count/Data、动态过滤隔离。
- 风险：Touch 时机变化可能造成旧 SQL 命中或重复渲染。
- 验收：缓存失效矩阵全部通过，缓存不持有 Builder、连接、事务、参数值或诊断上下文。

#### P1-T02（P0）Clone、并发和执行资源隔离加固

- 目标：固定来源/Clone 双向修改、参数、分页、子查询、流式和并发不变量。
- 依赖：P1-T01。
- 步骤：补齐源改 Clone 不变、Clone 改源不变、同逻辑模板并发参数不覆盖、取消/异常/Dispose 后租约恢复；验证 QueryContext Parent 语义。
- 测试：Unit + SQLite 真实执行；同步/异步/流式一致性；CancellationToken 传递。
- 验收：所有隔离和资源释放路径通过，无跨查询状态污染。

#### P1-T03（P0）修复诊断按需创建与身份传播

- 目标：无订阅、无 Activity、无启用日志时不创建执行消息、参数诊断和 Scope；所有启用通道共享身份。
- 证据：当前 `IsExecutionContextRequired()` 仅判断 Logger 不是单例 NullLogger。
- 修改范围：Dapper Core Diagnostics/Preparation/Terminal/Paging/Streaming，Data.Sql Diagnostics/Plan。
- 步骤：
  1. 使用实际 `Logger.IsEnabled` 的最低必要级别和 DiagnosticListener/Activity 状态判定。
  2. 在禁用路径避免构造参数列表、字典、Scope 和 DebugSql。
  3. 固定 QueryContextId、Parent、ExecutionId、Phase、TraceId/SpanId 在 Before/After/Error、日志、Activity、同步/异步/流式中的一致性。
  4. 默认日志只保留结构化身份与安全元数据，不泄露参数值。
- 测试：全部关闭、仅 Logger 各级别、仅 Activity、仅 Listener、组合、错误、取消、Count/Data、流式。
- 验收：禁用路径无执行消息；启用路径身份一致；每次实际执行 ExecutionId 独立。

### Phase 2：API 收敛与 Breaking Change

#### P2-T01（P0）删除泛型 Lambda 兼容路径并统一 From

- 目标：所有 `From<TEntity>` 重载返回非泛型 `SqlLambdaQuery`。
- 依赖：P0-T01、P1 全部。
- 步骤：
  1. 删除 `SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery`、`SqlLambdaQuery.Legacy.cs` 和 `Legacy*` 转发。
  2. 将 `ISqlQuery`/`SqlQueryBase` 收敛为唯一可选参数入口。
  3. 迁移仓库生产消费者、Unit/Integration、samples/docs。
  4. 更新 Shipped/Unshipped，显式记录主版本 Breaking Change。
- 测试：推荐 API 正向编译；旧类型、旧返回类型和 Legacy Factory 负向 Roslyn 编译；1～10 连续 From 完整 SQL。
- 风险：已发布 ABI 删除；必须有迁移表和主版本发布说明。
- 验收：`From<TEntity>()` 与 `From<TEntity>("o")` 静态返回类型一致；程序集不再导出泛型 Lambda 包装器。

#### P2-T02（P0）删除早期结果泛型入口

- 目标：Query/Sql/Interpolated/Procedure 均由终结方法选择结果类型。
- 依赖：P2-T01。
- 步骤：
  1. 审计 `Query<TResult>`、`Sql<TResult>`、`SqlInterpolated<TResult>`、`Procedure<TResult>` 和相关描述类型消费者。
  2. 新增或确认非泛型 `Query()`、`Procedure()` 描述；迁移所有普通消费者。
  3. 将低层 Dapper 多映射能力移至明确 Advanced API 或在无消费者时删除。
  4. 删除重复泛型工厂和 Runtime Factory 方法。
- 测试：正向终结 TResult；旧入口负向编译；Raw/Procedure 0/1/多行、输出参数、同步/异步、取消。
- 验收：常用 façade 不再在起始阶段固定结果类型；无 `.As<TResult>()`。

#### P2-T03（P0）显式别名与组合式一元/二元 API

- 目标：消除 Lambda 参数名必须匹配 SQL alias 的正确性依赖。
- 依赖：P2-T01。
- 步骤：
  1. Join 改为明确 `leftAlias/rightAlias/schema`。
  2. 为 Where、Select、AppendSelect、GroupBy、OrderBy、Having、Aggregate 提供显式来源定位。
  3. 同类型多来源无显式定位时 fail-fast；唯一类型可便捷推断。
  4. 评估 alias 参数与 `SqlSource<TEntity>` 句柄两种方案，以 API Contract 和调用样例择一，不同时保留双主路径。
- 测试：自连接、同类型多次 Join、重复子查询类型、alias 冲突、Lambda 参数名任意化、1～10 来源稳定性。
- 验收：正确 SQL 不依赖参数变量名；公开表达式输入最多二元。

#### P2-T04（P0）WhereIf、条件组与终结 API 治理

- 目标：消除参数顺序和终结语义重复。
- 依赖：P2-T02、P2-T03。
- 步骤：条件优先 WhereIf；实现 AndGroup/OrGroup；固定 ToEntity；审计并删除重复 SingleOrDefault；仅保留有价值的 FirstOrDefault；删除或直接化 ToDictionary；同步/异步签名对称。
- 测试：true/false、空组、AND/OR 优先级、嵌套组、0/1/2 行、重复键、空集合、非法 selector、取消。
- 验收：同一语义只有一套推荐终结方法，无仅参数顺序不同的重复重载。

### Phase 3：运行时边界与目录重构

#### P3-T01（P0）Runtime SPI 和 InternalsVisibleTo 治理

- 目标：生产程序集无友元依赖，普通用户 IntelliSense 不暴露执行内部细节。
- 依赖：Phase 2 API 稳定。
- 步骤：
  1. 审计所有 `InternalsVisibleTo`，只保留 Unit/Integration/Benchmarks/必要测试辅助程序集。
  2. 审计 `ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController`、`SqlQueryPlan`、Bridge、Factory、Binding 的真实跨程序集消费者。
  3. 将必要 SPI 移至 `Bing.Data.Sql.Runtime` 或 `Runtime.Integration`；使用 `[EditorBrowsable(EditorBrowsableState.Never)]` 降低普通 IntelliSense 干扰。
  4. 具体实现 internal；删除未消费公开桥接方法和多映射重载。
- 测试：程序集引用/反射契约、Dapper Core/Provider 编译、SQLite Runtime 绑定和执行。
- 验收：无生产 friend；public SPI 最小、职责明确，无 Builder/连接/事务逃逸。

#### P3-T02（P1）按职责拆分大文件

- 目标：降低 façade、核心、缓存、终结、快照和运行时协作耦合。
- 依赖：P3-T01。
- 建议拆分：
  - `SqlLambdaQuery`：Facade、Sources、Predicates、Projection、Grouping、Joins、Terminals
  - `SqlQuery`：Lifecycle、Cache、Plan、Terminals
  - `SqlBuilderRuntimeBridge`：Snapshots、Paging、Validation、SourceMutation
  - `SqlBuilderBase`：Clone、Rendering、Filters、Mutations、ParameterTokens
  - `JoinClause`：TypedJoin、SubqueryJoin、CandidateCommit
- 规则：先删除空转发层再移动；一个主要 public 类型一个文件；namespace 与目录一致；不借移动修改无关逻辑。
- 验收：全量编译通过，diff 可区分 API 变更和物理移动。

### Phase 4：测试体系补齐

#### P4-T01（P0）API Contract 与负向编译契约

- 目标：固定最终推荐入口并确保删除 API 无法使用。
- 测试：禁止 `.As<TResult>()`、`SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery`、早期泛型入口、Legacy Factory、3+ 参数表达式；允许唯一 From、终结 TResult、显式 alias、一元/二元组合。
- 验收：反射与 Roslyn 正负契约均通过，不依赖字符串扫描代替编译验证。

#### P4-T02（P0）1～10 表 Unit SQL 矩阵

- 目标：全部通过推荐 public API 构造，不调用 internal Builder 或 `SetRoots`。
- 矩阵：
  - 1 表：From、Where、Select、ToSql
  - 2 表：双根表、Join、自连接
  - 3～5 表：多根表、连续 Join、混合条件
  - 6～10 表：组合式一元/二元 API、别名稳定、参数无冲突
- 断言：完整 SQL、参数名称/值/顺序、alias/schema、异常后状态不变。
- 验收：1～10 From 与 2～10 Join 每个数量独立用例通过，不重新引入高元表达式。

#### P4-T03（P0）SQLite 真实执行矩阵

- 目标：验证 SQL 可执行、参数与映射正确、无跨查询污染。
- 范围：1～10 根表、2～10 Join、逗号根表 + Join、自连接、子查询 Join、动态过滤、Count/Data、分页、同步/异步/流式、取消、异常、Dispose、Context/Execution 传播。
- 数据设计：每表包含匹配、未匹配、干扰、null 和必要一对多行，避免弱笛卡尔积断言。
- 验收：SQLite 双 TFM 全绿，断言行数与映射内容。

#### P4-T04（P1）Provider 共享集成契约

- 目标：MySQL/PostgreSQL/SQL Server/Oracle 复用同一行为合同，Provider 差异仅由能力配置处理。
- 步骤：抽取测试辅助而非生产抽象；Gate 开启时运行真实执行；未配置时记录 `NOT_RUN_EXTERNAL_GATE_MISSING`。
- 验收：可用 Provider 通过；不可用 Provider 有明确环境原因，不修改 Skip 或猜测连接。

#### P4-T05（P2）规模、并发与属性测试

- 范围：10/100/1000 参数、大 IN、长 SQL、大数据量、高并发、诊断开关、Logger 级别、不同 TFM/Runtime、LOH 观察。
- 验收：无参数覆盖、死锁、共享状态污染或不可解释 LOH 增长；非确定场景不进入默认快速 Unit。

### Phase 5：Benchmark 与性能优化

#### P5-T01（P0）修复 Benchmark 真实性并保存基线

- 目标：测量查询热点，而不是代理创建和测试初始化。
- 步骤：
  1. 将 executor、builder services、metadata、filter、query template 初始化移到 `GlobalSetup`。
  2. 从被测路径移除 `DispatchProxy.Create`、容器创建和无关初始化。
  3. `DynamicFilter` 使用真实 `IDataFilter` 状态变化。
  4. Clone 测公开 `SqlLambdaQuery.Clone()`；增加 GetPlan、Snapshot、Render、Terminal preparation。
  5. 覆盖 1/2/5/10 表，10/100/1000 参数，诊断开/关。
  6. 记录 Mean、分布/P95、Allocated、Gen0/1/2、LOH、SQL 长度、Runtime、TFM、CPU、OS。
- 验收：基准可重复运行；旧/新在同机同配置比较；Dry 仅用于可运行性，不作为收益结论。

#### P5-T02（P1）按数据优化真实热点

- 候选热点：GetPlan 完整 Clone、动态过滤二次 Clone、WhereGroup Builder Clone、Join 集合复制、来源解析 LINQ/O(P×S)、表达式重建、ToDictionary 双持有、日志关闭分配。
- 原则：先测量后修改；优先删除重复 Clone、临时集合和关闭诊断时的分配；Span/ArrayPool/ValueTask/对象池/FrozenDictionary 仅在数据证明收益时采用。
- 测试：每项优化配正确性回归和前后 Benchmark；复杂优化添加中文设计注释。
- 验收：保留统计显著且无正确性回退的优化；不以 0 GC 为目标。

### Phase 6：注释、文档与发布准备

#### P6-T01（P0）中文 XML 注释治理

- 目标：按 chinese-comments Skill 完善本次修改范围所有类型、构造函数、具名方法、属性、字段、常量、枚举成员。
- 规则：上游契约先补完整，实现优先 `<inheritdoc />`；补齐 param/typeparam/returns/exception/remarks；void/Task/ValueTask 不写 returns；可空与 bool 说明条件；删除 Legacy/过期/机械注释。
- 验收：注释与最终签名、异常、生命周期、缓存和线程安全语义一致；生成的 public/protected API 也具备注释。

#### P6-T02（P0）迁移文档、追溯与发布基线

- 目标：文档只展示最终 API，不再要求 `From<TEntity>(null, null)`。
- 内容：最终调用示例、旧到新迁移表、删除/合并/internal 化清单、Breaking Change、条件组、别名、自连接、终结语义、诊断身份、缓存边界、Provider Gate、非 0 GC 声明。
- 文件：设计、治理、usage、lambda usage、traceability、ReleaseNotes、PublicAPI Shipped/Unshipped。
- 验收：生产符号到测试方法可追溯；示例可编译；Public API Analyzer 无未解释错误。

#### P6-T03（P0）最终回归与交付

- 目标：完成全量 build/test、可运行 Integration、Benchmark 对比、diff 和安全检查。
- 验收：
  - 全解 build 0 error；新增/受影响测试全绿。
  - SQLite 1～10 真实执行通过；外部 Provider 状态逐项记录。
  - Benchmark 有前后数据和 Allocation 结论。
  - 无生产 IVT、无旧兼容查询类型、无双重推荐 API。
  - `git diff --check` 通过；工作区保留修改；明确未 commit/push/PR/tag/release。

## 8. 用例矩阵摘要

| Given | When | Then | Mock 边界 |
| --- | --- | --- | --- |
| 单一实体来源 | From/Where/Select/ToList | 完整 SQL、参数、映射正确 | Unit Mock executor；Integration 真实 SQLite |
| 1～10 连续来源 | 重复 From | `FROM x, y, z...`，无上限类型 | 不 Mock Builder |
| 2～10 关联 | 连续二元 Join | SQL、alias、参数稳定 | Unit 真实 Builder；Integration 真实 DB |
| 同类型多来源 | 显式 alias 操作 | 正确绑定；缺 alias 明确失败 | 不依赖 Lambda 参数名 |
| WhereIf false/true | 条件构造 | false 无 mutation；true 一次提交 | 无需 Mock 表达式 |
| 嵌套条件组 | AndGroup/OrGroup | 括号和优先级正确；空组无变化 | 真实条件解析 |
| 动态过滤变化 | 重复 ToSql/执行 | 不命中过期 SQL，参数不污染 | Mock 仅过滤环境 |
| Clone | 双向修改/执行 | Builder、参数、Context 隔离 | Unit Mock executor + SQLite |
| 同一描述并发/流式 | 重入、取消、Dispose | 拒绝冲突并释放租约 | Unit 可控 executor + SQLite |
| 0/1/2 行 | ToEntity | default/实体/异常 | SQLite 真实数据 |
| 诊断全部关闭 | 执行查询 | 不创建消息/Scope/DebugSql | 测试 Logger/Listener/Activity |
| 诊断部分开启 | Before/After/Error | 身份一致、参数安全 | 仅 Mock 外部诊断消费者 |
| 10/100/1000 参数 | Render/GetPlan/Execute | 参数唯一、无明显 LOH 异常 | Benchmark/规模测试 |

## 9. 实际验证命令

执行 PowerShell 命令前设置 UTF-8 控制台编码。以下路径均来自当前仓库真实 solution/csproj：

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.MySql.Tests\Bing.Dapper.MySql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests\Bing.Dapper.PostgreSql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Oracle.Tests\Bing.Dapper.Oracle.Tests.csproj -c Release -nologo -v minimal

dotnet test .\framework\tests\Bing.Dapper.MySql.Tests.Integration\Bing.Dapper.MySql.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests.Integration\Bing.Dapper.PostgreSql.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests.Integration\Bing.Dapper.SqlServer.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Oracle.Tests.Integration\Bing.Dapper.Oracle.Tests.Integration.csproj -c Release -nologo -v minimal

dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release -nologo -v minimal
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*" --job Dry

git diff --check
git status --short
```

外部 Integration 命令只有在对应 `RUN_*_INTEGRATION_TESTS=true`、安全测试连接和项目要求的重置授权满足时才算实际执行；否则记录 `NOT_RUN_EXTERNAL_GATE_MISSING`。

## 10. 执行状态格式与交付要求

每个 Phase 使用：

```text
[task-id: BING-SQL-QUERY-REFACTOR-20260824-001]
[phase: Phase N]
[status: in_progress | completed | blocked]

已完成：
验证：
发现：
下一步：
```

最终 `execution.md` 必须包含：总体完成情况、最终 API 示例、删除/合并/重命名/internal 化清单、Breaking Change、目录调整、1～10 Unit/SQLite 结果、各 Provider Integration 状态、Benchmark 前后数据、GC/Allocation 结论、Context/Execution 设计、IVT 审计、中文 XML 注释检查、未完成项、`git status` 概览，并声明未执行 commit、push、PR、tag 或 release。

## 11. 完成定义

- `From<TEntity>()` 只有一个非泛型返回路径；不存在 `SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery`、Legacy Factory/文件。
- Query/Raw/Procedure 常用入口不在起始阶段固定结果类型；结果由终结方法选择。
- 所有常用 Lambda 操作支持明确来源定位，自连接和重复实体不依赖参数名。
- `WhereIf` 条件优先，条件组支持明确 AND/OR 嵌套，终结 API 无重复语义。
- 缓存、Clone、动态过滤、并发、分页、流式、取消和诊断身份不变量有直接测试。
- 1～10 From、2～10 Join 通过推荐 public API 的完整 SQL Unit 测试；SQLite 真实执行矩阵通过。
- Runtime SPI 最小化，生产程序集无 friend assembly，普通 IntelliSense 不暴露无意义内部协作 API。
- Benchmark 不包含代理/容器初始化失真，优化有同环境前后证据，不宣称 0 GC。
- 修改范围的中文 XML 注释、Public API 基线、设计/使用/迁移/追溯文档与最终实现一致。
- 全量验证无未解释错误，外部 Gate 缺失诚实记录，工作区修改留给用户审查提交。
