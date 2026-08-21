# Bing.Data.Sql Lambda 查询 API 收敛与 1～10 表测试完善计划

Status: APPROVED_FOR_EXECUTION

## 1. 任务信息

- Task ID：`sql-lambda-query-api-v4`
- 计划日期：2026-08-20
- 工作流：V4 Universal Agent Workflow
- 类型：破坏性 API 收敛、查询运行时边界治理、原子状态修复、测试与性能基线完善、文档同步
- 计划输出：`ai_docs/tasks/sql-lambda-query-api-v4/plan.md`
- 后续产物：执行 Agent 创建并持续维护 `execution.md`；Reviewer 创建并维护 `review.md`。本次计划阶段不提前创建空文件。
- 适用规范：根 `AGENTS.md`、`.github/copilot-instructions.md`、`.github/prompts/create-plan.prompt.md`。仓库搜索未发现 `framework/` 或 SQL 子目录下额外的 `AGENTS.md`。
- 执行限制：不自动 commit、push、tag、创建或合并 PR、发布 NuGet；不关闭 Public API Analyzer，不以 Skip、弱断言或兼容转发掩盖破坏性变更。

## 2. 仓库认知与基线证据

### 2.1 技术栈与真实验证边界

- 生产代码为 C#/.NET，`Bing.Data.Sql` 使用 SDK 风格项目并引用 `Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4`。
- 普通测试使用 xUnit，共享 `framework.tests.props`，目标框架为 `net8.0;net6.0`；Benchmark 项目为 `net8.0`，使用 BenchmarkDotNet `0.14.0`。
- 本地真实数据库回归项目为 `Bing.Dapper.Sqlite.Tests.Integration`，使用临时 SQLite 文件且无需外部服务。
- MySQL、PostgreSQL、SQL Server、Oracle 集成测试受 Provider 环境变量、专用测试连接和重置许可控制，不可用时只记录未验证范围。
- 当前计划会话没有活动终端，不能取得可信 `git status`、当前构建、测试、警告数或 Benchmark 实测结果。Phase 0 必须在任何代码改动前重新执行并将原始命令、退出码、测试数、警告分类和 Git 状态写入 `execution.md`，不得引用 `ai_docs/stage-*` 的历史数字。

### 2.2 已确认实现

| 能力 | 状态 | 源码/测试证据 | 判断 |
| --- | --- | --- | --- |
| `From<TEntity>()` 与 `From<T1,...,T10>()` | 已实现 | `ISqlQuery` Public API、`SqlMultiLambdaQuery.cs`、`SqlQueryApiContractTest.From_WhenMultipleEntitySourcesSupported_ShouldExposeArityOneThroughTen` | 入口和 1～10 元数类型真实存在，10 元类型无第 11 元 Join。 |
| 多根逗号来源与重复实体别名 | 已实现但覆盖偏弱 | `FromClause.SetRoots`、`SqlQueryDescriptionTest` 的双来源/重复实体/七来源测试 | 生产逻辑具备来源图和自动别名，尚缺 1～10 元逐项完整 SQL、参数和边界矩阵。 |
| 原子 Lambda Join 调用 | 已实现但需加固 | `SqlLambdaQuery.cs`、`SqlMultiLambdaQuery.cs` 调用 `JoinClause.Join(..., predicate)`；公共契约测试确认无 Lambda `.On(...)` | Join 与谓词已合并；字符串 Builder 的低层 `.Join().On()` 仍独立保留。 |
| Join 预检与派生表快照 | 部分完成 | `JoinClause.Join<TEntity>` 使用别名/参数副本并执行映射、表引用、格式化和跨库预检；派生表使用 `ValidateCompatible` 与渲染回滚 | 普通实体 Join 在 `_params.Add` 后才执行投影别名冻结和真实别名注册；Right/Full 能力仍在渲染时检查，尚未证明所有失败点完整回滚。 |
| DTO/标量/分页/流式目标类型物化 | 已接入 | `SqlQuery<TResult>` 调用 `SqlQuery`/`ISqlQueryPlanExecutor`，SQLite 已有 DTO、Scalar、Page、Streaming、Cancellation 用例 | Dapper 最终执行链存在，不是占位；但 Lambda 继承关系导致结果类型 API 语义冲突。 |
| 类型化派生表 | 较完整 | `SqlSubquery<TProjection>`、单/多来源 `SelectSubquery`、Provider/数据源/租户/Mapping Profile/物理身份测试 | 已有较多成功与失败测试，应保留并纳入高元数 Join 回归。 |
| Provider 能力 | 已实现 | `SqlProviderProfile` 及官方 Provider 测试；SQLite 明确拒绝 Right/Full，SQL Server 支持 Full | 当前能力主要在 `ToSql()` 渲染阶段消费，不满足“尽量在调用时失败”的新目标。 |
| Public API 基线 | 已启用 | `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`、`PublicAPI.Unshipped.txt` | Runtime 类型和 `<TNextResult>` 终结方法均明确出现在 Unshipped，可在未发布阶段直接收敛。 |
| 1～10 SQLite 来源测试 | 部分完成 | `SqliteAritySamples.cs`、`Lambda_WhenOneThroughTenTypedSourcesProvided_ShouldExecuteAndMaterializeFirstSource` | 每表仅 `Id/Name`，每表只插入同一 `Id=1`；4～10 元只连接首尾局部来源，不能排除中间笛卡尔积。 |
| 连续 Lambda Join 测试 | 未完整实现 | 单元测试仅少量 3/4 表链，SQLite 真实 Join 主要为双表 | 缺少 1～10 元逐元数完整 SQL和真实执行。 |
| 性能基线 | 部分完成 | `SqlLambdaRootBenchmarks` 仅覆盖根来源 1/2/7/10 | 不存在正式 `SqlLambdaJoinBenchmarks` 及 1/2/5/10 Join 基线。 |
| 使用与追溯文档 | 已过期 | `docs/sqlquery-usage.md` 仍描述 `Select<TProjection>` 切换结果类型，并保留无泛型终结和 `.LeftJoin().On()` 示例；追溯文档残留 `OnCore`、旧测试名和七来源描述 | 必须同步修正；目标 `ai_docs/sql-lambda-query-design.md` 与 `docs/sqlquery-lambda-usage.md` 当前不存在，应由 Phase 10 新建。 |

### 2.3 核心缺口与完成度判断

- API 语义完成度约 45%：来源泛型、原子 Join、目标类型终结链已具备，但 `SqlLambdaQuery<TEntity> : SqlQuery<TEntity>` 和 `SqlMultiLambdaQuery<TResult> : SqlQuery<TResult>` 仍公开无泛型终结方法；`SqlQuery<TResult>` 又公开整套 `<TNextResult>` 重载，违反最终唯一结果类型规则。
- 正确性覆盖约 35%：已有重要局部原子性和派生表测试，但普通实体 Join 的全部失败点、1～10 连续 Join、来源中间位绑定和抗笛卡尔积数据未形成职责级证据。
- Provider 覆盖约 30%：各 Provider 有一般 Builder 测试和少量 Full Join 能力测试，没有统一 2/5/10 Lambda Join、完整 SQL、参数前缀、分页和生命周期矩阵。
- 性能验收约 20%：只有根来源基准且参数为 1/2/7/10，没有 Join 基准和本任务同环境前后对比。
- 文档完成度约 25%：现有文档与源码冲突，两个目标文档尚不存在，追溯映射包含已经失效的符号和测试名。
- 综合完成度约 35%～40%。已有实现不是骨架，但距离目标 API、逐元测试、原子失败证明、Runtime 公共面治理和正式性能证据仍有明显差距。

### 2.4 设计与维护性结论

- 结果类型职责耦合在 `SqlQuery<TResult>` 继承层级中，Lambda 来源类型与物化类型混为一体，调用体验容易误用。
- `SqlMultiLambdaQuery.cs` 集中 2～10 元数类型，后半段已出现高度压缩的单行方法；手工维护会放大 API 对称性和 XML 注释漂移风险。
- `GetBoundSources()` 和 `JoinClause.GetTypedSources()` 每次通过列表与多段 LINQ 重建图；连续 Join 构建时反复扫描已有 Join，存在累计分配与潜在 $O(n^2)$ 风险，但必须以 Benchmark 证实后再优化。
- `ISqlQueryBuilderSource.cs` 混合 Builder 来源、内部 Builder 访问和大体量执行 SPI；`SqlProviderProfile.cs`、`ISqlOutputParameterAccessor.cs` 也分别混合多个独立职责。
- Runtime 类型并非可以直接全改 `internal`：`Bing.Dapper.Core`、Provider、EF Core 集成等独立程序集可能消费它们。Phase 3 必须先建立“生产程序集 -> 符号 -> 调用目的”引用矩阵，再选择 internal、明确 Integration SPI 或最小公开接口。
- 安全边界保持不变：结构化 Lambda 不接收外部原始 SQL；Raw API 仍要求固定模板和参数化值；本任务不得扩大字符串别名或原始 SQL 入口。

## 3. 最终 API 决策

### 3.1 Lambda 查询

- `SqlLambdaQuery<TSource1,...,TSourceN>` 的泛型仅表示来源图，所有元数共享不带默认结果类型的内部组合/执行核心。
- 仅公开 `ToList<TResult>`、`First<TResult>`、`FirstOrDefault<TResult>`、`Single<TResult>`、`SingleOrDefault<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步/取消对称入口。
- 不公开无泛型 Lambda 终结方法，不恢复 `.As<TResult>()`、`SelectAs<TResult>()`、`SelectDto<TResult>()`、后置 `.On(...)` 或 `ToEntity<TResult>()`。
- `Select<TProjection>` 只声明投影形状并返回当前来源元数类型；XML 注释不得声称切换查询描述或结果映射类型。

### 3.2 Raw Fluent/Text 查询

- `Query<TResult>()`、`Sql<TResult>()`、`SqlInterpolated<TResult>()` 在创建时确定结果类型，保留非泛型单结果、分页和流式终结方法。
- 删除 `SqlQuery<TResult>` 仅用于重新选择结果类型的 `<TNextResult>` 重载。
- `SqlFluentQuery<TResult>` 与 `SqlTextQuery<TResult>` 的 Dapper 2～7 对象多映射重载是独立能力，继续保留；它们返回创建时确定的 `TResult`，不泄漏到 Lambda。

### 3.3 Breaking Change 与迁移

| 删除/变化 | 迁移方式 | 兼容策略 |
| --- | --- | --- |
| Lambda `.ToList()`、`.First()`、`.ToPage()` 等 | 显式改为 `.ToList<TEntity>()`、`.First<Dto>()`、`.ToPage<Dto>()` | 项目未正式发布，不保留转发或 Obsolete 包装。 |
| `Query<TResult>().ToList<TNextResult>()` 等 | 在入口指定最终类型：`Query<TNextResult>().ToList()` | 不保留双重结果类型入口。 |
| Lambda 继承 `SqlQuery<TFirst>` | 改为来源专用组合核心 | 通过 API Contract/Roslyn 消费者负例保证误用不能编译。 |
| Runtime 普通公共类型 | 内部化，或移入明确 Runtime/Integration SPI | 仅对真实跨程序集官方消费保留最小公开契约；不新增超大 `ISqlRuntime`。 |

## 4. 全局用例矩阵

| Given | When | Then | 类型/Mock 边界 |
| --- | --- | --- | --- |
| 1～10 个不同或重复实体来源 | 使用逗号 `From` 的 Where/Select/Group/Having/Order/Page | 完整 SQL、参数、来源顺序、第一/中间/最后位置和元数正确 | Unit；只替换元数据/Provider 失败依赖。 |
| 1～10 表连续 Join | 每次新 Join 引用前一来源并最终投影首/中/尾 | 类型链逐元扩展且 10 元停止，完整 SQL/参数稳定 | Unit + SQLite Integration。 |
| DTO、标量、实体、分页、同步/异步/流式 | 指定终结 `TResult` | Dapper 按终结类型物化，基数/取消/资源释放语义保持 | SQLite Integration；不 Mock Dapper 内部行为。 |
| Raw Query/Text 创建时指定 `TResult` | 调用非泛型终结 | 仅以入口类型物化；`<TNextResult>` 不存在 | API Contract + Dapper Core/SQLite。 |
| Join 任一预检或解析步骤失败 | 调用实体/派生 Join | SQL、参数、Join、来源图、别名、Operation、投影逐项等于调用前 | Unit；在系统边界注入确定性失败桩。 |
| Provider 不支持 Right/Full | 调用对应 Lambda Join | 尽可能在调用点失败且无状态残留，不访问数据库 | Provider Unit；SQLite Integration 只证明未访问数据库。 |
| Cross Join | 调用后检查 API/尝试低层 On | Lambda 无谓词参数；低层 Cross On 明确拒绝且无参数 | API Contract + Unit。 |
| 有匹配、无匹配、干扰、一对多、null 数据 | 执行高元数 Join | 行数、字段、排序正确，无意外笛卡尔积 | SQLite Integration。 |
| 1/2/5/10 元 Join | 构建、重复渲染、Clone、投影 | 输出 BenchmarkDotNet 正式统计并与同机基线比较 | Benchmark，不预设无证据阈值。 |

## 5. 分阶段实施计划

所有 Phase 的 `完成状态` 初始为 `NOT_STARTED`。依赖按 Phase 编号执行；局部技术方案可在 `execution.md` 记录调整，但不得改变最终 API 与验收目标。

### Phase 0：建立真实基线

- Task ID / 优先级：`P0-T01` / P0
- 目标：冻结可复现的 Git、API、构建、测试、警告和性能前置状态。
- 前置依赖：无。
- 涉及文件：只读全仓库；写入 `ai_docs/tasks/sql-lambda-query-api-v4/execution.md`。
- 实现策略：记录 `git status --short`、`git diff --stat`、相关文件 diff；读取 Shipped/Unshipped；搜索 `As<`、`SelectAs`、`SelectDto`、`OnCore`、`MultiLambdaQuery_WhenPublicApiInspected_ShouldExposeTypedJoinChainAndOn`、`ProjectionResultTransitions`；执行 Release build、SQL Core、Dapper Core、五 Provider Unit、Analyzer、SQLite Integration；分类编译器/Analyzer/文档警告。首次执行 `SqlLambdaRootBenchmarks` Dry 冒烟，并保存正式历史结果的环境信息但不把历史数据当当前基线。
- API/数据/配置变化：无。
- 单元/集成/性能：仅运行现状；外部 Provider 只检查 Gate，不伪造执行。
- 文档：创建 `execution.md`，首行保持 `Status: IN_PROGRESS`。
- 验收条件：每条命令、退出码、TFM 测试数、跳过数、警告数/代码、外部 Gate、Git 改动文件均有真实记录；失败不阻断后续分析和修复。
- 风险与回滚：现有用户改动可能影响基线；禁止回滚，先标明归属并在后续最小化叠加。基线写入可直接修正文档记录。
- 完成状态：`NOT_STARTED`

### Phase 1：统一结果物化 API

- Task ID / 优先级：`P1-T01` / P0
- 目标：分离 Lambda 来源泛型与 Raw 创建时结果泛型，形成唯一结果类型决定规则。
- 前置依赖：Phase 0。
- 已确认文件：`Queries/SqlLambdaQuery.cs`、`Queries/SqlMultiLambdaQuery.cs`、`Queries/SqlQueryOfT.cs`、`Queries/SqlFluentQuery.cs`、`Queries/SqlTextQuery.cs`、`Queries/SqlSubqueryLambdaQuery.cs`、`ISqlQuery.cs`、`SqlQueryRuntimeFactory.cs`、`SqlQueryApiContractTest.cs`、Public API 基线。
- 候选文件：`SqlQuery.cs`、`ISqlQueryPlanExecutor*.cs`、`Bing.Dapper.Core` 查询执行实现、Analyzer Roslyn 消费契约；以引用分析确认后纳入。
- 实现策略：抽取无默认 `TResult` 的内部 `SqlLambdaQueryCore`/组合核心，集中 Builder、Executor、`ToSql()` 和泛型物化终结；单/多元 Lambda 不再继承 `SqlQuery<TFirst>`；删除 `SqlQuery<TResult>` 的 `<TNextResult>` 单结果/分页/流式重载，保留自身非泛型终结和 Raw 多对象映射；校验同步、异步、分页、流式、超时、取消参数完全对称。
- 破坏性 API：按第 3 节删除，无兼容层；更新 Unshipped，不把未发布成员迁入 Shipped。
- 单元测试：反射契约精确比较 Lambda 允许的终结方法；Raw Query/Text 只允许入口结果；2～7 多映射仍存在；10 元无第 11 元 Join；Roslyn 消费测试验证允许示例编译、`.As<T>()`/Lambda 无泛型终结/后置 `.On()` 不编译。
- 集成测试：改造现有 SQLite 调用为显式 Lambda `TResult`，覆盖实体、字符串、DTO、分页、同步/异步/流式代表路径。
- 性能验证：Phase 9 前只运行 Dry，确认新核心无反射或 dynamic 热路径。
- 文档更新：本 Phase 只更新 XML 注释和 `execution.md`；正式文档 Phase 10 收口。
- 验收条件：三个正向示例可编译执行；三个禁用示例不存在；Public API Analyzer 通过；Raw 多映射无回归。
- 风险与回滚：继承拆除影响面大，先用 API Contract 锁定目标再改实现；若组合核心需调整，回退到同职责非泛型基类，不得恢复默认结果泛型。
- 完成状态：`NOT_STARTED`

### Phase 2：统一 From 与原子 Join 状态图

- Task ID / 优先级：`P2-T01` / P0
- 目标：让单根、逗号多根、实体 Join、派生 Join 使用一致来源图，并保证所有失败在提交前发生。
- 前置依赖：Phase 1。
- 已确认文件：`Builders/Clauses/FromClause.cs`、`JoinClause.cs`、`Builders/Core/TableSource.cs`、`EntityAliasRegister.cs`、`SelectClause.cs`、`SqlBuilderBase.cs`、`SqlSubquery.cs`、Lambda arity 文件。
- 候选文件：Provider 查询能力解析、对象名格式化/表引用/跨库校验、参数管理器；仅在失败测试证明缺口时修改。
- 实现策略：定义一次 Join 候选事务，按 Provider 操作/能力、映射、表引用、格式化、数据库身份/跨库、别名、参数容量、完整谓词、投影别名冻结顺序预检；成功后一次提交参数、Operation、Join、SourceGraph、投影和别名。Right/Full 在调用点读取冻结 Profile 并失败；Cross Join 走无谓词候选路径。派生表继续校验 Provider、数据源、租户、Mapping Profile、物理身份、参数快照和执行 Scope。
- 破坏性 API：异常发生时机从 `ToSql()` 前移到 `.RightJoin()`/`.FullJoin()`；异常类型/消息保持稳定或同步更新契约。
- 单元测试：1～10 元所有 Where/Select/Group/Having/Order/Join 参数位置；Clone/重复渲染；SourceGraph 与别名快照；调用时 Provider 拒绝。
- 集成测试：SQLite 代表路径确认调用时拒绝不打开连接；成功链延后 Phase 7 完整覆盖。
- 性能验证：保留候选副本原子性，Phase 9 测量 Clone/扫描成本后再优化。
- 文档更新：在 `execution.md` 记录提交顺序和最终符号到测试映射草案。
- 验收条件：任一失败后七类状态逐项相等；Cross 无 On；派生兼容边界不回退；连续来源图顺序稳定。
- 风险与回滚：全 Builder 深 Clone 可能分配过高；优先最小候选对象/局部副本，但不得以直接写入再补偿破坏原子性。
- 完成状态：`NOT_STARTED`

### Phase 3：运行时公共 API 治理

- Task ID / 优先级：`P3-T01` / P0
- 目标：从普通用户 API 移除仅供官方程序集使用的 Runtime 类型，同时保留最小跨程序集集成边界。
- 前置依赖：Phase 1，建议在 Phase 2 后稳定执行契约。
- 已确认文件：`ISqlQueryBuilderSource.cs`、`ISqlQueryRuntimeBindingController.cs`、`SqlQueryPlan.cs`、`SqlQueryRuntimeFactory.cs`、`SqlBuilderRuntimeBridge.cs`、`SqlParameterRuntimeBridge.cs`、`SqlMutationRuntimeBridge.cs`、`SqlBuilderExecutionSnapshot.cs`、Public API 基线、`AssemblyInfo.cs`。
- 候选文件：`Bing.Dapper.Core`、五 Provider、EF Core/FreeSql 集成与测试中的所有引用点。
- 实现策略：逐符号输出引用矩阵；仅同程序集使用则 internal；官方跨程序集使用则移动到 `Bing.Data.Sql.Runtime` 或 `Bing.Data.Sql.Integration` 并按 Builder 创建、计划执行、资源绑定、快照分别保留最小契约；第三方无需实现则用公开静态窄入口或内部实现承接。禁止新增超大接口和大量 `InternalsVisibleTo`，现有 Tests/Benchmarks friend 可保留，SQL Server friend 是否仍必要需引用证明。
- 破坏性 API：删除或重定位九个点名 Runtime 类型；同步 Unshipped 和消费者命名空间。
- 单元测试：Runtime API Contract 白名单；每个保留 SPI 的第三方最小实现/消费者编译测试；已内部化类型不得出现在导出类型集合。
- 集成测试：Dapper Core、EF Shared/Independent、事务 Scope、SQLite 查询执行全回归。
- 性能验证：工厂/Bridge 治理不得增加反射查找或每次执行服务定位。
- 文档更新：Phase 10 写入公共治理决策；本 Phase 在 `execution.md` 保存逐类型结论。
- 验收条件：普通 Public API 不再暴露内部桥接对象；真实官方程序集编译；Public API Analyzer 通过。
- 风险与回滚：跨程序集 internal 会直接破坏构建；以引用矩阵为硬门禁，必要时公开最小 Integration SPI，而非恢复原大接口。
- 完成状态：`NOT_STARTED`

### Phase 4：拆分文件、目录与高元数生成

- Task ID / 优先级：`P4-T01` / P1
- 目标：按 Lambda/Fluent/Subquery/Runtime/Provider/Parameter 职责拆分，消除手工九套漂移。
- 前置依赖：Phase 1～3 API 稳定。
- 已确认文件：`SqlLambdaQuery.cs`、`SqlMultiLambdaQuery.cs`、`ISqlQueryBuilderSource.cs`、`SqlProviderProfile.cs`、`ISqlOutputParameterAccessor.cs` 及巨型 SQL/SQLite/SQL Server 测试类。
- 候选文件：新增 `Queries/Lambda/Arity01..10`、`Materialization`、`Queries/Fluent`、`Queries/Subqueries`、`Runtime/QueryExecution|QueryBinding|BuilderSnapshots`、`Providers/Profiles|Capabilities`、`Parameters/Binding|Output`；生成器/模板项目位置先检查仓库现有生成机制。
- 实现策略：优先使用可重复模板或 Source Generator 生成 arity 2～10 API；生成输入定义一次操作矩阵，输出稳定格式；若仓库没有可维护生成基础，使用构建前显式代码生成工具并提交生成产物与校验命令，不引入运行时生成。接口/实现/Snapshot/Converter 分文件；测试按 Contract、From、Join Success、Join Atomic Failure、Materialization 拆分。
- 破坏性 API：物理文件/目录不改变命名空间；生成器输出必须与 Phase 1 API 白名单一致。
- 单元测试：生成快照或 API Contract 覆盖每元数签名、返回元数、10 元上限；拆分后原职责测试保持。
- 集成测试：只跑 Phase 7 代表冒烟，完整矩阵随后执行。
- 性能验证：生成代码不得引入反射/表达式动态调用。
- 文档更新：记录生成命令和文件所有权，不在用户文档暴露实现细节。
- 验收条件：无单文件集中十个 Lambda 类型；生成可重复且二次运行无 diff；三个点名大杂烩文件完成职责拆分。
- 风险与回滚：生成器增加构建复杂度；先做最小 PoC 和快照，失败可回退到模板生成已提交 `.cs`，不可回退手工复制维护。
- 完成状态：`NOT_STARTED`

### Phase 5：补齐 1～10 表成功单元测试

- Task ID / 优先级：`P5-T01` / P0
- 目标：建立 From 与连续 Join 的逐元数、完整 SQL、完整参数证据。
- 前置依赖：Phase 2、Phase 4。
- 已确认文件：`Bing.Data.Sql.Tests` 的 FromClause/Join/Contract 测试，`Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`。
- 候选文件：拆分后的 `Queries/Lambda/*Test.cs` 与共享 10 表样例模型。
- 实现策略：1～10 每元数独立命名测试；可用 MemberData 生成期望，但每个 case 展示元数；每条期望 SQL 上方写中文结构说明；断言完整 SQL、参数名值顺序、来源顺序、返回类型 arity，并让下一 Join 条件引用前一个 Join 来源。
- API/配置变化：无新增产品 API。
- 单元测试：覆盖 Inner/Left/Cross、支持 Provider Right/Full、重复实体显式/自动别名、派生 Join、5/10 混合 Join、DTO、Group/Having/Order、Skip/Take/Page、参数上限、Clone、重复渲染。
- 集成测试：不在本 Phase 扩大，只保证测试代码编译。
- 性能验证：测试辅助不得成为生产抽象。
- 文档更新：更新追溯草案。
- 验收条件：1～10 From 与 1～10 连续 Join 各有可定位失败的独立完整断言；禁止以 `Contains` 代替 SQL 全字符串。
- 风险与回滚：SQL 快照脆弱但这是仓库强制契约；仅集中样例构造，不抽象掉各元数可读性。
- 完成状态：`NOT_STARTED`

### Phase 6：补齐原子失败单元测试

- Task ID / 优先级：`P6-T01` / P0
- 目标：证明普通实体和派生 Join 的全部失败路径不部分提交。
- 前置依赖：Phase 2、Phase 5。
- 涉及文件：拆分后的 Join Atomic Failure 测试、映射/格式化/表引用/跨库/参数限制测试桩；生产文件仅在测试暴露缺陷时最小修复。
- 实现策略：每例失败前冻结 `ToSql()`、`GetParams()`、JoinCount、SourceGraph、AliasState、OperationKind、ProjectionState；分别注入 NullPredicate、DuplicateAlias、MappingResolutionFailure、TableReferenceValidationFailure、ObjectNameFormattingFailure、CrossDatabaseValidationFailure、ParameterLimitFailure、PredicateResolutionFailure、UnsupportedProviderJoin、ProjectionAliasFreezeFailure；异常后逐项深比较。
- 破坏性 API：只允许 Phase 2 已声明的异常时机变化。
- 单元测试：实体和派生表分别覆盖矩阵；重复实体投影冻结失败必须有独立用例；不能只断异常类型。
- 集成测试：SQLite Provider 不支持能力代表用例验证连接未打开。
- 性能验证：不适用，原子方案性能由 Phase 9 衡量。
- 文档更新：最终生产符号 -> 测试方法映射写入追溯草案。
- 验收条件：所有失败用例七类状态完全一致；后续合法 Join 仍可成功，证明别名/参数未被占用。
- 风险与回滚：部分内部状态无只读测试入口时，优先使用现有 friend 测试和内部快照，不新增公共诊断 API。
- 完成状态：`NOT_STARTED`

### Phase 7：补齐 1～10 表 SQLite 真实执行

- Task ID / 优先级：`P7-T01` / P0
- 目标：以有区分度的链式数据证明来源绑定、物化和资源生命周期。
- 前置依赖：Phase 5～6。
- 已确认文件：`SqliteAritySamples.cs`、`SqliteIntegrationDatabaseFixture.cs`、`SqliteExecutionIntegrationTest.cs`。
- 候选文件：按 Arity/Join/Materialization/Streaming/Transaction 拆分的新测试类和 fixture seed helper。
- 实现策略：扩展每表为 `Id`、`NextId`、`Name`、可空值、表序号；每表插入匹配、未匹配、干扰、一对多数据，关系为 `T01.NextId=T02.Id ... T09.NextId=T10.Id`；每个元数重置/播种、真实执行、投影首/中/尾并检查完整 SQL/参数/排序/行数和无笛卡尔积。
- API/数据变化：仅测试数据库 schema；不新增生产配置。
- 单元测试：Phase 5 已覆盖。
- 集成测试：1～10 Inner Join 独立用例；多表 Left null、Cross 乘积、重复实体、DTO、标量、分页、同步/异步代表、取消、流式提前停止/释放、事务内可见性、派生表参与高元数 Join。
- 性能验证：集成测试数据保持小而确定，不做计时断言。
- 文档更新：记录 SQLite 运行时和测试数。
- 验收条件：所有用例真实访问临时 SQLite；无相同 ID 弱数据；完整 SQL/参数与结果共同断言。
- 风险与回滚：巨型单测试导致维护困难；按职责拆类并复用 fixture 数据构造，保留每元数独立测试方法。
- 完成状态：`NOT_STARTED`

### Phase 8：五 Provider 单元与受控外部集成

- Task ID / 优先级：`P8-T01` / P1
- 目标：证明 MySQL/PostgreSQL/SQL Server/Oracle/SQLite 方言和能力一致性。
- 前置依赖：Phase 2、Phase 5～7。
- 涉及文件：五个 `Bing.Dapper.<Provider>.Tests` Builder/Metadata 测试；四个外部 Integration fixture/test；Public API 基线如有 Provider 边界变化。
- 实现策略：五 Provider 统一 2/5/10 Join 场景，分别断言标识符、Schema、Alias、参数前缀、Right/Full 能力、分页、Clone/New 和完整 SQL；不要强行共享抹平方言差异。外部 Gate 可用时运行真实 2/5/10 代表链及分页，严格使用专用测试库与重置授权。
- 破坏性 API：Provider 不支持能力的失败时机同步到调用点。
- 单元测试：五项目全矩阵；SQL Server/PostgreSQL/Oracle 支持能力按真实 Profile，MySQL/SQLite 禁用能力按 Profile 断言。
- 集成测试：MySQL/PostgreSQL/SQL Server/Oracle Gate 可用即执行；不可用记录变量名、缺失条件和范围，不改普通 Skip 策略掩盖。
- 性能验证：Provider 方言渲染只做 Phase 9 代表基准，不增加计时测试。
- 文档更新：执行记录列出每个 Provider 的 Gate 和实际结果。
- 验收条件：五 Provider Unit 全绿；可用外部 Integration 全绿；不可用项明确标记 `NOT_RUN_EXTERNAL_GATE_MISSING`。
- 风险与回滚：外部数据库不可用不阻塞其他 Phase，也不能宣称通过。
- 完成状态：`NOT_STARTED`

### Phase 9：建立 Lambda Join 正式性能基线并按证据优化

- Task ID / 优先级：`P9-T01` / P1
- 目标：量化 1/2/5/10 元构建、重复渲染、参数、重复实体、DTO 和 Clone 成本。
- 前置依赖：Phase 4～8 功能稳定。
- 已确认文件：`Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`、Benchmark csproj、`BenchmarkDotNet.Artifacts`。
- 候选文件：新增 `SqlLambdaJoinBenchmarks.cs`、正式结果目录；仅在数据证明后修改 `GetBoundSources`、`GetTypedSources`、参数 Clone 或查询包装。
- 实现策略：新增 `BuildJoinAndRender`、`RenderExistingJoin`、`BuildParameterizedJoin`、`BuildRepeatedEntityJoin`、`BuildDtoProjectionJoin`、`CloneJoinQuery`，参数 1/2/5/10；先保存基线，再用同机器、SDK、Runtime、Job 比较。检查重复列表/LINQ、累计扫描、参数 Clone、包装对象和重复渲染；仅对显著热点优化。
- API/配置变化：不改变公共 API；Benchmark Job 保留正式 launch/warmup/iteration 配置并记录版本。
- 单元/集成测试：每次优化后重跑 Phase 5～8，尤其原子失败和 Clone 隔离。
- 性能验证：记录 Mean、Median、Allocated、Gen0/1/2、Ratio、环境、SDK、Runtime、BDN 版本、Job；不存在统计证据时不做对象池/全局表达式缓存。
- 文档更新：将基线和优化后结果路径写入 `execution.md` 与追溯文档。
- 验收条件：Root 和 Join 都有 1/2/5/10 正式结果；增长趋势无明显无解释超线性退化；任何优化不污染可变 Builder。
- 风险与回滚：环境噪声导致误判；同环境多轮运行，以分配和趋势辅助判断，性能回归可回退单项优化而保留基准。
- 完成状态：`NOT_STARTED`

### Phase 10：文档、全量验收与 V4 Review/Fix 闭环

- Task ID / 优先级：`P10-T01` / P0
- 目标：同步设计、治理、追溯和使用文档，完成 Release 验证并进入独立 Review。
- 前置依赖：Phase 0～9。
- 已确认文件：`ai_docs/sql-metadata-test-traceability.md`、`ai_docs/sql-public-api-governance.md`、`docs/sqlquery-usage.md`。
- 新增文件：`ai_docs/sql-lambda-query-design.md`、`docs/sqlquery-lambda-usage.md`。
- 实现策略：使用文档包含单表实体/标量/DTO、逗号来源、2/5/10 Join、Left/Cross、派生表、分页、同步/异步/流式、重复实体、Provider Right/Full 异常、Raw 边界、结果类型设计、无 As/后置 On 原因、查询描述不可并发复用；修正或删除 `OnCore`、旧测试名、`ProjectionResultTransitions`、七来源和“Select 切换 SqlQuery”描述。维护最终生产符号 -> 项目/测试方法追溯。
- 破坏性 API：在治理和迁移章节完整列出，不生成兼容 API。
- 单元/集成/性能：运行下节所有命令；外部 Gate 按 Phase 8 记录。
- 验收条件：Release build、核心/Dapper/五 Provider/Analyzer/SQLite 全绿，Public API 基线一致，正式 Benchmark 结果存在；`execution.md` 只有 Reviewer 最终 PASS 后才改 `Status: COMPLETED`。
- 风险与回滚：文档与源码再次漂移；以 API Contract、Public API 文件和实际调用示例反查，不从旧追溯复制结论。
- Review/Fix：执行 `/review-plan sql-lambda-query-api-v4`；`NEEDS_FIX` 时 `/fix-review` 只处理 MUST_FIX，将命令与结果追加 `execution.md`，不改本计划和 Reviewer 的 `review.md`；循环至 `PASS` 或用户停止，再执行 task-finish 收口。
- 完成状态：`NOT_STARTED`

## 6. 已确认与候选修改范围汇总

### 6.1 已确认生产/API 文件

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOfT.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlSubquery*.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/ISqlQueryBuilderSource.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/SqlProviderProfile.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Parameters/Binding/ISqlOutputParameterAccessor.cs`
- 九个点名 Runtime 类型文件、`AssemblyInfo.cs`、`PublicAPI.Unshipped.txt`；`PublicAPI.Shipped.txt` 只核对，不擅自改写已发布基线。

### 6.2 已确认测试/Benchmark/文档文件

- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs` 及 Builders/From/Join 测试。
- `framework/tests/Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`。
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Infrastructure/SqliteAritySamples.cs`、fixture、`SqliteExecutionIntegrationTest.cs`。
- 五个 Provider Unit 项目、Dapper Core 与 Analyzer 测试项目。
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs` 和新增 Join 基准。
- 四份最终目标文档及 `execution.md`；`review.md` 仅由 Reviewer 写入。

### 6.3 候选文件纳入规则

- `Bing.Dapper.Core`、Provider、EF Core、FreeSql 等跨程序集消费者：仅当符号引用矩阵证明受 Runtime/API 调整影响时修改。
- Source Generator/模板项目：先搜索现有生成基础；没有时选择最小可重复生成方案，不为本任务创建通用代码生成平台。
- 生产 Builder 热路径：只有 Phase 6 正确性测试或 Phase 9 数据证明问题时修改，禁止顺便重构。

## 7. 真实验证命令

以下命令来自仓库现有 `.sln`/`.csproj`；执行时使用 PowerShell、UTF-8 控制台并逐条记录结果。若全解决方案因无关模块失败，仍需修复本任务引入问题并继续执行本任务项目级验证，不能以解决方案失败替代项目结果。

```powershell
dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.MySql.Tests\Bing.Dapper.MySql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests\Bing.Dapper.PostgreSql.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Oracle.Tests\Bing.Dapper.Oracle.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*" --job Dry
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambdaRootBenchmarks*" "*SqlLambdaJoinBenchmarks*"
```

外部 Provider 只在各 fixture 要求的 Gate、连接变量和 `ALLOW_DATABASE_RESET_FOR_TESTS=true` 齐全时执行对应 Integration csproj。连接字符串不得写入仓库或 `execution.md`，只记录变量是否存在和脱敏数据库标识。

## 8. 最终验收清单

- [ ] Lambda 无 `.As<TResult>()`、后置 `.On(...)`、无泛型结果终结和 `ToEntity<TResult>()`。
- [ ] Lambda 结果类型只由终结方法决定；Raw Query/Text 只由创建入口决定。
- [ ] `From<T1,...,T10>()` 恰好支持 1～10；连续 Join 到 10；无第 11 Join；Cross 无谓词。
- [ ] 1～10 From、连续 Join 完整 SQL/参数 Unit 全绿，第一/中间/最后绑定正确。
- [ ] 1～10 SQLite 使用强数据真实执行，无意外笛卡尔积，DTO/标量/分页/流式/事务正确。
- [ ] 普通实体与派生 Join 所有失败路径七类状态无残留。
- [ ] 五 Provider 2/5/10 Unit 全绿；外部 Provider 如可用则真实执行，否则诚实记录 Gate 缺失。
- [ ] Runtime SPI 不污染普通 API，Public API Analyzer 与 Shipped/Unshipped 一致。
- [ ] 高元数代码可重复生成或拆分；接口、实现、Snapshot、Converter 分责；巨型测试拆分。
- [ ] Root/Join 1/2/5/10 正式 Benchmark 有完整统计和同环境比较，无未经数据支持的缓存/池化。
- [ ] 四份最终文档与实际 API/测试一致，追溯映射可从生产符号定位到测试方法。
- [ ] `execution.md` 记录最新测试数、警告数和 Git 状态，不引用旧阶段数字。
- [ ] `review.md = PASS` 后方可标记执行完成并 task-finish；全流程不自动提交或推送。

## 9. 执行顺序与停止条件

执行顺序为 `P0-T01 -> P1-T01 -> P2-T01 -> P3-T01 -> P4-T01 -> P5-T01 -> P6-T01 -> P7-T01 -> P8-T01 -> P9-T01 -> P10-T01 -> review/fix loop`。编译、测试、Analyzer、格式或文档失败属于待修复状态，不是自动停止条件。只有权限缺失、用户必需凭据缺失、根本需求冲突或仓库/依赖无法恢复时才可标记 `BLOCKED`；外部数据库不可用不阻塞本地阶段。
