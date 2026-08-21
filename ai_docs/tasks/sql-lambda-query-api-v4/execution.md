<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: sql-lambda-query-api-v4
AI_EXECUTION_FINISHED_AT: 2026-08-21T01:20:07.6625738Z
AI_EXECUTION_STARTED_AT: 2026-08-20T14:01:21.976Z

# 实施执行报告

> Executor 已完成本轮 Review Fix；最终是否通过仍由独立 Reviewer 判定。

## 执行记录

### Phase 0：基线

- SQL Core 基线：`Bing.Data.Sql.Tests`，Release，`net6.0`/`net8.0`，基线 2366/2366 通过；加入全部 Join 原子失败和投影别名冻结用例后当前 2378/2378 通过。
- Dapper Core 基线：`Bing.Dapper.Core.Tests`，Release，`net6.0`/`net8.0`，262/262 通过。
- SQLite Integration 基线：Release，246/246 通过；API 迁移、高元数连续 Join 和 3/4/6/7/8/9 元独立测试后最终达到 266/266。
- Root Benchmark Dry 冒烟已完成，正式 Join Benchmark 后续使用同一 BenchmarkDotNet 版本和 `FormalHost` Job。
- 基线问题：`SqlExecutionKind` Analyzer 动态编译契约缺失属于既有测试基线问题，已补齐动态源的命名空间引用并恢复测试通过；本任务未关闭 Analyzer、未修改 `PublicAPI.Shipped.txt`。

### Phase 1：结果物化 API

状态：已实现并通过定向回归。

- `SqlMultiLambdaQuery` 不再携带结果类型泛型；单表、派生表和 2～10 来源 Lambda 查询统一通过显式 `TResult` 终结方法执行。
- Raw `SqlQuery<TResult>` 删除 `<TNextResult>` 单结果、分页和同步/异步流重载；Raw Fluent 2～7 对象多映射继续由 `SqlFluentQuery<TResult>` 承载。
- `PublicAPI.Unshipped.txt` 已同步新增/删除成员，`PublicAPI.Shipped.txt` 未修改。
- API Contract 已覆盖 Lambda 基类、显式终结方法、Raw 无泛型终结方法、1～10 来源和无 11 来源入口。
- `Bing.Data.Sql.Tests` 当前回归：2378/2378 通过；既有 `SqlLambdaQuery.Select(bool)` RS0027 警告仍保留，未以 NoWarn 或 Shipped 基线修改规避。

### Phase 2：From 与原子 Join

状态：核心实现已完成，失败矩阵仍在扩展。

- 结构化实体 Right/Full Join 在候选 Join、参数、Operation、投影 alias 和来源图提交前读取冻结 Provider 能力。
- 参数和谓词解析使用副本，成功后才合并参数和真实别名；派生表仍使用兼容性校验及渲染回滚。
- 已新增 SQLite 调用阶段 Right Join 拒绝测试，并确认 SQL 未写入且连接未访问。
- 已新增类型化 Join 重复 alias、空谓词失败后的 SQL、参数和类型化来源数量快照测试。
- 已补齐实体映射解析、表引用验证、对象名格式化、跨数据库验证、参数上限、谓词解析、派生表投影和别名冲突失败注入；失败后均比较 SQL、参数、来源图、Select 和 Operation 状态，部分用例验证合法重试。
- 新增重复实体类型化 Join 的投影别名冻结测试 `Join_WhenSelfJoinTypedProjectionAlreadyExists_ShouldFreezeRootProjectionAlias`，确认既有根投影不会被后续 Join 改写；SQL Core Join 定向测试双 TFM 28/28 通过。

### Phase 3：Runtime 公共 API 治理

状态：已完成最小收敛。

- 已确认 `ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController` 和 `SqlQueryPlan` 是真实跨程序集执行/绑定 SPI，保留公开边界。
- 仅由 `Bing.Dapper.Core` 消费的 `SqlQueryRuntimeFactory`、`SqlBuilderRuntimeBridge`、`SqlParameterRuntimeBridge`、`SqlMutationRuntimeBridge` 和 `SqlBuilderExecutionSnapshot` 已内部化。
- 通过 `InternalsVisibleTo("Bing.Dapper.Core")` 保持官方消费者编译，不新增公共大接口。
- SQL Core 2378/2378、Dapper Core 262/262 通过，Analyzer 无新增 RS0016/RS0017。
- Analyzer 动态编译契约补齐 `using Bing.Data.Sql;` 后，`Bing.Data.Sql.Analyzers.Tests` 当前 19/19 通过；该修复仅针对既有测试源缺少 `SqlExecutionKind` 命名空间引用的问题。

### Phase 5/7：1～10 来源与 SQLite 连续 Join

状态：已完成 1～10 元真实执行覆盖。

- Arity 样例增加 `NextId`，SQLite `Arity01`～`Arity10` 表同步增加关系列。
- 新增十表连续 Lambda Join：每一段谓词引用前一来源，数据包含匹配链和每表干扰行，DTO 投影断言首/中/尾字段、唯一结果行、完整 SQL。
- 新增 `Lambda_WhenTwoSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenThreeSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenFourSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenFiveSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenSixSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenSevenSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenEightSourcesAreJoinedSequentially_ShouldMaterializeBoundRows`、`Lambda_WhenNineSourcesAreJoinedSequentially_ShouldMaterializeBoundRows` 和 `Lambda_WhenTenSourcesAreJoinedSequentially_ShouldMaterializeChainWithoutCartesianRows`，通过双 TFM；SQLite Integration 当前 266/266 通过。
- 原有 1～10 逗号来源测试保留，用于入口元数和来源顺序回归；连续 Join 测试证明中间来源不会产生意外笛卡尔积。

### Phase 4：文件职责与生成

状态：部分完成。

- 已新增独立 `SqlLambdaJoinBenchmarks.cs`，使用结构化 JoinClause 表达式覆盖 1/2/5/10 来源。
- 已将 8～10 元 `SqlLambdaQuery<...>` 类型移至独立 `SqlMultiLambdaQuery.Arity08-10.cs`，保持命名空间、继承关系、构造函数和公共方法签名不变；原文件继续承载公共基类及 2～7 元类型。
- 尚未建立可重复生成工具，也未完成 2～7 元类型的进一步职责拆分，当前仍不宣称该 Phase 完成。
- 已同步 `SqlLambdaQuery`、`SqlSubqueryLambdaQuery` 和 2～7 元多表 Lambda XML 注释，明确来源泛型与投影形状不决定最终物化类型；本次未改变公共签名或运行逻辑。

### Phase 8：Provider 验证

- SQLite Unit：198/198 通过。
- MySQL Unit：354/354 通过。
- PostgreSQL Unit：268/268 通过。
- SQL Server Unit：550/550 通过。
- Oracle Unit：180/180 通过。
- 本轮重新回归五个 Provider Unit：SQLite 198/198、MySQL 354/354、PostgreSQL 268/268、SQL Server 550/550、Oracle 180/180，均双 TFM；Provider 测试仅保留既有警告。
- MySQL/SQL Server 受影响消费者定向构建通过；真实外部数据库 Integration 未运行。
- 外部 Gate 状态：MySQL、PostgreSQL、SQL Server、Oracle 未提供本轮可验证连接环境，记录为 `NOT_RUN_EXTERNAL_GATE_MISSING`，未伪造通过。

### Phase 9：Join Benchmark

- `SqlLambdaJoinBenchmarks` 已编译通过。
- BenchmarkDotNet `0.14.0` 的 Dry/`FormalHost` 运行完成 48 个组合，覆盖 `JoinCount=1,2,5,10` 与六个基准方法：构建渲染、重复渲染、参数化、重复实体、DTO 投影和 Clone。
- 本轮记录环境：Windows 10 22H2、.NET SDK 10.0.300、.NET 8.0.27、X64 RyuJIT AVX2、Intel Core Ultra 7 270K Plus。
- 未根据单次基准结果擅自修改生产 Join 扫描或 Clone 逻辑；性能结果作为后续优化基线。

### 全局验证

- Round 1 当时的历史结果：`dotnet build .\Bing.All.sln -c Release -nologo -v minimal --no-restore` 成功，87 条警告；主要为 net6.0 EOL、依赖包 TFM 支持、既有隐藏成员和既有 `RS0027`，无编译错误。
- Round 1 Analyzer 动态编译契约修复后的历史结果：同一全解构建成功，87 条警告，0 errors；当前结果以 Round 2 最新审计记录为准。
- `get_errors` 检查 SQL Core、SQL Core Tests、SQLite Integration Tests 和 Benchmark 项目：无错误。
- arity/API 定向回归：`SqlQueryApiContractTest` 双 TFM 66/66 通过；连续 Join 定向回归双 TFM 18/18 通过。
- SQLite Integration 全量：双 TFM 266/266 通过。
- SQL Core：2378/2378 通过，双 TFM。
- Dapper Core：262/262 通过，双 TFM；Analyzer：19/19 通过，net8.0。
- SQLite Integration：266/266 通过，双 TFM；外部数据库 Gate 未配置时不计为通过。
- `ai_docs/sql-metadata-test-traceability.md` 的 V4 Canonical Addendum 已同步 SQL Core 当前计数 2378/2378。
- 物理 arity 拆分后 `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release -nologo -v minimal --no-restore`：成功；仅保留既有 `SqlLambdaQuery.Select(bool)` 的 RS0027 警告。
- XML 合同注释同步后同一 `Bing.Data.Sql` 构建仍成功；仅保留既有 `SqlLambdaQuery.Select(bool)` 的 RS0027 警告。
- XML 合同注释同步后 `SqlQueryApiContractTest` 双 TFM 仍为 66/66 通过。
- `ISqlQuery.From` 来源泛型注释同步后 `Bing.Data.Sql` 构建仍成功；仍仅有既有 `SqlLambdaQuery.Select(bool)` 的 RS0027 警告。
- 本记录写入时尚未执行 `task-finish`；Round 1 收口阶段已按工作流执行。全程未执行 `git add`、`git commit`、`git push`、reset、clean 或创建 PR。

## Round 1 Review Fix 执行记录

本节是 Executor 对 `review.md` 中 MUST_FIX 的修复证据；Reviewer 文件保持原样，以下状态不表示 Reviewer 已通过。

### FIX-001：可重复 arity 生成与职责归属

- 已新增 UTF-8 生成器 `tools/SqlLambdaQueryCodegen/GenerateSqlMultiLambdaQuery.py`，统一生成 `SqlMultiLambdaQuery.Arity02.cs` 至 `SqlMultiLambdaQuery.Arity10.cs`。
- 生成器使用正式 `TThird`～`TTenth` 泛型命名；10 元数只生成当前类型，不生成第 11 个 `Join`/`CrossJoin`；公共组合核心保留在 `SqlMultiLambdaQuery.cs`。
- 命令：`python .\tools\SqlLambdaQueryCodegen\GenerateSqlMultiLambdaQuery.py`，第一次退出码 `0`；同一命令第二次退出码 `0`。
- 验证：第二次生成后 9 个输出文件 SHA256 与第一次生成完全一致；源码搜索生成文件无 `TNext`，10 元文件无下一元 `Join`/`CrossJoin`。第二次生成无漂移。
- 构建：`dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release -nologo -v minimal --no-restore`，退出码 `0`；仅有既有 `SqlLambdaQuery.Select(bool)` 的 `RS0027`。

### FIX-002：1～10 元数 Unit SQL/参数矩阵

- `Bing.Dapper.Sqlite.Tests.Metadata.SqlQueryDescriptionTest` 已加入 2～10 连续 Join 的独立 Unit 方法；每个方法断言完整 SQL、参数名称、参数值顺序、来源位置和具体 `SqlLambdaQuery<...>` arity，10 元数验证无第 11 个入口。
- 保留 SQLite Integration 的真实 2～10 连续物化测试，Unit 与 Integration 分别承担 SQL 快照和数据库执行职责。
- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo --no-restore`，退出码 `0`，双 TFM 总计 `2380/2380`，失败 `0`，跳过 `0`。
- `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo --no-restore`，退出码 `0`，双 TFM 总计 `218/218`，失败 `0`，跳过 `0`。
- `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo --no-restore`，退出码 `0`，双 TFM 总计 `266/266`，失败 `0`，跳过 `0`。

### FIX-003：实体 Join 投影冻结/别名失败原子性

- `JoinClause.Join<TEntity>` 调整为先完成投影 alias 冻结，再注册真实 alias，最后提交 Operation、参数和 Join；失败路径使用 alias lifecycle 释放已注册候选。冻结点为 `internal virtual`，仅供 friend test 故障注入，不扩大公共 API。
- 新增 `TypedJoin_WhenProjectionAliasFreezeFails_ShouldKeepAllStateUnchangedAndAllowRetry`，比较失败前后 SQL、参数、OperationKind、来源数量，并在关闭故障后验证合法 Join 可重试；既有重复 alias 和 self-join 投影冻结测试继续保留。
- SQL Core 全量 `2380/2380` 双 TFM 通过；SQLite Unit `218/218`、SQLite Integration `266/266` 通过；MySQL `354/354`、PostgreSQL `268/268`、SQL Server `550/550`、Oracle `180/180` 双 TFM Provider Unit 均通过，所有命令退出码 `0`。
- 外部数据库 Integration 未运行，Gate 状态为 `NOT_RUN_EXTERNAL_GATE_MISSING`；未使用生产连接信息。

### FIX-004：Root/Join 同环境 FormalHost 基线

- Root 参数已改为 `RootCount=1,2,5,10`；Join 保持 `JoinCount=1,2,5,10`。
- 两个 Benchmark 类均使用 BenchmarkDotNet `0.14.0`、`FormalHost`（3 launch、6 warmup、15 iteration）、`MedianColumn`、Baseline/Ratio 配置和 `MemoryDiagnoser`。新增 `SqlLambdaBenchmarkColumns.cs` 的 `Gen2` 列，直接读取真实 `GcStats.Gen2Collections`，零值显示为 `0`，不改变 workload。
- `dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release -nologo -v minimal --no-restore`，退出码 `0`。
- Root 正式运行命令 `dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambdaRootBenchmarks*"`，退出码 `0`；Join 正式运行同命令替换为 `*SqlLambdaJoinBenchmarks*`，退出码 `0`。
- 环境：Windows 10 `10.0.19045.6466/22H2`，Intel Core Ultra 7 270K Plus，.NET SDK `10.0.300`，Runtime `.NET 8.0.27`，X64 RyuJIT AVX2，BenchmarkDotNet `0.14.0`。
- 可复核产物：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report-github.md`、`Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`、`Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report-github.md`、`Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report.csv`。
- 四份报告均包含 `Mean`、`Median`、`Allocated`、`Gen0`、`Gen1`、`Gen2`、`Ratio`；Root 与 Join 均存在 `1/2/5/10` 结果。FormalHost 代表趋势：Root `SetRootsAndRender` Mean 从 `2.379 us` 增至 `10.959 us`、Allocated 从 `8.18 KB` 增至 `36.29 KB`；Join `BuildJoinAndRender` Mean 从 `2.219 us` 增至 `36.737 us`、Allocated 从 `10.37 KB` 增至 `129.83 KB`。结果仅作为同机基线，未据单次数据添加缓存、对象池或生产热路径优化。

### FIX-005：公共 API 治理文档

- `ai_docs/sql-public-api-governance.md` 已加入 Lambda 来源泛型/显式 `TResult` 终结、Raw 创建入口固定结果类型、删除 `<TNextResult>`、Runtime 内部化与保留 SPI、Breaking Change 迁移、Shipped/Unshipped 规则和直接测试证据。
- 通过全文反查确认文档包含 `SqlMultiLambdaQuery`、Runtime Bridge、`PublicAPI.Unshipped.txt` 和 Dapper Core/API Contract/Analyzer 测试证据；未新增兼容 API。

### FIX-006：生产符号到测试方法追溯

- `ai_docs/sql-metadata-test-traceability.md` 顶部已重建 V4 当前唯一映射，覆盖 API 形状、1～10 arity、显式结果终结、Join 原子性、Provider 能力、SQLite 真实执行和 Root/Join Benchmark artifact。
- 原有大表已明确标记为“既有 SQL Metadata 追溯（非 V4 当前 Lambda 合同）”；旧 `SqlMultiLambdaQuery<TResult>`、旧测试名和旧结果类型语义只保留在历史迁移说明，不再属于当前映射。
- 当前映射中的生成器、API Contract、Join Atomic Failure、SQLite Unit/Integration 方法均已通过源码搜索或对应测试命令验证；发现并修正了一个不存在的旧 Unit 方法名。

### FIX-007：执行审计字段

- Round 1 全解决方案记录：`dotnet build .\Bing.All.sln -c Release -nologo -v minimal --no-restore`，退出码 `0`，`87 warnings / 0 errors`；该数字为当时快照，不是当前最终审计值。
- 当前 warning 分类：既有 `NETSDK1138`（net6.0 EOL）、依赖包 net6.0 TFM 支持提示、既有 `CS0108`/`CS0114` 成员隐藏、既有 `CS0618` 过时 API、Provider 测试的 `CS8632` nullable 注释上下文提示，以及 `Bing.Data.Sql` 既有 `RS0027`；未出现新增 `RS0016`/`RS0017`。警告未通过 NoWarn 或修改 Shipped 基线隐藏。
- 定向命令及退出码：SQL Core `0`、Analyzer `0`、Dapper Core `0`、SQLite Unit `0`、SQLite Integration `0`、MySQL Unit `0`、PostgreSQL Unit `0`、SQL Server Unit `0`、Oracle Unit `0`、Benchmark Build `0`、Root Benchmark `0`、Join Benchmark `0`、生成器两次运行 `0`。
- 外部 Gate：MySQL、PostgreSQL、SQL Server、Oracle 真实数据库测试均为 `NOT_RUN_EXTERNAL_GATE_MISSING`；SQLite 使用本地临时数据库完成，未使用生产数据库。
- `git status --short` 当前已跟踪修改 31 个文件，另有新增任务产物、V4 文档、2～10 arity 生成文件、Join Benchmark、Gen2 列和 `tools/SqlLambdaQueryCodegen`；未执行 `git add`、commit、push、reset、clean 或 PR 操作。

### FIX-008：差异格式

- `git diff --check` 退出码 `0`。Git 输出的 CRLF->LF 信息仅为换行提示，不是 whitespace error；`SqlMultiLambdaQuery.cs` EOF 多余空白行已删除。

## Round 1 收口状态

- Review 状态：`NEEDS_FIX`；Review 文件：`ai_docs/tasks/sql-lambda-query-api-v4/review.md`。
- FIX-001：执行状态 `COMPLETED`；根因是 arity 实现缺少单一生成输入和稳定输出；已建立生成器、9 个产物和二次 SHA256 稳定性证据。
- FIX-002：执行状态 `COMPLETED`；根因是连续 Join 只有 Integration 证据；已补齐 2～10 元逐元数 SQL/参数/arity Unit 矩阵。
- FIX-003：执行状态 `COMPLETED`；根因是投影冻结/alias 注册不在同一候选提交边界；已调整提交顺序并加入冻结失败可重试故障注入。
- FIX-004：执行状态 `COMPLETED`；根因是 Root 场景和 BDN 统计列不足；已完成 Root/Join 同环境 `1,2,5,10` FormalHost artifact，并显式输出 Gen2。
- FIX-005：执行状态 `COMPLETED`；根因是治理文档未同步 V4 API/Runtime 决策；已补齐公共合同、Runtime SPI 和迁移规则。
- FIX-006：执行状态 `COMPLETED`；根因是旧 V4 映射与当前映射混在同一层级；已建立顶部当前映射并隔离历史段落。
- FIX-007：执行状态 `COMPLETED`；根因是旧执行报告缺少 Round 1 逐项审计；已追加命令、退出码、测试数、警告分类、Git 状态、artifact 和外部 Gate。
- FIX-008：执行状态 `COMPLETED`；根因是 `SqlMultiLambdaQuery.cs` EOF 多余空白行；`git diff --check` 已返回 `0`。
- MUST_FIX：`8`；已完成：`8`；PARTIAL：`0`；BLOCKED：`0`；FAILED：`0`。
- 回归验证：Round 1 汇总中的全解构建为 `87 warnings / 0 errors`，当前最新结果见 Round 2；SQL Core `2380/2380`、Dapper Core `262/262`、Analyzer `19/19`、SQLite Unit `218/218`、SQLite Integration `266/266`、MySQL `354/354`、PostgreSQL `268/268`、SQL Server `550/550`、Oracle `180/180`；全部失败 `0`、跳过 `0`。
- 下一步：由独立 Reviewer 重新验收；Executor 不修改 `review.md` 的结论。
- Executor 已写入 `COMPLETED` 终态；该状态仅表示本轮修复完成，不代表 Reviewer 已通过。
- Reviewer 的 `review.md` 未修改，独立验收结论仍由下一轮 Reviewer 决定。

## Round 2 Review 修复记录

本节记录 Round 2 `NEEDS_FIX` 中两个 `MUST_FIX` 的 Executor 修复证据。`review.md` 保持 Reviewer 原始结论，不由本记录改变。

### FIX-009：类型化实体 Join 全状态原子提交与回滚

- 处理要求：`MUST_FIX`。
- 执行状态：`COMPLETED`。
- 根因：类型化实体 Join 的投影、实体别名、Operation、参数和 Join 列表原先在真实 Builder 上分步修改，后置阶段异常时缺少统一状态恢复。
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/SelectClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/EntityAliasRegister.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Params/ParameterManager.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Params/ParameterLimitManagerBase.cs`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`
- 修复：类型化实体 Join 先在 Select 和 EntityAliasRegister 候选副本上完成投影冻结、别名注册和完整谓词解析，成功后提交候选 Select/别名状态；真实提交链的 Select、Alias、Operation、参数管理器和 Join 项均位于统一 try/catch 边界。失败时恢复 Select 列及投影计数、别名注册顺序/集合/FromType、参数值和增强元数据及参数序号、Operation，并移除可能已加入的 Join 项；外部持有的原 JoinClause 引用保持有效。
- 测试：新增/调整以下故障注入用例，均使用已有 `Sample` 投影和同实体 Join，并逐项比较完整 SQL、参数名称/值/顺序、增强参数元数据、Alias 映射、Operation、类型化来源数量及 Select 状态，关闭故障后使用同一 JoinClause 重试：
	- `TypedJoin_WhenProjectionAliasFreezeFails_ShouldKeepAllStateUnchangedAndAllowRetry`
	- `TypedJoin_WhenAliasRegisterCommitFails_ShouldKeepAllStateUnchangedAndAllowRetry`
	- `TypedJoin_WhenFinalJoinCommitFails_ShouldKeepAllStateUnchangedAndAllowRetry`
- 验证：
	- SQL Core Unit 双 TFM：`2384/2384`，失败 `0`，跳过 `0`。
	- SQLite Unit 双 TFM：`218/218`，失败 `0`，跳过 `0`。
	- SQLite Integration 双 TFM：`266/266`，失败 `0`，跳过 `0`。
	- Analyzer：`19/19`，失败 `0`，跳过 `0`。
	- Dapper Core 双 TFM：`262/262`，失败 `0`，跳过 `0`。
	- SQLite Provider 拒绝路径和相关 Provider 回归未见新增失败；外部 MySQL/PostgreSQL/SQL Server/Oracle Gate 仍为 `NOT_RUN_EXTERNAL_GATE_MISSING`。

### FIX-010：执行审计数据与当前工作区同步

- 处理要求：`MUST_FIX`。
- 执行状态：`COMPLETED`。
- 根因：Round 1 execution 保留了当时的 `87 warnings` 快照，但未追加后续全解构建结果和具体 diff 统计，导致最终审计值落后于当前工作区。
- 修复：保留 `87 warnings / 0 errors` 作为 Round 1 历史证据，并明确当前值以本 Round 2 记录为准；追加最新构建、Git 状态和 diff 统计，不修改 Reviewer 的 `review.md`。
- 最新验证：
	- `dotnet build .\Bing.All.sln -c Release -nologo -v minimal --no-restore`：退出码 `0`，`101 warnings / 0 errors`。
	- 当前警告分类仍为既有 `NETSDK1138`、依赖包 net6.0 TFM 支持提示、`CS0108`/`CS0114`、`CS0618`、Provider 测试 `CS8632` 和既有 `RS0027`；本轮修复补齐 XML 参数注释后，之前的 `CS1573` 不再出现；未通过 NoWarn 或修改 Shipped 基线隐藏警告。
	- `git diff --stat`：当前已跟踪差异为 `35 files changed, 1566 insertions(+), 1980 deletions(-)`。Round 2 Reviewer 复审时的前置快照 `1236 insertions(+), 1976 deletions(-)` 保留为审计证据；本轮新增 Join 原子提交/回滚代码和测试后以当前统计为准。
	- `git status --short`：`35` 个已跟踪文件修改；任务文档、生成器、arity 产物和 Benchmark 文件以未跟踪目录/文件存在，均属于本任务工作区产物；未执行 add、commit、push、reset、clean 或 PR。
	- `git diff --check`：退出码 `0`。

### Round 2 汇总

- MUST_FIX：`2`。
- 已完成：`FIX-009`、`FIX-010`。
- PARTIAL：`0`。
- BLOCKED：`0`。
- FAILED：`0`。
- 回归验证：SQL Core `2384/2384`、SQLite Unit `218/218`、SQLite Integration `266/266`、Analyzer `19/19`、Dapper Core `262/262` 全部通过；全解构建 `101 warnings / 0 errors`；`git diff --check` 通过。
- 外部 Gate：MySQL、PostgreSQL、SQL Server、Oracle 真实数据库测试未运行，保持 `NOT_RUN_EXTERNAL_GATE_MISSING`。
- 下一步：执行 `task-finish` 后交还独立 Reviewer，再次复审 `FIX-009`、`FIX-010`；本 Executor `COMPLETED` 仅表示本轮修复已完成，不代表 Reviewer 已通过。

## Round 3 Review Fix 执行记录

本节记录 Round 3 `NEEDS_FIX` 中 `FIX-011` 的 Executor 修复证据。`review.md` 保持 Reviewer 原始结论，不由本记录改变。

### FIX-011：公开自定义参数管理器的 Join 参数提交原子性

- 处理要求：`MUST_FIX`。
- 执行状态：`COMPLETED`。
- Review 状态：`NEEDS_FIX`；Review 文件：`ai_docs/tasks/sql-lambda-query-api-v4/review.md`。
- 根因：类型化实体 Join 的参数解析和合并依赖真实 `IParameterManager` 的逐项 `Add()`。公开自定义实现可能在部分参数写入后抛错，而原恢复逻辑只覆盖内置参数管理器和参数上限包装器，无法统一恢复第三方实例。
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlClauseContext.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`
- 修复：
	- 在 `SqlClauseContext` 和 `SqlBuilderBase` 之间引入内部共享 `ParameterManagerState`，使已创建的 Clause 始终读取 Builder 当前参数管理器引用。
	- 类型化实体 Join 在候选参数管理器上解析完整 Lambda 谓词；成功后由 `SqlBuilderBase.ReplaceParameterManager()` 一次替换 Builder 和共享上下文的参数状态，不向真实第三方管理器执行逐项合并。
	- 失败时恢复调用前参数管理器引用，同时恢复 Select、Alias、Operation 和 Join 状态；候选管理器被丢弃，避免第三方参数管理器留下部分写入。
	- 保留 Clone、延迟创建 Clause、Mutation 上下文和现有参数上限包装器的状态绑定；增加可注入提交失败边界以直接验证提交后恢复。
- 测试覆盖：
	- `TypedJoin_WhenCustomParameterProbeFails_ShouldKeepStateUnchangedAndAllowRetry`：普通 `IParameterManager` 在第二个候选参数写入时确定性失败，验证完整 SQL、参数、Operation、来源图和 Join 状态未变，随后从原参数序号重试成功。
	- `TypedJoin_WhenCustomAdvancedParameterProbeFails_ShouldKeepMetadataStateUnchangedAndAllowRetry`：`IAdvancedParameterManager` 在第二个候选参数写入时失败，验证 SQL 参数和增强元数据无污染，重试后 Builder 元数据完整且原始注入管理器仍为空。
	- `TypedJoin_WhenCustomParameterCommitFails_ShouldRestoreStateAndAllowRetry`：候选参数状态已进入提交边界后注入失败，验证恢复旧管理器引用并从 `@_p_0` 开始重试。
- 验证：
	- 自定义参数专项过滤测试：双 TFM `6/6`，失败 `0`，跳过 `0`。
	- SQL Core：双 TFM `2390/2390`，失败 `0`，跳过 `0`。
	- Custom Provider：双 TFM `38/38`，失败 `0`，跳过 `0`。
	- SQLite Unit：双 TFM `218/218`，失败 `0`，跳过 `0`。
	- SQLite Integration：双 TFM `266/266`，失败 `0`，跳过 `0`。
	- Analyzer：`19/19`，失败 `0`，跳过 `0`。
	- Dapper Core：双 TFM `262/262`，失败 `0`，跳过 `0`。
	- MySQL Provider Unit：双 TFM `354/354`，失败 `0`，跳过 `0`。
	- PostgreSQL Provider Unit：双 TFM `268/268`，失败 `0`，跳过 `0`。
	- SQL Server Provider Unit：双 TFM `550/550`，失败 `0`，跳过 `0`。
	- Oracle Provider Unit：双 TFM `180/180`，失败 `0`，跳过 `0`。
	- 全解构建：`dotnet build .\Bing.All.sln -c Release -nologo -v minimal --no-restore`，退出码 `0`，`87 warnings / 0 errors`；警告为既有 TFM 支持、成员隐藏、过时 API、nullable 注释和既有 Analyzer 警告类别，未关闭 Analyzer 或修改 Shipped 基线。
	- 差异格式：`git diff --check` 退出码 `0`；输出中的 CRLF/LF 提示为 Git 换行提示，不是 whitespace error。
	- 当前已跟踪差异：`git diff --stat` 为 `36 files changed, 1948 insertions(+), 1985 deletions(-)`；另有任务文档、生成产物、Benchmark 和工具目录等未跟踪任务产物，未执行 `git add`、commit、push、reset、clean 或 PR。
	- 外部 Gate：MySQL、PostgreSQL、SQL Server、Oracle 真实数据库 Integration 均为 `NOT_RUN_EXTERNAL_GATE_MISSING`，未使用生产数据库连接。

### Round 3 汇总

- MUST_FIX：`FIX-011`。
- 已完成：`FIX-011`。
- PARTIAL：`0`。
- BLOCKED：`0`。
- FAILED：`0`。
- 回归验证：专项测试、SQL Core、Custom Provider、SQLite Unit/Integration、Analyzer、Dapper Core、四个 Provider Unit 和全解构建全部通过；`git diff --check` 通过。
- 下一步：已执行 `task-finish`，交还独立 Reviewer 再次验收 `FIX-011`；本 Executor `COMPLETED` 仅表示当前 Review Fix 要求已完成，不代表 Reviewer 已通过。

## Round 4 Review Fix 执行记录

本节记录 Round 4 `NEEDS_FIX` 中唯一 `MUST_FIX`：`FIX-012`。`review.md` 保持 Reviewer 原始结论，不由本记录改变。

### FIX-012：类型化派生表 Join 的第三方参数管理器原子提交

- 处理要求：`MUST_FIX`，严重程度：HIGH。
- 执行状态：`COMPLETED`。
- 根因：类型化派生表 Join 原先先通过 `RenderSubquery()` 向真实参数管理器合并子查询参数，再通过 `FromClause.MergeNewParameters()` 逐项合并 On 谓词参数；失败补偿依赖真实第三方管理器的 `Clear/Add` 回放，无法保证部分写入、参数序号和增强元数据恢复。
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
	- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`
- 修复：
	- `SqlBuilderBase` 增加指定候选参数管理器和候选子查询参数重命名映射的内部渲染入口；既有普通子查询、CTE 和 Union 渲染入口仍使用 Builder 当前状态。
	- 类型化派生表 Join 从调用前参数管理器克隆候选状态，并深复制 `_subqueryParameterNames`；子查询快照渲染、参数冲突重命名和多来源 On 谓词解析全部写入候选状态。
	- 成功后以 `ReplaceParameterManager()` 一次替换共享参数状态，并一次替换子查询重命名映射，然后提交 Operation、Join 项和别名；失败时丢弃候选，恢复旧参数管理器引用、映射、别名和 Operation，不调用真实第三方管理器的 `Clear/Add`。
	- 派生表 Join 的 Join 项提交标记延后到别名注册完成，别名注册异常时移除可能已加入的 Join 项，保持 JoinCount 和来源图不变。
- 测试覆盖：
	- `TypedSubqueryJoin_WhenCustomParameterRenderFails_ShouldKeepStateUnchangedAndAllowRetry`：普通 `IParameterManager`，子查询包含两个参数并带 On 常量参数；第一次候选写入失败时验证 SQL、参数、Alias、Operation、来源图、JoinCount 和重命名状态未变，关闭故障后从 `@_p_1` 重试成功。
	- `TypedSubqueryJoin_WhenCustomAdvancedParameterRenderFails_ShouldKeepMetadataStateUnchangedAndAllowRetry`：`IAdvancedParameterManager` 等价路径，额外验证增强参数元数据未污染，重试后四项元数据完整。
	- 两条测试使用同一 Builder/JoinClause、已有 `@_p_0`，并在 `net6.0`/`net8.0` 双 TFM 通过。
- 验证：
	- 派生表第三方参数专项：`4/4`，失败 `0`，跳过 `0`。
	- SQL Core：`2394/2394`，失败 `0`，跳过 `0`。
	- Custom Provider：`38/38`，失败 `0`，跳过 `0`。
	- SQLite Unit：`218/218`，失败 `0`，跳过 `0`；SQLite Integration：`266/266`，失败 `0`，跳过 `0`。
	- Analyzer：`19/19`，失败 `0`，跳过 `0`；Dapper Core：`262/262`，失败 `0`，跳过 `0`。
	- MySQL Unit：`354/354`；PostgreSQL Unit：`268/268`；SQL Server Unit：`550/550`；Oracle Unit：`180/180`；均失败 `0`、跳过 `0`。
	- `dotnet build Bing.All.sln -c Release --no-restore`：退出码 `0`，`133 warnings / 0 errors`。警告为既有 net6.0 EOL、依赖包 TFM 支持、成员隐藏、过时 API、Provider nullable 注释、SQLite RID 和既有 `RS0027` 等，未通过 NoWarn 或 Shipped API 基线隐藏。
	- `git diff --check`：退出码 `0`；CRLF/LF 输出仅为 Git 换行提示。
	- `git diff --stat`：`36 files changed, 2143 insertions(+), 2002 deletions(-)`；未跟踪的任务产物、arity 生成文件、Benchmark 和工具不计入该统计。
	- MySQL、PostgreSQL、SQL Server、Oracle 真实数据库 Gate 继续记录为 `NOT_RUN_EXTERNAL_GATE_MISSING`，未伪造外部服务通过。

### Round 4 汇总

- MUST_FIX：`FIX-012`。
- 已完成：`FIX-012`。
- PARTIAL：`0`。
- BLOCKED：`0`。
- FAILED：`0`。
- `review.md` 未修改；本 execution `COMPLETED` 仅表示 Executor 已完成修复，不表示 Reviewer 已通过。
- 下一步：执行 `task-finish` 后交还独立 Reviewer，再次验收 `FIX-012`。
