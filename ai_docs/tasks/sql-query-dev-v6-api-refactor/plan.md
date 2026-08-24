# Bing.Data.Sql 查询体系 dev_v6 API 重构计划

Status: APPROVED_FOR_EXECUTION

## 1. 任务与边界

- Task ID：`sql-query-dev-v6-api-refactor`
- 计划日期：2026-08-21
- 输出：`ai_docs/tasks/sql-query-dev-v6-api-refactor/plan.md`
- 目标：以单一非泛型 `SqlLambdaQuery` 和方法级泛型 Fluent API 恢复 dev_v6 风格调用体验；保留 V4 的独立查询描述、原子 Join、跨库/租户边界、参数快照与运行时资源隔离。
- 非目标：不恢复共享根 Builder、执行后自动 Clear、后置 Lambda `.On()`、高元数公开类型/表达式；第一阶段不增加跨实例或全局 SQL 缓存，不实现 0GC，也不默认引入 Roslyn Source Generator。
- Breaking Change：仓库明确允许未发布 API 破坏性收敛；不新增转发层或默认 `[Obsolete]`，除非 P0 引用矩阵发现必须迁移的已发布外部消费者并获确认。
- 本次 Planner 未执行 Build/Test/Benchmark，也无法通过当前可用只读接口取得实时 `git status`；V4 `execution.md` 记录的工作区为 35 个已跟踪文件差异、全解 `101 warnings/0 errors`，仅作历史线索。执行器必须重新采集真实 Git、命令和环境基线，绝不覆盖既有用户改动。

## 2. 仓库认知与现状判断

### 2.1 技术、规范与验证边界

- `Bing.Data.Sql` 为 SDK 风格 .NET `netstandard2.0` 类库，使用 `Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4`；测试使用 xUnit、`net6.0;net8.0`，Benchmark 为 `net8.0` + BenchmarkDotNet `0.14.0`。
- 适用规则：根 `AGENTS.md`、`.github/copilot-instructions.md`、`ai_docs/sql-public-api-governance.md`。SQL 输出必须完整字符串断言；修改公共 API、映射、缓存或渲染必须有直接单测、SQLite 本地集成和最终生产符号到测试映射。
- SQLite 集成总是使用临时文件数据库；MySQL/PostgreSQL/SQL Server/Oracle 仅在 `RUN_INTEGRATION_TESTS` 或 Provider Gate、受控测试连接和安全库校验满足时执行。常规 CI 的真实命令是 `dotnet build -c Release` 与将全部外部 Gate 设为 false 的 `dotnet test -c Release`。
- 当前计划目录此前不存在；相近的 `sql-lambda-query-api-v4` 计划与执行报告已完成，保留其正确性证据，但其“维护 1～10 元数公共类型”的结论被本任务明确取代。

### 2.2 已实现、部分实现与缺口

| 范围 | 真实状态与证据 | 本次结论 |
| --- | --- | --- |
| 根工厂和独立 Builder | `ISqlQuery` 创建 `Query/Sql/Procedure/From`；`SqlQueryBase.CreateIndependentSqlBuilder()` 为每个描述创建 Builder。 | 已实现且保留；根对象不得重新暴露 Builder/Clause。 |
| 高元数 Lambda | `ISqlQuery.From<T1,...,T10>`、`SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery.Arity02..10.cs` 和 Python 生成器都真实存在。 | 已实现但与目标冲突，删除公开 1～10 类型及 3+ 参数表达式。 |
| 多根来源/Join | `FromClause.SetRoots/AppendRoot` 保存 `TableSource`；`JoinClause.Join` 通过参数/别名/Select 候选副本并有失败回滚。 | 基础能力可复用；改为连续 `From<TEntity>`、二元方法泛型和来源句柄定位。 |
| 参数、过滤和渲染 | `SqlBuilderBase.CreateRenderSnapshot()` 对动态过滤器创建 Builder 快照；分页 Count/Data 也 Clone。 | 能防污染但每次渲染和分页多 Clone；增加冻结计划、版本化实例缓存与增量候选，不能缓存可变 Builder。 |
| 终结与执行 | `SqlQuery`/`SqlQuery<TResult>`、`SqlTextQuery<TResult>`、`ISqlQueryPlanExecutor` 与 Dapper 真实执行、分页、流式、取消已接通。 | 结果入口混合：Lambda 已使用显式泛型终结但名称未统一为目标 `ToEntity/ToList/ToPage`；Raw 当前在创建处固定 TResult。统一并删除 `.As<TResult>()`（搜索未证实现有公开 As，仍以 API Contract 强制禁止）。 |
| 诊断 | `DiagnosticsMessage` 仅有每消息新建的 `OperationId`；Dapper `Before/After/Error` 通过克隆同一消息保持它。 | 需替换为 QueryContext/Execution/Trace/Span/Phase，并接入 Activity 与 Core tracing，不能让 Data.Sql 依赖 ASP.NET Core。 |
| Runtime 边界 | `ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController`、`SqlQueryPlan` 为公开 SPI；桥接类已 internal；`AssemblyInfo.cs` 仍有 `InternalsVisibleTo("Bing.Dapper.Core")`。 | Dapper friend assembly 未治理完成，先做符号矩阵再以最小 Runtime SPI 移除。 |
| 测试和性能 | V4 已有 1～10 SQLite Unit/Integration、Join 原子失败、Root/Join Benchmark；正式基准显示 10 Join 构建约 `36.737 us/129.83 KB`，为历史基线。 | 覆盖仍绕过目标 API；无 Query 生命周期、缓存、条件组、诊断完整矩阵。重建全部 API 驱动的 1～10 表矩阵与新前后基线。 |

### 2.3 完成度、风险与可维护性

- 当前目标完成度约 **35%**：独立查询描述、Dapper 物化、原子 Join、动态过滤渲染快照和 Provider Profile 是可复用的真实实现；非泛型描述、方法级 API、明确生命周期、实例缓存、可观测性和无生产 IVT 仍未实现。
- 高元数类、`ISqlQueryPlanExecutor` 的 2～7 Dapper 多映射重载及 `SqlQueryRuntimeFactory` 的 1～10 工厂形成重复符号与 IDE/Roslyn 负担；`SqlMultiLambdaQuery`、`SqlQuery` 和 `ISqlQueryBuilderSource` 是职责密集文件。
- Clone 与 LINQ 集合复制明显存在于 `FromClause`、`JoinClause.GetTypedSources()`、`SqlBuilderBase.Clone()`、过滤渲染、子查询和分页。历史基准已证明 10 Join 分配高，但不能在测量前假设单一热点或优化幅度。
- V4 文档（`ai_docs/sql-lambda-query-design.md`、`docs/sqlquery-usage.md`、`ai_docs/sql-public-api-governance.md`）仍说明高元数 API，实施时必须作为过期文档整体替换。当前不发现专用 ADR/RFC 或 SQL Roslyn Generator；已有 `tools/SqlLambdaQueryCodegen/GenerateSqlMultiLambdaQuery.py` 只能生成应删除的 arity 代码。

## 3. 目标架构与 API 决策

### 3.1 描述、来源和表达式

1. `ISqlQuery` 只创建独立描述与保有执行资源：`From<TEntity>(string alias = null, string schema = null)`、`FromTable(string table, string alias = null, string schema = null)`、`FromSubquery(...)`、`Sql(...)`、`Procedure(...)`。根不公开 `SqlBuilder`、`GetBuilder()`、`Clear`、可变 Clause 或共享 Fluent 状态。
2. `SqlLambdaQuery` 为唯一公开 Lambda 描述；内部可保留非公开状态/解析核心，但不得保留 `SqlLambdaQuery<T...>` 或将其作为 compatibility shim。连续 `.From<TEntity>()` 追加逗号来源而非替换来源；删除 `From<T1,...,T10>()` 与含义不清的 Lambda `.From("o")`。
3. 所有 Lambda 操作是方法级泛型：单源 `Where<TEntity>`、`Select<TEntity>`、`OrderBy<TEntity>`；关联关系仅使用二元 `Where<T1,T2>`、`Join<TLeft,TRight>`。不公开 `Expression<Func<T1,...,TN,...>>`。当同类实体重复出现，要求 `alias`；普通唯一实体可省略。
4. 引入 `SqlSource<TEntity>` 仅作为可选强定位句柄，并保持别名参数可用：`From<TEntity>(..., out SqlSource<TEntity> source)` 或 `Source<TEntity>(alias)` 的最终具体形式在 P2 原型和 Analyzer 契约中二选一。选择标准是不会使普通调用变冗长、能无歧义处理自连接，且无需高元数返回类型。
5. `WhereGroup(Action<ISqlConditionGroup>)` 支持 `.And<T1,T2>()/.Or<T1,T2>()`、嵌套 Group；先在 `FilterOverlay` 解析、参数预检、别名定位，成功后一次提交。组内以显式嵌套括号为优先级来源，组外按添加顺序 AND；空组无变化，未知来源/重复别名/解析失败不污染状态。

### 3.2 Join、子查询、物化和语义

- Lambda Join 必为原子 `Join/LeftJoin/RightJoin/FullJoin<TLeft,TRight>(predicate, alias, leftAlias, schema)`；Cross Join 不接收谓词。保留低层字符串 Builder `.Join().On()` 作为它自己的 API，Lambda 不恢复后置 `.On()`。
- Join 子查询、重复实体、多级 Join 均写入候选 `JoinCandidate`；校验 Profile、数据源/物理库、租户、MappingProfile、表引用、别名、参数、投影和 Select 后提交。任一失败不得改变来源、Join、别名、参数、Select、Operation、版本或缓存。
- 删除 `.As<TResult>()`。结构化/Raw 统一由终结方法决定结果：`ToEntity<TResult>`、`ToList<TResult>`、`ToPage<TResult>`、`ToDictionary<TKey,TValue>` 及 async 对称方法；保留 `First/FirstOrDefault/Single/SingleOrDefault/Scalar` 仅在语义不与 `ToEntity` 重复时，P4 定稿并删除重复入口。
- 固定语义：`ToEntity` 至多一行，0 行为 default，>1 行抛异常；`First` 0 行抛异常、`FirstOrDefault` 0 行 default，均可由 Provider 限制为 1；`Single`/`SingleOrDefault` 至少读取 2 行检测多行，不能静默截断；`ToList` 完整物化；流式使用 `AsEnumerable/AsAsyncEnumerable` 并持有执行租约直到枚举结束。所有终结支持 timeout，异步支持 CancellationToken，事务来自根绑定资源，DTO 使用现有 Dapper 映射。

### 3.3 生命周期、冻结与缓存

`Draft -> Frozen -> Executing -> Completed`：Draft 可变；`ToSql()` 默认产出无副作用的渲染快照但不冻结，另由内部 `Freeze()` 在终结或显式调试冻结时固化 `SqlQueryPlan`。Frozen 可重复执行，不可再修改；修改抛出明确 `InvalidOperationException`。`Clone` 产生新的 Draft、拥有新的 QueryContextId 且 Parent 指向来源；同一描述不允许并发修改或并发执行，执行租约拒绝冲突。根可创建多个独立描述。

冻结计划保存不可变的来源/Clause/参数布局/Provider/Mapping/Tenant/DataSource 身份快照；分页从同一个 Frozen 计划派生 Count 和 Data 子计划，且二者共享 QueryContextId、分别具有 QueryPhase。子查询在作为来源前冻结。调试 SQL 不写入参数、不触发过滤器回写、不创建执行身份。

缓存为查询实例内 `ShapeVersion/CachedVersion/CachedSql/CachedParameterLayout`：所有成功结构变化经 `Touch(QueryMutationKind)` 递增；`WhereIf(false)` 不 Touch，`WhereIf(true)` 仅成功提交后 Touch；失败不 Touch。SQL Shape 与参数值分离，缓存中不含 Builder、连接、事务、租户值、QueryContextId、ExecutionId、TraceId、SpanId 或日志 Scope。动态过滤按参数值、条件数量、位置、运算符、IN 长度、null 变 `IS NULL`、raw SQL、Provider/Mapping/Tenant Policy 分类；只有 Shape 与环境指纹稳定时可命中，其他情况绕过缓存或重建布局。

### 3.4 诊断、Runtime、目录与 Generator 门禁

- QueryContextId 在描述创建时生成；Clone/子查询写 ParentQueryContextId；每个 Count/Data/Subquery/Retry 实际执行生成 ExecutionId，TraceId/SpanId 优先 `Activity.Current`，无 Activity 时从 `ICorrelationIdProvider`，再回退 `TraceIdContext.Current`。事务使用现有 TransactionId；不把任何 ID 写进 SQL 注释。
- 诊断、DiagnosticListener、Activity Tags 和 `ILogger.BeginScope` 共享同一执行上下文，Before/After/Error 绝不重新生成 ExecutionId。Data.Sql 只引用 Bing.Core tracing 抽象，不引用 ASP.NET Core。
- 生产 IVT 目标为零。先判定 `Bing.Dapper.Core` 对 internal 的每一项访问，再将必要协作收敛为 `Bing.Data.Sql.Runtime` 下的计划执行、绑定、不可变快照契约；不公开 Builder/Clause 镜像，不创建万能 Runtime 接口。Tests/Benchmarks friend 可保留，`Bing.Dapper.SqlServer.Tests` 也须矩阵证明。
- 按职责拆到 `Queries/Abstractions|Fluent|Descriptors|Context|Plans|Rendering|State|Internal` 和 `Runtime/Execution|Binding|Diagnostics|Snapshots`；一公开主要类型一文件，partial 仅同类型清晰职责。先完成 API 删除再判断剩余重复；现有 Python arity generator 停用/删除或改为不生成高元数，不改为 Source Generator。仅当剩余机械 API 达到可量化维护成本时做 Generator Spike，要求 XML、快照、Public API Contract、构建失败可见；否则不引入。

## 4. API 与迁移矩阵

| 当前 | 目标 | 处理 |
| --- | --- | --- |
| `SqlLambdaQuery<T1,...,TN>` | `SqlLambdaQuery` | 删除公开 arity 类型和生成器输出，不保留转发。 |
| `From<Order>().From("o")` | `From<Order>("o")` | 合并别名入实体 From。 |
| `From<T1,...,T10>()` | 连续 `From<TEntity>()` | 删除。 |
| 高元数 `Where/Select/GroupBy/Having/OrderBy/Join` | 一元、二元和 `WhereGroup` | 删除 3+ 参数表达式。 |
| `.Join().On()` Lambda 链 | 原子 Join | Lambda 删除/不公开；低层 Builder 保留。 |
| `.As<TResult>()` | TResult 终结方法 | 删除，无兼容层。 |
| 现有 `First/Single/ToList/ToPage` | 明确 `ToEntity/ToList/ToPage` 与必要基数终结 | P4 逐项收敛，API Contract 固定空/多行/限制语义。 |
| `DiagnosticsMessage.OperationId` | `QueryContextId` + `ExecutionId` 等 | 删除 OperationId，更新日志/测试/PublicAPI。 |
| `InternalsVisibleTo("Bing.Dapper.Core")` | 最小 Runtime SPI | 先迁消费者，再删除 IVT。 |

## 5. 统一测试矩阵

- Unit：所有 1～10 表逗号 From 和 2～10 Join 必须由根 `ISqlQuery` 经新公开 API 创建；每例中文测试目的、AAA、完整 SQL、参数名称/值/顺序、别名/来源顺序。覆盖 Inner/Left/Right/Full/Cross、子查询、自连接、重复别名、WhereIf true/false、null/空 IN/不同 IN 长度、动态过滤、条件组嵌套、Join 失败重试、Clone 独立、ToSql 重复、冻结后修改、并发拒绝、Context/Trace 继承。
- Integration：SQLite 1～10 表逗号 From、2～10 Join、DTO、ToEntity/List/Page、WhereIf、动态过滤、软删除/租户、自连接、子查询、Count/Data、同步/异步、事务、取消、流式和隔离。每张表仅插入匹配、未匹配、干扰和必要一对多行，避免笛卡尔积弱断言。
- Provider：MySQL/PostgreSQL/SQL Server/Doris PR 冒烟为 1/2/5/10，Nightly/Release 完整 1～10；Oracle 保留现有受控矩阵。外部数据库没有 Gate 时只报告 `NOT_RUN_EXTERNAL_GATE_MISSING`，不改 Skip。
- 性能：新基准为 `Build_1/2/5/10Table`、`Render_First/Repeated`、`WhereIf_True/False`、`Join_2/5/10`、`DynamicFilter_Render`、`Clone_Query`、`Join_Failure`，记录 Mean、Allocated、Gen0/Gen1、SQL 长度、SDK/runtime/BDN/Job；同环境才比较前后，绝不预设收益。

## 6. 分阶段执行计划

所有任务的共同规则：先读取变更文件；仅修改列出的已确认范围及引用矩阵证明的候选范围；SQL 测试全量断言；不删除失败测试或以 TODO/吞异常伪造完成。每阶段在 `execution.md` 写入命令、退出码、TFM、测试数、警告和 Git 归属。

### Phase 0：仓库认知、行为基线和引用矩阵

#### P0-T01（P0）基线、dev_v6 比较与公共引用审计

- 目标/证据：以当前 V4 实现（`ISqlQuery`、高元数 arity 文件、`SqlQueryPlan`、Dapper `SqlQueryBase`、V4 execution）建立可复核基线；验证远端 `dev_v6.0` 仅用于调用体验对比，不作为实现来源。
- 前置依赖：无。
- 已确认文件：`ISqlQuery.cs`、`PublicAPI.*.txt`、`AssemblyInfo.cs`、`SqlQueryApiContractTest.cs`、`ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`docs/sqlquery-usage.md`、`appveyor.yml`、`docs/integration-testing.md`。
- 候选文件：所有 `Bing.Dapper.*`、EF Core、FreeSql、SkyApm、Analyzer、samples/modules 的引用点；仅记录/修改实际消费者。
- 步骤：记录 Git 状态/diff；枚举所有公开类型/成员和消费者；逐符号建立 `API|程序集|消费者|Unshipped/Shipped|重复/兼容|保留/删除/internal` 表；采集 dev_v6 调用示例并产出“旧/当前/目标/保留优点/禁止缺点”表；运行现有基准 Dry 和指定测试，不以历史数字替代。
- API/Breaking：无生产改动。
- 测试/专项：基线覆盖 `From/Join/WhereIf/filters/Clone/ToSql/page/diagnostic/Runtime`；检查外部 Gate 和现有生成器。
- 风险：远端分支不可访问或当前工作区脏；分别标记 `待验证`，绝不 fetch、reset、restore。
- 验收：`execution.md` 有真实矩阵、差异归属、命令/退出码；未进入实施前的需求冲突全部列明。

### Phase 1：非泛型描述与生命周期

#### P1-T01（P0）建立独立 Draft/Frozen 查询描述

- 目标/证据：替换 `SqlLambdaQuery<TEntity>`/`SqlMultiLambdaQuery` 对可变 Builder 的直接长期持有；现有 `SqlQuery` 每次 `GetPlan()` 仍引用 Builder，必须冻结。
- 前置：P0-T01 API/消费矩阵。
- 已确认文件：`Queries/SqlQuery.cs`、`SqlQueryPlan.cs`、`SqlLambdaQuery.cs`、`SqlMultiLambdaQuery.cs`、`SqlSubquery*.cs`、`SqlQueryRuntimeFactory.cs`、Dapper `SqlQueryBase.QueryPlan*.cs`。
- 候选文件：`SqlBuilderBase.cs`、参数快照、分页/流式实现、Runtime snapshots。
- 步骤：引入不可变 descriptor/state/plan 结构及 phase 枚举；构建 Draft 只写查询状态；终结首次 Freeze；明确 ToSql 无副作用；实现重复执行、修改拒绝、Clone Parent 和执行租约；让分页从同一计划派生 Count/Data；将子查询冻结到只读快照。
- API/Breaking：公开 `SqlLambdaQuery` 非泛型；删除所有高元数继承/构造暴露；不增加根状态 API。
- 测试：生命周期、重复终结、冻结后修改、Clone/子查询父子关系、并发修改/执行拒绝、Count/Data 计划隔离、调试 SQL 无副作用。
- 集成/性能：SQLite 重复执行、分页、流式租约；新增 Freeze/ToSql baseline。
- 风险：冻结改变过滤器捕获时机；以创建/Freeze 上下文规则和动态过滤分类测试锁定。
- 验收：不存在描述间 Builder 共享；Frozen 无可变 Clause；所有执行路径使用 Frozen Plan。

### Phase 2：From 与方法级泛型扩展

#### P2-T01（P0）移除高元数来源并实现连续 From

- 目标/证据：`ISqlQuery` 当前仍公开 1～10 `From`，`FromClause` 已有 `AppendRoot`，可作为连续 From 的候选基础。
- 前置：P1-T01。
- 已确认文件：`ISqlQuery.cs`、`SqlQueryBase.cs`、`SqlQueryRuntimeFactory.cs`、`FromClause.cs`、`TableSource.cs`、`EntityAliasRegister.cs`、`PublicAPI.Unshipped.txt`。
- 候选文件：对象名解析/验证、`SqlSubquery`、Analyzer Compile Contract。
- 步骤：实现 `From<TEntity>(alias,schema)` 连续追加、`FromTable` 和 `FromSubquery`；移除 `From<T1..T10>`、`SetRoots` 的 Lambda 公共路径和 arity 工厂；建立重复实体 alias/source 解析，选择并实现 P3.1 的句柄方案；拆出 From/Select/Group/Order 扩展，所有方法只接受一元或二元表达式。
- API/Breaking：删除高元数 Query/From，删除 Lambda `From("alias")`；不改变低层 `ISqlBuilder.From` 安全校验。
- 测试：新 API Contract/Roslyn 正负编译、1～10 连续 From 完整 SQL/参数/别名、schema、重复实体、自连接歧义、未注册来源。
- 集成/性能：SQLite 逗号 From 1～10；Build 1/2/5/10 基准。
- 风险：将原字符串 raw API 混入结构化 Lambda；FromTable 仍必须走现有安全表引用解析。
- 验收：公开元数查询类型/From 为零，普通单表示例和逗号三表目标示例可编译且完整渲染。

### Phase 3：原子 Join、Where 与条件组

#### P3-T01（P0）使用二元绑定和增量提交实现复杂关系

- 目标/证据：现有 `JoinClause` 原子性已存在，但 `GetTypedSources` 和高元数 predicate 仍依赖类型位置并复制集合。
- 前置：P1-T01、P2-T01。
- 已确认文件：`JoinClause.cs`、`WhereClause.cs`、`SelectClause.cs`、`GroupByClause.cs`、`OrderByClause.cs`、`ParameterManager.cs`、`EntityAliasRegister.cs`、`SqlBuilderBase.cs`。
- 候选文件：Provider profiles、表达式解析器、子查询/过滤器实现。
- 步骤：实现 `ParameterWriteSet/AliasWriteSet/SelectDelta/JoinCandidate/FilterOverlay`；将 Join、Where 和 Group 解析验证放入候选；二元表达式通过 alias/source 定位；加入 `WhereGroup` 递归表达式树及方言渲染；落实 Join 子查询/Right/Full/Cross、失败回滚；仅成功提交后 Touch。
- API/Breaking：删除 3+ 参数 Where/Select/Group/Having/Order/Join；Lambda 无 On；新增公开条件组只暴露必要的 And/Or/Group。
- 测试：每类 Join、子查询、自连接、重复实体、多级关系、全部失败注入、失败后合法重试、条件组优先级/嵌套/参数/别名/异常、WhereIf 不污染。
- 集成/性能：SQLite 2～10 Join，Right/Full 不支持时确认调用点无连接访问；`Join_Failure`、2/5/10 Join 基准。
- 风险：Provider 对 Right/Full/Cross 差异；继续由冻结 `SqlProviderProfile` fail-closed。
- 验收：失败前后 SQL/参数/aliases/sources/select/operation/version/cache 全等；不再存在高元数 Lambda expression。

### Phase 4：终结方法与结果物化统一

#### P4-T01（P0）固定 TResult 与基数语义

- 目标/证据：`SqlQuery` 和 `SqlQuery<TResult>` 真实接入 Dapper，但命名及描述端结果选择不统一。
- 前置：P1-T01、P2-T01。
- 已确认文件：`SqlQuery.cs`、`SqlQueryOfT.cs`、`SqlTextQuery.cs`、`SqlFluentQuery.cs`、`SqlProcedureQuery.cs`、`ISqlQueryPlanExecutor*.cs`、Dapper terminal/paging/streaming partials。
- 候选文件：Dapper parameter binder、公共 Raw multi-map 终结与调用方。
- 步骤：逐个审计 `Query<TResult>/As<TResult>/ToEntity/First/Single/ToList/ToPage`；实现/收敛目标终结方法与 sync/async/timeout/cancellation 对称；明确 limit 注入与 Dapper cardinality；保持 Raw 的同一规则并隔离 low-level multi-map；删除重复入口和无价值泛型切换。
- API/Breaking：删除 As；依据矩阵删除重复 First/Single/ToEntity alias，不为未发布成员保留桥接。
- 测试：反射/API Contract、Roslyn 正负例、0/1/2 行、DTO/实体/标量/字典、timeout/transaction/cancel/stream dispose、Raw SQL 同规则。
- 集成/性能：SQLite 全物化矩阵；执行路径不重复绑定/渲染。
- 风险：`ToEntity` 历史含义可能与 FirstOrDefault 冲突；以 P0 消费矩阵决定迁移并在文档明确。
- 验收：每种终结只有一种可理解职责，目标单表示例与 Raw 示例均实际执行。

### Phase 5：QueryContext、ExecutionId 与 Trace

#### P5-T01（P1）替换 OperationId 并贯通诊断

- 目标/证据：`DiagnosticsMessage.OperationId` 为消息默认 Guid，缺少查询/执行/链路身份；Dapper已统一 Before/After/Error 入口。
- 前置：P1-T01、P4-T01。
- 已确认文件：`DiagnosticsMessage.cs`、`SqlQueryBase.Diagnostics.cs`、`SqlQueryBase.QueryPlan*.cs`、`SqlTransactionDiagnosticInfo.cs`、Core `ICorrelationIdProvider.cs`、`TraceIdContext.cs`。
- 候选文件：SkyApm diagnostics、日志配置、SQLite observer/Provider tests。
- 步骤：创建 Query/Execution context 值对象；在 descriptor/plan/terminal 分配身份和 phase；Activity first、Core correlation second、TraceIdContext fallback；填 DiagnosticListener、Activity tags、Logger scope；处理 clone/subquery/page/retry 关系，删除 OperationId。
- API/Breaking：诊断公共消息替换属性，无兼容 OperationId。
- 测试：Before/After/Error 相同 ExecutionId、每次执行不同 ExecutionId、Clone Parent、Count/Data QueryContext/Phase、Activity Trace/Span、Correlation/TraceId fallback、无 ASP.NET 引用和 SQL 注释。
- 集成/专项：SQLite 诊断观察器、取消/错误/事务；Activity allocation 基线。
- 风险：敏感参数泄露；沿用现有脱敏路径，ID 不写 SQL/缓存键。
- 验收：所有执行诊断具备完整且一致的五类上下文，OperationId 不再是公开 API。

### Phase 6：ShapeVersion、缓存与 Clone 优化

#### P6-T01（P0）实现实例级缓存并降低不必要复制

- 目标/证据：现有 `CreateRenderSnapshot`/分页 Clone 防污染但无 ShapeVersion/缓存；历史 10 Join 分配较高。
- 前置：P1-T01、P3-T01、P5-T01。
- 已确认文件：`SqlBuilderBase.cs`、Clause/parameter/alias classes、`SqlQueryPlan.cs`、`SqlBuilderRuntimeBridge.cs`、Dapper paging/preparation、filter files。
- 候选文件：Provider mapping/database identity cache；只在环境指纹测试证明需要时更改。
- 步骤：实现 `Touch`、版本与只读 layout；把 value/layout 分离；定义过滤指纹/绕过规则；以增量候选替代 Join/Filter 的全 Builder 深 Clone；保留显式 Clone、子查询冻结和分页派生；所有失败回滚不变更 ShapeVersion。
- API/Breaking：仅内部状态，禁止新增全局缓存配置。
- 测试：命中/未命中、重复 ToSql 同字符串、WhereIf true/false、IN/null/raw/filter/provider/mapping/tenant 变化、缓存 SQL/参数一致、跨租户/数据源、Clone 和 Count/Data 隔离。
- 集成/性能：SQLite filter/page isolation；完整规定 Benchmark 前后对比，分析 ParameterManager/Select/Alias/StringBuilder 分配。
- 风险：缓存污染是 P0；不满足指纹证明时宁可 bypass，不以缓存命中率换正确性。
- 验收：缓存从不保存可变 Builder/执行资源，所有污染场景绿，性能结果有原始 artifact。

### Phase 7：Runtime SPI 与生产 IVT 清理

#### P7-T01（P0）移除 `Bing.Dapper.Core` 生产 friend assembly

- 目标/证据：V4 已 internalize bridge，但 `AssemblyInfo.cs` 仍授予 Dapper Core 整体 internal 访问。
- 前置：P0-T01 符号矩阵、P1-T01 Frozen Plan。
- 已确认文件：`AssemblyInfo.cs`、`ISqlQueryPlanExecutor*.cs`、`ISqlQueryRuntimeBindingController.cs`、`SqlQueryPlan.cs`、`SqlQueryRuntimeBinding.cs`、Dapper Core 全部 SqlQuery/Executor/PreparedCommand 文件。
- 候选文件：EF Core/FreeSql factories、Provider 和 CustomProvider tests。
- 步骤：逐个编译驱动地记录 IVT 使用；将必须跨程序集的计划执行、资源绑定、不可变执行快照迁至窄 Runtime namespace；internalize 非必要接口；迁移 Dapper，检查 EF/FreeSql/Provider 不受影响；删除 IVT 并审计剩余 friend。
- API/Breaking：Runtime 名称空间/契约可能变化，所有非发布成员只更新 Unshipped。
- 测试：无 IVT 外部 consumer compile contract、Dapper Core/EF Core/FreeSql 构建、运行时绑定拒绝/成功。
- 集成/性能：SQLite Query/Executor/transaction 路径；禁止每次服务定位或反射桥接。
- 风险：过度公开内部模型；以“消费者实际需要的操作”而不是类型镜像确定 SPI。
- 验收：`InternalsVisibleTo("Bing.Dapper.Core")` 删除，官方消费者编译和执行通过，普通 Public API 无 Builder 泄露。

### Phase 8：文件、目录、命名空间和 XML 治理

#### P8-T01（P1）按最终职责物理收敛

- 目标/证据：`SqlQuery`、`SqlMultiLambdaQuery`、`ISqlQueryPlanExecutor` 和 arity 生成文件职责过载。
- 前置：P2-T01 至 P7-T01 API 稳定。
- 已确认文件：Queries 下所有描述/计划、Runtime/diagnostics、Public API、tests 中巨型 `SqlQueryApiContractTest` 与 `SqliteExecutionIntegrationTest`。
- 候选文件：现有 generator 与 `tools/SqlLambdaQueryCodegen`；无剩余高元数工作则删除工具和生成产物。
- 步骤：按第 3.4 目录移动，不无意义改命名空间；一公开类型一文件；以 partial 切分 terminal/paging/streaming 时保持单一类型职责；重写 XML 注释表达 lifecycle/alias/结果语义；检查 Generator gate 并记录“不适用”或完成最小 Spike。
- API/Breaking：目录内部变更不改变未计划的公共名；API 基线只反映实际第 4 节删除。
- 测试/专项：API 导出清单、XML doc warnings、构建/Analyzer；物理拆分后全回归。
- 风险：大范围移动制造噪声；仅在职责变化已完成后移动，分 Task 保持可 review。
- 验收：没有 arity/Generator 残留；目录对应职责；所有公开 API 有准确 XML 注释。

### Phase 9：1～10 表 Unit 与 SQLite 集成矩阵

#### P9-T01（P0）以真实公开 API 重建正确性证据

- 目标/证据：V4 测试真实但调用已废弃 arity API，不能作为新 API 覆盖。
- 前置：P2-T01 至 P6-T01。
- 已确认文件：`Bing.Data.Sql.Tests` Query/Builder tests、`Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`、SQLite Integration fixture/arity samples/Execution test。
- 候选文件：新增按 `From/Join/Conditions/Lifecycle/Diagnostics/Materialization` 分拆的测试文件与 shared seed helper。
- 步骤：落实第 5 节每条 Unit 与 Integration 矩阵；将旧 1～10 用例迁为连续 From/二元 Join；每例只通过 `ISqlQuery` 公开扩展；按 Provider 特性拆 Right/Full；维护符号->测试追溯。
- API/Breaking：仅测试和文档迁移，不允许使用 internal Core 绕过。
- 测试：所有矩阵必做；测试方法英文 `Method_State_Expected`、中文 XML 目的、完整 SQL。
- 集成：SQLite 1～10 From/2～10 Join、all materialization/context/transaction/cancel/isolation；外部 Provider PR/nightly 分层。
- Benchmark：确认测试辅助不进入生产热路径。
- 风险：用相同 Id 的弱数据掩盖错误；每表设置匹配/干扰/空值/一对多，断言行数和首中尾投影。
- 验收：无高元数 API 测试依赖；SQLite 全矩阵真实执行；外部 Gate 状态诚实记录。

### Phase 10：Benchmark、Public API、文档和最终回归

#### P10-T01（P0）发布前收口与执行审计

- 目标/证据：现有 Root/Join Benchmark 和 Public API files 可复用，但都反映旧高元数设计。
- 前置：P0-T01 至 P9-T01。
- 已确认文件：`SqlLambdaRootBenchmarks.cs`、`SqlLambdaJoinBenchmarks.cs`、Benchmark csproj/artifacts、`PublicAPI.*.txt`、`ai_docs/sql-metadata-test-traceability.md`、SQL 使用/设计/治理文档、CI docs。
- 候选文件：Release notes 或 docs toc，仅当当前导航实际引用 SQL 文档时更新。
- 步骤：替换基准为第 5 节十场景，保存环境和 artifact；更新 Unshipped（Shipped 只读，除非 P0 证明已发布变更）；更新迁移/usage/design/governance/traceability，包含 v6 示例、生命周期、条件组、缓存边界、追踪、Provider Gates；全量验证与 diff 审计。
- API/Breaking：发布迁移表必须包含所有第 4 节项、无兼容层原因和明确删除的旧类型。
- 测试/专项：全量 build/unit/analyzer/SQLite/provider；基准同机比较；检查 IDE/IntelliSense/Roslyn 性能只能标记 `待验证`，不虚构量化收益。
- 风险：文档/Unshipped 与实现漂移；以 API Contract 和导出清单反查。
- 验收：所有计划目标及追溯映射完成；`git diff --check` 通过；外部 Gate 缺失项目有范围说明；无 commit/push/PR。

## 7. 实际验证命令

执行前在 PowerShell 设置 UTF-8，命令来自现有 csproj、solution 和 AppVeyor 配置：

```powershell
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
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*" --job Dry
git diff --check
```

外部 Integration 仅在受保护连接和对应 `RUN_*_INTEGRATION_TESTS=true`、必要时 `ALLOW_DATABASE_RESET_FOR_TESTS=true` 已提供时运行；不得把连接字符串写入仓库或 execution.md。

## 8. 后续 Executor 强制要求

1. 必须读取并使用 `.agents/skills/execute-plan/SKILL.md`，先执行：
   `node .agents/scripts/task-state.mjs start sql-query-dev-v6-api-refactor --source codex`
2. 创建并持续维护 `ai_docs/tasks/sql-query-dev-v6-api-refactor/execution.md`，每项记录状态、变更、命令、退出码、风险和计划偏差。
3. 不因完成单文件、Task 或 Phase 停止；Build/Test 首次失败必须定位根因并继续安全修复。
4. 不用空实现、TODO、吞异常、弱断言、删除失败测试或关闭 Analyzer 伪造完成；不修改 `PublicAPI.Shipped.txt` 逃避未发布 API 收敛。
5. 每 Phase 运行最近验证，最终运行适用全量验证、检查 Git Diff 和 `git diff --check`；不执行 `git add/commit/push/PR/reset --hard/clean/restore`。
6. 所有阶段达到合法终态、执行报告注明未自动提交后，执行：
   `node .agents/scripts/task-finish.mjs sql-query-dev-v6-api-refactor`

## 9. 完成定义

- 单一非泛型 `SqlLambdaQuery` 与连续 From、方法级一元/二元泛型、原子 Join/WhereGroup、别名/自连接能力全部真实进入 Dapper/Provider 执行链。
- 无公开高元数类型、From/表达式、As 或 Lambda 后置 On；结果物化、生命周期、冻结、缓存污染防护、诊断身份和 Runtime SPI 均有直接测试。
- SQLite 的 1～10 表矩阵真实执行；Provider PR/nightly Gate 矩阵有效；所有 SQL 断言完整。
- 缓存/Clone 优化有同环境 benchmark 证据；Generator 只有在 API 删除后仍有事实依据才引入。
- `InternalsVisibleTo("Bing.Dapper.Core")` 已删除，公共 API/Unshipped/文档/追溯映射一致，且最终无未经说明的验证失败。