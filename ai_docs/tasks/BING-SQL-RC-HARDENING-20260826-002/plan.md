# Bing.Data.Sql / Bing.Dapper RC 加固实施计划

- task-id: `BING-SQL-RC-HARDENING-20260826-002`
- 状态: `pending`
- 计划日期: `2026-08-26`
- 任务类型: 正确性修复、Breaking API 收敛、测试与 CI 加固、Benchmark 治理、数据驱动低分配优化、发布准备
- 执行约束: 禁止 `git add`、`git commit`、`git push`、PR、历史改写、`git reset --hard`、`git checkout -- .` 或破坏性清理；保留所有既有工作树改动。
- Planner 写入边界: 本轮仅创建本计划。`progress.md`、`decisions.md`、`benchmark-baseline.md`、`verification-report.md` 必须由执行器在 Phase 0 创建并持续维护。

## 1. 输入、冲突与事实依据

### 1.1 需求和适用约束

- 用户要求覆盖 `Bing.Data.Sql`、`Bing.Data.Sql.Analyzers`、`Bing.Dapper.Core`、Dapper Provider、Unit/Integration Tests、Benchmarks、CI 和文档；项目尚未正式发布，允许合理 Breaking Change，且不得通过兼容转发恢复旧 API。
- 已读取仓库规则: `AGENTS.md`、`.github/copilot-instructions.md`、`.editorconfig`、`.github/prompts/create-plan.prompt.md`。
- 已读取架构和使用说明: `ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`docs/sqlquery-lambda-usage.md`、`docs/integration-testing.md`。
- 已读取前序 RC 任务资料: `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/{plan,execution,benchmark-report,review,final-summary}.md`。
- 用户列出的 `Bing.Data.Sql-全面审查报告-20260826.md` 与 `Bing.Data.Sql-Benchmark补充分析-20260826.md` 不在仓库中；不得以其缺失阻塞工作。本计划按用户明确问题、现有文档和当前源码建立，实施时必须以实时源码和运行结果复核。

### 1.2 指令冲突和裁决

1. 用户要求计划完成后立即实施并创建五个过程文件；当前 Agent 为 `plan-writer`，唯一可写文件为本 `plan.md`。因此本轮在计划完成后停止。执行器启动时先创建用户指定过程文件并立即进入 Phase 0，不可将空文件预创建为 Planner 产物。
2. 用户允许 Breaking Change；仓库治理仍使用 `PublicAPI.Shipped.txt`/`Unshipped.txt` 及 Public API Analyzer。实施应删除目标 API、同步仓库消费者、Public API 基线、编译契约、文档与迁移说明，禁止 `[Obsolete]` 或 wrapper 规避删除。
3. 已有活动设计文档仍将高层 `FromTable` 视为 Lambda 根入口，与本任务要求“删除高层 `ISqlQuery.FromTable` 和 `SqlLambdaQuery.FromTable`，字符串表统一迁移至 `Query().From(string)`”冲突。以本任务为准；Phase 2 必须同步修正文档和追溯映射。
4. 外部 Provider 真实执行是 RC 验收的一部分，但当前规划阶段无法判断凭据/Gate。SQLite 永远执行；缺少安全 Gate、专用测试库或凭据的外部 Provider 逐项标记 `blocked`，记录具体变量或环境原因，并继续其余任务。

## 2. 仓库认知与当前完成度

### 2.1 技术栈和工程边界

| 范围 | 当前证据 | 结论 |
| --- | --- | --- |
| 生产库 | `framework.props` 指定 `netstandard2.0` | `Bing.Data.Sql` 与 `Bing.Dapper.Core` 是 SDK 风格库。 |
| 测试 | `framework.tests.props` 指定 `net8.0;net6.0` | 使用 xUnit，Unit 与 Integration 按项目分层。 |
| API 门禁 | `Bing.Data.Sql.csproj`、`Bing.Dapper.Core.csproj` 引用 `Microsoft.CodeAnalysis.PublicApiAnalyzers` | Breaking Change 必须更新 API 文本基线，不能关闭 Analyzer。 |
| Analyzer | `Bing.Data.Sql.Analyzers` 以 analyzer 项目形式被 Data.Sql 引用 | 需维持 Analyzer Tests 和 public compile contract。 |
| Benchmark | `Bing.Data.Sql.Benchmarks` 为 `net8.0` Console，BenchmarkDotNet `0.14.0` | 现有 `FormalHost` Job 已用于 Root/Join 等基准，但参数矩阵和 before 证据不完整。 |
| CI | `appveyor.yml` 固定 `Visual Studio 2017`，直接 `dotnet build/test` | 与 net8/net6 测试目标和 RC SDK 治理不匹配，须先确认实际可用 CI 平台后现代化。 |
| Integration | SQLite、MySQL、PostgreSQL、SQL Server、Oracle、Doris 项目存在 | SQLite 采用临时文件、始终执行；外部 Provider 由 `RUN_*_INTEGRATION_TESTS` 与安全连接配置门控。 |

`.editorconfig` 规定 C# UTF-8 BOM、四空格、`RS0016/RS0017` 在 Data.Sql 与 Dapper.Core 为 error。所有文本读写必须显式 UTF-8；PowerShell 输出与落盘须显式 UTF-8。

### 2.2 当前已实现并应保留的能力

| 能力 | 现有证据 | 状态 |
| --- | --- | --- |
| 非泛型 Raw 主路径 | `ISqlQuery.Query/Sql/SqlInterpolated/Procedure` 与 `SqlQueryApiContractTest` | 已实现。 |
| 非泛型 Lambda 描述 | `ISqlQuery.From<TEntity>`、`SqlLambdaQuery` | 已实现，连续 `From<TEntity>()` 可组合。 |
| 结果类型终结 | `ToEntity<TResult>`、`ToList<TResult>`、分页、标量、同步/异步流 | 已实现；无 `.As<TResult>()` 公开入口。 |
| 连续组合 | `SqlQueryLifecycleTest` 和 SQLite 集成已有 1-10 Root、2-10 Join 追溯 | 已实现，必须保留并转为仅公开 API 覆盖。 |
| 多映射 | Fluent/Text 的 2-7 Dapper 映射终结方法及前序 SQLite 覆盖 | 已实现，需重新验证异常、取消、提前释放。 |
| SQL 实例缓存 | `SqlQuery` 以 `_shapeVersion/_cachedVersion/_cachedSql` 缓存，`Touch()` 失效 | 部分实现；扩展 Fluent 调用路径未统一触发变更通知。 |
| 多结果集回调清理 | `SqlMultipleQueryResult.Complete()` 已先 `Interlocked.Exchange(ref _complete, null)` 且清空 `_completeAsync`；`CompleteAsync()` 仅交换 `_completeAsync`，在 fallback 才交换 `_complete` | 同步路径已覆盖双回调清空；异步主路径仍不满足“开始时同时清空两者”的明确要求。 |
| Runtime SPI | `ISqlQueryPlanExecutor` 与 `ISqlQueryBuilderSource` 已拆分，运行时类型标记 `EditorBrowsable(Never)` | 已有收敛，但仍需真实跨程序集消费者矩阵与 public/internal 裁决。 |

### 2.3 已确认缺口、矛盾和风险

| 项目 | 当前源码/资料证据 | 结论 |
| --- | --- | --- |
| 扩展 Fluent 缓存失效 | `SqlQueryOperationAccessor` 仅返回 Builder/ClauseAccessor；`Extensions.IWhere/ISelect/IFrom` 等直接修改 clause，未触及 `SqlQuery.Touch()` | P0 正确性缺陷。`ToSql()` 后经扩展 `.Where()` 等修改可继续返回旧缓存 SQL。 |
| 变更原子性 | 扩展方法直接修改真实 Builder；无统一 mutate gateway | 必须将成功提交与单次 Touch 绑定；异常不得改变 Builder、参数、版本或缓存。 |
| 高层 `FromTable` | `ISqlQuery.FromTable`、`SqlLambdaQuery.FromTable`、PublicAPI.Unshipped、Benchmark Root 20/50 均仍存在 | P0 Breaking Change 未完成，且当前文档仍推荐该路径。 |
| 高层 `ClearSelect` | `SqlLambdaQuery.ClearSelect` 与 `ISqlBuilder.ClearSelect`/Public API 条目存在；`Select<TEntity>(bool)` 调用 Builder 的 append 型 `Select` | 必须统一为 `Select` 替换、`AppendSelect` 追加，迁移调用后删除高层 `ClearSelect`。底层 Builder `ClearSelect` 是否保留必须按 CRUD/Builder 独立职责裁决。 |
| Join API | 当前公开 Join 仍使用多个 string 参数（`rightAlias/leftAlias/schema`） | 本任务要求普通场景使用 `rightAlias`，高级场景使用 `SqlJoinOptions`，不得增加仅参数顺序不同的 overload。 |
| 多结果集异步 retained delegate | `CompleteAsync()` 未在开始时交换 `_complete`；且 internal 类构造函数仍标记 `public` | P0 生命周期收尾和可见性清理未完成。 |
| Benchmark 口径 | `SqlLambdaJoinBenchmarks.JoinCount` 实际表示总来源规模且 `BuildRepeatedEntityJoin/JoinFailure` 仍受此参数交叉组合；Root 所有方法受 `ParameterCount` 影响，20/50 Root 使用 `FromTable` | 无效矩阵，需拆分且迁移 raw 压力场景。 |
| FormalHost 发布证据 | 前序独立 review 明确 Root before `72/72`、Join before `36/36` CSV 均不存在；Round 3 provenance 无效；Round 4-10 不完整 | 发布性能验收为 `blocked`，不得宣称无回归/低 GC。 |
| 基准报告矛盾 | 前序 `benchmark-report.md` 同时称 Round 3 provenance 无效、又称可追溯性已解决 | 新任务需单独建立有效基线，明确旧结果不可用于 before/after。 |
| 文档追溯过期 | `sql-lambda-query-design.md`、`sql-metadata-test-traceability.md`、`sqlquery-lambda-usage.md` 仍出现 `FromTable` 或历史高元数描述 | 需要同步，否则 API 体验与实际合同冲突。 |
| CI 年代 | AppVeyor image 为 VS2017，未固定当前 SDK | P1 发布工程风险。 |

### 2.4 综合完成度和质量判断

- **总体完成度：约 50%-60%。** 类型化/Raw 主路径、连续 1-10 组合、终结方法、SQLite 基础执行和前序 Runtime SPI 初步收敛已存在；但本任务最核心的扩展 Fluent 缓存一致性、`FromTable`/`ClearSelect` API 收敛、Join Options、完整 Benchmark provenance、CI SDK 固定和端到端性能基线未完成。
- **正确性与安全：高风险未收口。** SQL/参数缓存若与 Builder 不同步会产生错误执行 SQL，必须优先于低分配优化；动态过滤、租户和软删除不得因缓存/Clone/Pool 共享而泄漏。
- **性能：尚无可发布结论。** 历史报告提示 10 来源构建和 1000 IN 参数分配较高，但 Root/Join FormalHost before 证据无效。只能将这些数值用作调查线索，不能作为优化收益基线。
- **复杂度与维护性：中高。** `SqlLambdaQuery`/`SqlLambdaQueryCore` 已有部分原子操作，但静态扩展越过实例变更边界；Runtime SPI 仍须按消费者而非 `EditorBrowsable` 收敛；大型追溯文档包含历史与现行合同混合内容。
- **API 与开发体验：部分合理。** 结果在终结方法指定、无高元数类型、Raw Query/Text/Procedure 分层清楚；高层 `FromTable`、`ClearSelect` 和多 string Join 参数仍使推荐路径不唯一或易误用。
- **测试：部分覆盖、但缺直接回归。** 已有连续组合与 SQLite 真实执行证据；缺扩展 mutation cache、Select 替换、FromTable 负向编译、delegate retained、外部 Provider 统一矩阵以及 Benchmark 可追溯性验证。

## 3. 目标架构和不变量

### 3.1 最终公开 API

保留：

```csharp
query.From<TEntity>(alias: "t", schema: "dbo");
query.FromSubquery<TProjection>(subquery);
query.Query().Select("Id").From("Orders");
query.Sql(sql, parameters);
query.SqlInterpolated($"Select * From Orders Where Id={id}");
query.Procedure(name, parameters);
query.ToEntity<TResult>();
query.ToList<TResult>();
query.AsEnumerable<TResult>();
query.AsAsyncEnumerable<TResult>();
```

删除且不留 wrapper：

- `ISqlQuery.FromTable(string, ...)`、`SqlLambdaQuery.FromTable(string, ...)`；字符串表仅走 `Query().From(string)`。
- 高层 `SqlLambdaQuery.ClearSelect()`；底层 Builder 的 CRUD/独立职责不受此删除自动影响。
- 任何 `.As<TResult>()`、高元数查询描述、仅为旧 API 保留的兼容转发。

Join 收敛为：

```csharp
Join<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate, string rightAlias = null)
Join<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate, SqlJoinOptions options)
```

`SqlJoinOptions` 仅含 `RightAlias`、`LeftAlias`、`Schema`，须不可变或仅初始化期可写，输入验证与 alias 解析应在真实 Builder 提交前完成。

### 3.2 Mutation gateway 合同

1. 只要 Fluent 扩展会改变 SQL 结构、参数、过滤、来源、投影、排序、分页、Union/CTE/Subquery 或动态条件，就必须经过一个 internal gateway，例如 `SqlQueryOperationAccessor.Mutate(source, action)`，而不是扩展各自 `Touch()`。
2. 对 `SqlQuery` 描述，gateway 必须取得实例/Builder、在候选状态完成可失败操作、成功后只调用一次 `Touch()`；no-op 条件分支不得触发 `Touch()`。
3. 对普通 `ISqlBuilder`，gateway 保留现有 Builder 行为但不伪造 Query version；不能改变 Builder 的公开扩展兼容语义。
4. 异常时原 SQL、参数、alias、版本和缓存保持调用前状态。若底层操作不具备原子能力，gateway 必须采用 Clone/candidate 预检或由各 clause 提供一次性提交 API；不得通过先 Touch 后回滚缓存掩盖状态泄漏。
5. `ToSql()` 和真实执行准备的 SQL 必须来自同一修正后快照；WhereIf(false) 绝不改 SQL、参数、版本或缓存。

### 3.3 生命周期、隔离与性能不变量

- `CompleteAsync()` 一开始原子取走 `_complete` 与 `_completeAsync`，仅选择应执行的一项，保证无论 callback 成功/失败都不保留另一委托；reader、completion、lease 均恰好一次释放。
- 不引入 production `InternalsVisibleTo`；跨生产程序集必要契约保留 minimal `public` + `EditorBrowsable(Never)`，其余 internal/private。
- 不池化可变 Query/Builder/Clause；任何 copy-on-write/池化先有线程、租户、过滤和异常路径直接测试。
- 性能优化仅在同一机器、同一 Job、同一源码身份可比较 before/after 中成立；收益低于约 5% 或复杂度明显上升时撤回并记录。
- 修改 SQL、缓存、映射、Provider 分支或热路径时，完整 SQL/参数断言与“生产符号 -> 测试方法”追溯必须同步更新。

## 4. 过程产物和状态协议

执行器在 Phase 0 创建：

- `progress.md`：各 Phase 状态、修改文件、命令、结果、阻断、下一步。
- `decisions.md`：Breaking API、SPI visibility、Join Options、优化保留/撤回裁决。
- `benchmark-baseline.md`：before/after 源码身份、环境、命令、artifact hash、有效性。
- `verification-report.md`：最终功能/API/测试/Benchmark/blocked 汇总。

以上文件和本计划中的状态仅使用 `pending`、`in_progress`、`completed`、`blocked`。同一时刻仅一个主 Phase 为 `in_progress`。每轮变更后更新 `progress.md`；最终不得把 `blocked` 写成 pass 或 skip 代替。

## 5. 文件范围

### 5.1 已确认修改候选

生产：

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/SelectClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.I{From,Join,Select,Where,GroupBy,OrderBy}.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/{SelectClauseExtensions,WhereClauseExtensions}.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryResult.cs`
- `framework/src/Bing.Data.Sql/{PublicAPI.Shipped.txt,PublicAPI.Unshipped.txt}`
- 必要时 `framework/src/Bing.Dapper.Core/{PublicAPI.Shipped.txt,PublicAPI.Unshipped.txt}`、Runtime 抽象/Plan/Bridge/Factory 文件及真实 Provider 消费者。

测试与基准：

- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- 新增职责明确的 `SqlFluentMutationCacheTest.cs`、`SqlLambdaSelectSemanticsTest.cs`、`SqlJoinOptionsTest.cs`（命名以实施时仓库现有模式为准）。
- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryExecutorTest.cs` 及现有生命周期测试位置。
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/{SqliteExecutionIntegrationTest,SqliteMultipleQueryIntegrationTest}.cs`。
- `framework/tests/Bing.Data.Sql.Benchmarks/{SqlLambdaRootBenchmarks,SqlLambdaJoinBenchmarks}.cs`，以及新增 IN/过滤/Diagnostics/SQLite E2E 基准类。
- Provider Tests/Integration 项目与 `Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`。

配置与文档：

- `appveyor.yml`，以及仅当仓库实际 CI 架构证明需要的现有 CI/SDK 配置文件；不存在 `global.json`，是否新增需基于发布 SDK 决策。
- `docs/sqlquery-lambda-usage.md`、`docs/sqlquery-usage.md`、`docs/integration-testing.md`、`docs/ReleaseNotes.md`。
- `ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`。

### 5.2 条件候选文件

仅在真实 consumers 或实现拆分证明需要时修改：`Bing.FreeSQL`、`Bing.EntityFrameworkCore`、各 Dapper Provider 的 Runtime 绑定/Factory；共享测试基础设施；CI 之外的发布脚本。不得为完成目录拆分进行无关格式化或全仓改名。

## 6. 用例矩阵与 Mock 边界

| 范围 | Given | When | Then | 类型 |
| --- | --- | --- | --- | --- |
| Fluent cache | `Query().Select().From()` 已调用 `ToSql()` | 通过扩展 `Where("Status", 1)` 修改 | 新 `ToSql()` 与执行捕获 SQL 完整相等，均有 Where 和同一参数 | Unit + SQLite |
| no-op | 已缓存 SQL | `WhereIf(false, ...)` | SQL、参数、版本、缓存命中不变 | Unit |
| 原子失败 | 已缓存 SQL/参数 | 扩展 Join/Select/Where/Union 后续校验抛出 | Builder、参数、alias、版本和缓存全不变，可重试 | Unit |
| mutation family | Query/Builder | Select/AppendSelect/From/Join/Where/Group/Having/Order/Union/CTE/Paging/动态过滤 | 成功只失效一次，最终 SQL/参数完整 | Unit |
| Select | 已有投影 | 连续 `Select`、`AppendSelect`、`Select<TEntity>(bool)` | Select 替换；Append 追加；失败保持旧投影 | Unit + SQLite |
| API 删除 | Consumer source/reflection | 使用 `FromTable`、高层 `ClearSelect`、`.As<TResult>()` | 编译失败/反射不存在；替换入口能编译 | Analyzer compile contract |
| Join options | 普通/显式 left alias/schema/同实体 alias | 调用 string 或 `SqlJoinOptions` 入口 | SQL、别名、参数正确；非法 options 不污染 | Unit + SQLite |
| multiple result | 双 callback 捕获对象 | `DisposeAsync`、读取失败、交叉 sync/async Dispose | 两 callback 引用立刻清空，选定 callback 一次、lease 一次、无 retained delegate | Dapper Unit |
| cleanup aggregation | reader/callback/lease 均可控失败 | read/Dispose/DisposeAsync | 主异常与 cleanup 聚合顺序稳定，不吞异常 | Dapper Unit |
| isolation | 租户、软删除、同实体 alias、并发描述 | render/execute/clone/失败重试 | 无 SQL/参数/过滤串扰 | Unit + SQLite |
| values | null/empty/invalid/Nullable/enum/Guid/DateTime/byte[] 与 IN 0/1/10/100/500/1000/2100 | bind/render/execute | SQL、参数顺序和值、Provider 上限和异常稳定 | Unit + Provider/SQLite |

仅 Mock 时间、连接、reader、transaction、lease、外部 Provider 与日志等系统边界。不得 Mock 被测 Builder/Query 的内部调用来替代状态和 SQL 结果断言。SQL 测试断言完整文本而非 `Contains`。

## 7. 分阶段执行计划

### Phase 0 - 可追溯基线与消费者矩阵

**状态: `pending`**

#### RC26-P0-01 [P0] 创建过程产物并记录工作树/环境

- 目标: 在任何代码变更前建立可重放的源码、环境、工具链和未提交变更基线。
- 依赖: 无。
- 修改范围: 仅本任务过程文件。
- 步骤:
  1. 创建第 4 节的四个过程文件，初始化状态；将 Phase 0 标为 `in_progress`。
  2. 在 UTF-8 PowerShell 环境记录 `git rev-parse HEAD`、`git branch --show-current`、`git status --short --untracked-files=all`、`git diff --stat`、`git diff --check` 和工作树 diff SHA-256。不得把 dirty 工作树伪装为 commit。
  3. 记录 `dotnet --info`、`dotnet --list-sdks`、Windows 版本、CPU、内存、电源模式、Runtime、GC 模式、TFM、Nullable、Analyzer、runsettings 和全部不含机密值的 Gate 状态。
  4. 对 Benchmark 源、csproj、Config 和运行命令计算 SHA-256；确认 artifacts 位于项目默认 Compile glob 排除路径。若现有目录会被 `**/*.cs` 包含，先调整到明确 `Compile Remove` 的位置或既有 artifacts 参数目录，并作 build 验证。
  5. 运行能执行的核心 Build/Unit/Analyzer/Dapper/SQLite 命令并把失败分类为既有、本任务相关、环境阻断；不因一项失败停止。
- 验收: before 身份、diff、环境、命令、日志/artifact 路径与 hash 可追溯；没有 Benchmark 产物导致 AssemblyAttribute/TargetFrameworkAttribute 重复编译。

#### RC26-P0-02 [P0] 公开 API、SPI 和消费者矩阵

- 目标: 用真实 consumers 决定 API 删除、Join 迁移、Runtime visibility 和目录拆分，不凭旧计划或 `EditorBrowsable` 断言。
- 依赖: RC26-P0-01。
- 步骤:
  1. 搜索生产、测试、文档、样例和 Public API 中的 `FromTable`、`ClearSelect`、`.As<`、Join 多 string 参数、Runtime SPI、IVT、`SetRoots`、高元数类型及所有扩展 mutation 族。
  2. 对 Data.Sql、Dapper.Core、EFCore、FreeSql、Provider、Tests、Benchmarks 构建“符号 -> 消费程序集 -> 所需成员 -> 替代路径”矩阵。
  3. 检查当前未提交 SQL 范围是否重叠。若重叠，将文件、hunk、预期处理写入 `decisions.md`，只做最小合并，不回滚用户内容。
  4. 更新追溯映射，明确生产符号到计划中将新增的测试方法。
- 验收: 每个删除/收窄符号有仓库消费者、迁移方案和直接测试位置；无新增生产 IVT 的需求。

#### RC26-P0-03 [P0] before Benchmark 基线有效性裁决

- 目标: 把前序无效/不完整 FormalHost 明确排除，并设计可自然完成的独立 before/after 运行。
- 依赖: RC26-P0-01。
- 步骤:
  1. 将前序 Round 3 及 Round 4-10 的不完整 Root/Join 结果记录为“历史参考，不可作本任务 before”。
  2. 基于当前 HEAD 和 dirty diff 创建 before 身份：若工作树含本任务重叠改动，使用独立 worktree 或 worktree patch 且记录 SHA；不 reset 当前工作树。
  3. 将 Root、Join 和后续新基准拆成可独立完成的进程/产物目录，制定先 smoke、后完整 FormalHost 的调度，保存 stdout/stderr、BDN log、CSV、Markdown、HTML、命令和 hash。
  4. 定义逐 case 键 `Method + 全 Params + Job + Runtime`、Mean/Median/Error/StdDev/Allocated/Gen0/Gen1/Gen2/P95 计算方式，P95 必须来自原始样本或明确独立采样，绝不将 BDN Error 当 P95。
- 验收: `benchmark-baseline.md` 清晰区分有效、无效和 blocked；不会使用 Dry、不同 Job、partial log 或共享二进制作 formal before。

### Phase 1 - P0 Fluent cache 与多结果集正确性

**状态: `pending`**

#### RC26-P1-01 [P0] 实现统一 Fluent mutation gateway

- 目标: 修复扩展 Fluent 绕过 `Touch()` 的缓存污染，并统一成功/no-op/失败语义。
- 依赖: Phase 0 通过。
- 修改范围: `SqlQuery`、`SqlQueryOperationAccessor`、所有修改型 Extensions、必要 clause 原子 API 与直接测试。
- 步骤:
  1. 审计 extension family：Select/AppendSelect、From/Join、Where/WhereIf、GroupBy/Having、OrderBy、Union/CTE/Subquery、Distinct/Paging、参数和动态过滤；区分只读/helper/no-op 与真正 mutation。
  2. 将 `SqlQueryOperationAccessor` 扩展为唯一 internal mutation gateway，能识别独立查询实例与普通 Builder；禁止各扩展自行触碰 cache/version。
  3. 对操作可失败的 family 先使用既有 Clone/候选 clause/参数预检完成，再提交到真实 Builder，成功后 `Touch()` 一次。对 no-op 如 blank、null、条件 false 不触发 gateway commit。
  4. 保持生命周期 Draft 检查，确保缓存失效只影响当前 query，Clone、租户和动态过滤隔离不变。
  5. 添加 Unit regression：题设 `Query().Select("Id").From("Orders") -> ToSql -> Where("Status", 1) -> ToSql`，断言完整 SQL、参数和执行 snapshot SQL 相同；对每个代表族测试成功、false/no-op、失败原子性和重复 ToSql 命中。
  6. 添加 SQLite Integration：捕获实际执行命令或由真实结果差异证明执行 SQL 与第二次 ToSql 一致；含干扰数据、租户/软删除至少一项并发隔离用例。
- 风险: 直接 Clone 可能改变参数名/别名或增加热路径分配。先运行 correctness，再在 Phase 5 量化；不因优化跳过原子性。
- 验收: 所有已审计 mutator 经同一入口；修改后 SQL/执行一致；失败与 false 不改状态；成功每操作一次失效；无 query/tenant/filter 污染。

#### RC26-P1-02 [P0] 完成多结果集回调生命周期

- 目标: `CompleteAsync()` 开始即同时断开 `_complete` 与 `_completeAsync`，清理 retained delegate 和无意义 public internal constructor。
- 依赖: RC26-P0-02。
- 步骤:
  1. 将构造函数访问级别收窄为 `internal`，检查仅测试/同程序集实例化路径并调整测试访问，不新增生产 IVT。
  2. 以 `Interlocked.Exchange` 在 `CompleteAsync()` 开始同时取得/清空两 callback，按现有 sync/async 创建合同选择一个执行，避免双执行、遗漏或 callback 失败后残留。
  3. 保持 reader、transaction、completion、lease 的一次性语义；同步/异步交叉 Dispose、取消、读失败、重复 Dispose、并发拒绝、主异常/清理异常聚合不得退化。
  4. 添加直接 internal 生命周期测试或 WeakReference 测试，验证结果对象不再强持有回调捕获对象；使用可控 reader/callback/lease 注入同时验证异常顺序。
  5. SQLite 多结果集真实回归覆盖正常读完、提前释放、读失败、取消、sync/async 交叉释放和后续查询可用。
- 验收: 异步路径开始后两委托字段立即不再由结果对象持有；回调/lease 不重复；组合异常、资源和取消语义有直接 Unit 与 SQLite 证据。

#### RC26-P1-03 [P0] Phase 1 验证与复审

- 目标: 以 targeted -> 全量核心 -> SQLite 顺序证明正确性。
- 依赖: RC26-P1-01、RC26-P1-02。
- 验证命令: 先从实际 `.sln`/csproj 和现有 runsettings 确认，再记录实际执行命令。最低包含 Data.Sql Unit、Dapper Core Unit、SQLite Unit/Integration、Analyzer Tests；对触及项目指定 `-f net8.0` 与 `-f net6.0` 分别运行。
- 验收: 无未修复 P0；SQL 全字符串与参数值断言通过；`progress.md` 记录 passed/failed/skipped、已知 warning 与阻断。

### Phase 2 - Breaking API 收敛和 Runtime SPI

**状态: `pending`**

#### RC26-P2-01 [P0] 删除高层 FromTable 并迁移 raw 压力路径

- 目标: Lambda 类型化链只保留 `From<TEntity>`/`FromSubquery`，原始字符串表统一走 Raw Fluent。
- 依赖: Phase 1 完成、消费者矩阵。
- 步骤:
  1. 迁移生产、测试、Benchmark、示例和 docs 的高层 `FromTable`；20/50 表压力场景迁至 Raw Fluent 专项而非保留 Lambda API。
  2. 删除 `ISqlQuery.FromTable`、`SqlLambdaQuery.FromTable` 及专属支撑逻辑，只在无其它职责时删除 `SqlTableReference` 创建路径。
  3. 同步 PublicAPI、XML、Roslyn/reflection negative compile contract，验证 `Query().From(string)` 的正向 consumer 编译。
- Breaking Change: 无兼容层；迁移 `sqlQuery.FromTable("Orders", "o")` 为 `sqlQuery.Query().From("Orders", "o")`。
- 验收: 当前 production/current docs/current tests 不再有高层 FromTable；Raw Fluent 表来源完整 SQL/参数与 SQLite 执行通过。

#### RC26-P2-02 [P0] Select 替换/AppendSelect 追加和高层 ClearSelect 删除

- 目标: 全部 `Select` 原子替换投影，只有 `AppendSelect` 追加。
- 依赖: RC26-P1-01。
- 步骤:
  1. 审计 `SqlBuilderBase`、`SelectClause`、`SqlLambdaQueryCore` 与所有 `Select<TEntity>(bool)` consumer，识别 append 语义和依赖 `ClearSelect().Select(...)` 的调用。
  2. 让每种 Select 在候选投影上完成表达式/alias/parameter 解析并替换，失败不改旧 projection；AppendSelect 保留追加。
  3. 迁移所有 `ClearSelect().Select<TEntity>(true)`，删除高层 `SqlLambdaQuery.ClearSelect`；仅在底层 Builder/CRUD 独立功能需要时保留底层 `ISqlBuilder.ClearSelect`，在 decisions 中写明职责边界。
  4. 添加连续 Select、Append、同实体多 alias、空/异常 projection 原子性、完整 SQL 与 SQLite 物化测试。
- Breaking Change: 删除高层 `ClearSelect()`，迁移为直接 `Select(...)`。
- 验收: 没有依赖 ClearSelect 的高层消费；Select/Append 语义可由直接测试区分，完整 SQL/参数不回归。

#### RC26-P2-03 [P1] Join Options 收敛

- 目标: 普通调用不再暴露多 string 参数，高级 alias/schema 使用 `SqlJoinOptions`。
- 依赖: RC26-P0-02、RC26-P2-01。
- 步骤:
  1. 新增单职责 `SqlJoinOptions`，只包含 `RightAlias`、`LeftAlias`、`Schema`；决定 null/default/输入冲突语义，必要时 copy input 防止调用后可变污染。
  2. 替换 Join/Left/Right/Full 的多 string 签名为题设两个入口，统一进入一个 internal core；不保留旧 overload 和包装。
  3. 更新二元 Join、派生表 Join、同实体 alias、schema、非法 alias 的 direct Unit、SQLite、Roslyn contract、PublicAPI、XML/示例。
- Breaking Change: 原位置参数 `leftAlias/schema` 调用改为 `new SqlJoinOptions { ... }` 或命名属性初始化。
- 验收: 无仅调换参数顺序的 overload；普通路径只有 predicate + rightAlias；高级路径完整 SQL、异常原子性和 alias 绑定正确。

#### RC26-P2-04 [P1] Runtime SPI 最小公开面

- 目标: 按真实跨程序集需求收敛 `ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、Runtime Binding/Bridge/Plan/Factory。
- 依赖: RC26-P0-02、Phase 2 API 冻结。
- 步骤:
  1. 用消费者矩阵逐成员裁决 public/internal/private，移动确需 public 的合同到清晰 Runtime namespace 并标注 `EditorBrowsable(Never)`。
  2. 不跨程序集的 builder/resource/mutable 状态降为 internal/private；跨程序集用 immutable plan/snapshot 或窄执行方法替代资源泄露。
  3. 构建 Dapper Core、EFCore、FreeSql、Provider 真实 consumers，增加 reflection/compile contract 验证无新增生产 `InternalsVisibleTo`。
  4. 更新 Public API 文本和迁移说明；不恢复高元数类型或 `.As<TResult>()`。
- 验收: public SPI 仅含被官方生产消费者使用的最小成员；无 production IVT；Plan 不公开 Builder/connection/transaction/diagnostic scope。

### Phase 3 - 测试与 CI 加固

**状态: `pending`**

#### RC26-P3-01 [P0] 公开 API 1-10 Root/Join 与 P0/P1 测试矩阵

- 目标: 保留并整理只用公开 API 的可读测试，锁定端到端行为。
- 依赖: Phase 2 完成。
- 步骤:
  1. 将 1-10 Root、2-10 连续 Join 组织为独立可读测试类，名称/注释用“连续组合”，不得使用 internal batch API、反射或已删除 API。
  2. 每个规模断言完整 SQL、完整参数名、参数值、顺序和物化；SQLite 创建表、插入匹配/干扰行，验证无笛卡尔积和正确绑定。
  3. 实现第 6 节所有缺口，重点流式提前结束/Dispose、CancellationToken、动态租户/软删除并发、同实体 alias、value matrix、IN 边界/Provider 上限。
  4. 对真正承担状态/异常逻辑的 internal 类型做 direct test；纯转发 wrapper 不机械补测。
- 验收: 1-10 Root、2-10 Join 全部通过公开调用构建和真实 SQLite 执行；关键 SQL 全断言，不通过 Skip/弱断言伪绿。

#### RC26-P3-02 [P1] Provider 集成矩阵

- 目标: SQLite 始终全量执行，外部 Provider 按安全 Gate 获取真实 1/2/5/10 证据。
- 依赖: RC26-P3-01。
- 步骤:
  1. SQLite：1-10 Root、2-10 Join、分页/参数/流式/多结果集全跑并进入普通 CI。
  2. MySQL/PostgreSQL/SQL Server：至少 1/2/5/10 表；Oracle 覆盖分页、参数前缀、标识符、Returning/Procedure output；Doris 仅只读并验证稳定 NotSupported。
  3. 若未配置 Gate/安全测试库，记录每 Provider 缺失的变量、专用库/重置授权状态为 `blocked`，仍运行离线 Provider SQL contract，绝不把 skipped 计作 pass。
- 验收: SQLite 通过；每外部 Provider 有真实 pass 或具体 blocked 原因和可执行启用路径。

#### RC26-P3-03 [P1] 现代化 CI 和制品保存

- 目标: 使 CI 实际支持目标 SDK 并保存 RC 可审计制品。
- 依赖: RC26-P0-01、RC26-P3-01。
- 步骤:
  1. 识别实际 CI 平台和 release SDK；不默认新增 GitHub Actions。替换/移除不能支持当前 TFM 的 VS2017/.NET Core 2.2 设定。
  2. 以 `global.json` 或现有 CI 显式 SDK 固定机制固定真实发布 SDK；不得猜测版本。
  3. 常规 CI 必跑 Build、Data.Sql Unit、Analyzer、Dapper Core、SQLite Integration、Public API contract；外部 Provider 放受保护手动/定时 job，并输出 pass/fail/skip/blocked 数量。
  4. 保存 TRX、覆盖率、Benchmark summary、Breaking Change/API artifact，确保无机密连接串。
- 验收: 修改后的 CI 配置在目标 SDK 可执行；外部 Gate 不影响 SQLite 和 Unit 的绿色主链。

### Phase 4 - Benchmark 体系修复与有效基线

**状态: `pending`**

#### RC26-P4-01 [P0] Benchmark provenance 与矩阵重构

- 目标: 消除无效参数组合，保证每次结果可追溯。
- 依赖: Phase 0 完成；API 删除后更新 benchmark 调用路径。
- 步骤:
  1. 每次 artifact 记录 Task ID、HEAD、branch、dirty/diff hash、benchmark source hash、OS/CPU/电源、SDK/runtime/JIT/GC、Job、开始/结束、Markdown/CSV/raw log hash。
  2. 将 `JoinCount` 改为 `SourceCount` 或改为真实 Join 数 `0/1/4/9`；将 `BuildRepeatedEntityJoin`、`JoinFailure` 移出该参数化类。
  3. Root 的 `ParameterCount` 仅保留 IN 专项；其它 Root 场景移除 10/100/1000 重复。20/50 表改为 Raw Fluent 专项。
  4. WhereIf 拆为端到端、已构造 query 增量、缓存后 false/true mutation correctness+performance；动态过滤拆 soft-delete、tenant、组合和缓存命中。
  5. 使用 BenchmarkDotNet 原生 Gen0/1/2，移除重复自定义 Gen2；如要 P95，使用原始样本/独立采样输出。
- 验收: 每一 Params 维度影响被测行为；无已删除 API；报告可证明来源和统计列含义。

#### RC26-P4-02 [P0] IN 和 SQLite/Dapper E2E 基准

- 目标: 分清值创建、绑定/渲染、完整构建、Provider 分窗和真实数据库执行。
- 依赖: RC26-P4-01。
- 步骤:
  1. 新建或拆分基准：预构造 values 绑定/渲染；values 创建+boxing harness；Build+IN+Render；Provider 分窗/上限，覆盖 0/1/10/100/500/1000/2100 合法边界。
  2. 使用可重复微型 SQLite 数据库建立 E2E：Query/ToEntity/ToList、buffered/streaming、2/5/7 映射、多结果集、Diagnostics Off/Activity/Listener/Trace、cancel/exception/Dispose。
  3. 将纯 SQL 构建与 DB E2E 分报告，避免把连接/IO 解释为 Builder 性能。
- 验收: 每个 E2E case 可重复、无公网/随机 sleep；输出与纯构建结果分开。

#### RC26-P4-03 [P0] 完整 FormalHost before/after

- 目标: 形成有效 Root/Join/IN/过滤/诊断代表子集的完整基线。
- 依赖: RC26-P4-01、RC26-P4-02；不得在 Phase 5 优化前跳过。
- 步骤:
  1. 以 Phase 0 定义的隔离 before 身份运行 smoke 后完整 FormalHost；before/after 各自在自己的 worktree/build/output/artifact 中运行，禁止共享二进制或 historical partial artifact。
  2. Root/Join 必须自然完成全部设计 case，无 NA、process -1、构建失败或中途终止；对多峰/离群进行重复采样、隔离重跑或有证据接受。
  3. 输出逐 case delta，Mean/Allocated 超过 10% 且误差区间不重叠为回归候选；修复、复跑或明确 RC 拒绝，不能静默通过。
  4. Windows Workstation 保留，补 Server GC 代表子集和 Linux x64 CI 代表基线，SDK 与发布一致。
- 验收: before/after key 全匹配、环境与 hash 完整、报告无矛盾；本任务前序 invalid 数据不参与发布结论。

### Phase 5 - 数据驱动性能优化

**状态: `pending`**

#### RC26-P5-01 [P1] IN 参数热路径优化

- 目标: 在相同 benchmark harness 下减少 100/1000 IN 参数 Allocated，不破坏 SQL/顺序/隔离。
- 依赖: Phase 4 有效 before。
- 步骤:
  1. 用 profiler/benchmark 量化参数名字符串、descriptor/SqlParam、Dictionary/List/array copy、boxing、token render、Provider 分窗快照。
  2. 仅对占主导的已证明分配采取局部优化，优先复用已有不可变快照或减少无效复制；不池化可变 Builder/Clause。
  3. 每次优化先补完整 SQL/参数顺序/上限/并发隔离测试，再同机器同 Job after 对比。
- 验收: 100/1000 IN 的稳定 Allocated 下降；Mean/Allocated 无解释退化超过 10% 则撤回或修复并复跑。

#### RC26-P5-02 [P2] 来源解析/Join 与诊断快照

- 目标: 基于数据降低 `ResolveSources/ResolveSource` 扫描和未启用诊断的无效快照。
- 依赖: RC26-P5-01 或独立测量证明热点。
- 步骤:
  1. 评估只读 Type/Alias index，确保同实体多 alias 正确且 clone 独立；不恢复高元数类型。
  2. Activity-only 不创建完整连接/事务/参数深快照；DiagnosticListener/Trace 启用才构造重对象；复用已有 ExecutionId，避免多余 Guid 字符串。
  3. 维持 QueryContextId、ExecutionId、父子 Phase 和异常诊断语义，用 Unit+SQLite 验证。
- 验收: 只有可重复至少约 5% 收益且复杂度合理的优化保留；否则在 `decisions.md` 记录撤回。

#### RC26-P5-03 [P1] 性能复审与准入

- 目标: 将 correctness、资源隔离与性能结果合并为 RC 判定。
- 依赖: RC26-P5-01、RC26-P5-02。
- 验收: 不声称 0 GC；报告明确缓存命中、普通构建、hot path、压力、E2E 的不同含义；任何未解释 >10% 回归维持 `blocked`。

### Phase 6 - 目录、注释、文档和发布准备

**状态: `pending`**

#### RC26-P6-01 [P1] 有证据的职责拆分与 XML 文档

- 目标: 按 Query Facade、Runtime SPI、Builder Core、Clauses、Diagnostics、Provider、Tests 拆分真实大杂烩。
- 依赖: API 和性能冻结。
- 步骤:
  1. 以文件行数、类型职责、引用方向为证据选择候选；主要 public 类型原则一个文件，紧密小类型可合并；优先 partial，不建立多层策略抽象。
  2. namespace 与目录对齐，移动后不改变 API；每次拆分运行 public API、目标 Unit 和 SQLite。
  3. public API 补 `summary/typeparam/param/returns/关键 exception`；实现/override 用 `inheritdoc`；internal 仅注释不变量和原因；删除过期“八元 API”叙述。
- 验收: 无循环依赖、无行为夹带重构、无同义重复 API，XML 文档与最终签名一致。

#### RC26-P6-02 [P1] 用户文档、示例和发布材料

- 目标: 文档只展示最终推荐路径，并能作为 compile contract。
- 依赖: RC26-P2-01、RC26-P2-02、RC26-P2-03。
- 步骤:
  1. 文档提供 typed Lambda、raw Fluent、raw text/interpolated、procedure、1/2/5/10 连续组合、Select/AppendSelect、动态过滤、流式释放示例。
  2. 用编译测试或现有样例项目验证示例；不得保留 `FromTable`/ClearSelect/`.As<TResult>()`/高元数 API。
  3. 在 ReleaseNotes 与迁移材料列出 FromTable/ClearSelect 删除、Join Options、Runtime namespace/SPI 调整、`.As<TResult>()` 不恢复、性能结论边界。
  4. 更新 `sql-metadata-test-traceability.md` 为最终“生产符号 -> 测试方法”映射。
- 验收: 文档、XML、示例、PublicAPI 与 API 编译合同一致，无误导性性能/0 GC 声明。

#### RC26-P6-03 [P0] 最终验证、报告和停止

- 目标: 汇总所有可执行验收，不掩盖 blocked 项。
- 依赖: 全部前序 Phase。
- 步骤:
  1. 运行实际配置的 restore/build、Data.Sql Unit、Analyzer、Dapper Core、SQLite Unit/Integration、Provider Unit、已开启外部 Integration、Public API Contract 和 benchmark formal subset/full matrix。
  2. 执行 `git diff --check`，与 Phase 0 status/diff 对比确认没有无关修改；绝不通过 reset/checkout 清理。
  3. 写 `verification-report.md`：功能/API、test 命令与 pass/fail/skip、Benchmark before/after、artifact hash、blocked、风险和 Breaking Change。更新所有 Phase 状态并只将满足全量标准者置 `completed`。
  4. 输出任务最终摘要，明确没有自动提交。
- 验收: 仅当第 8 节所有适用项满足时总体 `completed`；外部环境或 Benchmark evidence 未达标时总体 `blocked`/`partial`，并给出精确继续路径。

## 8. 最终验收清单

### 正确性

- [ ] Fluent 扩展修改后的 `ToSql()` 与真实执行 SQL 一致。
- [ ] WhereIf false 不改变缓存/状态；true 正确失效；失败 mutation 原子无污染。
- [ ] 动态过滤、租户、软删除、Clone、并发无污染。
- [ ] 多结果集 callback 和资源释放无 retained delegate，异常/取消/交叉 Dispose 合同通过。

### API

- [ ] 类型化和 Raw Fluent 分层清晰；`FromTable`、高层 `ClearSelect`、无意义 wrapper 已删除。
- [ ] Select/AppendSelect 语义统一；Join Options 唯一高级入口。
- [ ] `.As<TResult>()`、高元数查询类型未回归；无新增 production IVT。
- [ ] Runtime SPI、Public API 和消费者构建一致。

### 测试与 CI

- [ ] 1-10 Root、2-10 Join 使用公开 API，完整 SQL/参数/物化断言通过。
- [ ] Data.Sql Unit、Analyzer、Dapper Core、SQLite Integration 全部通过，不依赖 Skip/弱断言。
- [ ] 外部 Provider 每项真实通过或以具体安全环境缺失标为 `blocked`。
- [ ] 现代 CI 使用真实目标 SDK，保存 TRX/coverage/API/Benchmark 制品。

### Benchmark

- [ ] Root/Join FormalHost 在当前源码身份完整完成，无 NA/build failure/process -1/中止。
- [ ] before/after 同机、同 Job、同 Params、独立构建，artifact 与 hash 可追溯。
- [ ] 无效参数矩阵已修复；IN、过滤、诊断、SQLite/Dapper E2E 有代表性结果。
- [ ] 性能结论区分缓存、普通构建、hot path、压力和 E2E，未声称 0 GC。

### 工程化

- [ ] 目录/命名空间、XML、文档、示例和 traceability 一致。
- [ ] `git diff` 无无关修改，且全过程无 commit/push。

## 9. 推荐执行命令的获取规则

本规划阶段无终端权限，未执行命令，不得虚构通过数。执行器必须先从 `Bing.All.sln`、各 csproj、现有 runsettings 与 CI 实际配置确认下列命令的精确项目路径、TFM 和 filter，然后在 `progress.md`/`verification-report.md` 记录原样命令与输出：

```powershell
# 基线和最终验证时按实际项目/TFM 拆分执行；不得用此处示例替代仓库核验。
dotnet restore .\Bing.All.sln
dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test <Bing.Data.Sql.Tests.csproj> -c Release -f net8.0 --no-restore
dotnet test <Bing.Data.Sql.Analyzers.Tests.csproj> -c Release -f net8.0 --no-restore
dotnet test <Bing.Dapper.Core.Tests.csproj> -c Release -f net8.0 --no-restore
dotnet test <Bing.Dapper.Sqlite.Tests.Integration.csproj> -c Release -f net8.0 --no-restore
```

外部 Provider 必须只在 `RUN_INTEGRATION_TESTS=true` 或对应 `RUN_<PROVIDER>_INTEGRATION_TESTS=true`、安全连接字符串及需要时 `ALLOW_DATABASE_RESET_FOR_TESTS=true` 条件满足时执行。日志不得回显机密。

## 10. 依赖顺序

`Phase 0 -> Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6`。

- Phase 4 的有效 before 是 Phase 5 的硬前置；没有有效 before 不得声称优化收益。
- Phase 2 API 冻结是测试重组、docs 和大文件拆分前置。
- Phase 3 SQLite 通过是 Provider/CI/发布验证前置；外部 Provider blocked 不阻塞 Unit、SQLite、Benchmark 和文档。
- 最终状态取最差关键项：P0 correctness、API negative contract、SQLite、FormalHost provenance 任一未满足时不得标记 `completed`。
