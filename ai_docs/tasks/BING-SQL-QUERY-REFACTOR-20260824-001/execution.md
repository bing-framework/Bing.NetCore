<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-QUERY-REFACTOR-20260824-001
AI_EXECUTION_FINISHED_AT: 2026-08-24T16:30:27.7212198+08:00

# 实施执行报告

## 执行结论

核心 API 收敛、正确性修复、SQLite/Provider Unit 回归、Roslyn 契约、Runtime SPI 审计、文档和 Benchmark 真实性治理已完成并通过验证。由于完整大文件物理拆分和基于正式前后基线的性能优化尚未实施，本任务以 `PARTIAL` 终态收口；未将短基准结果解释为性能收益。

## 任务信息

- Task ID：`BING-SQL-QUERY-REFACTOR-20260824-001`
- 执行器：Copilot，source `copilot`
- 计划：[plan.md](plan.md)
- 执行开始：2026-08-24T03:10:31.668Z
- 执行开始时仅有本任务计划目录的未跟踪产物；当前源码、测试、Benchmark、文档和本任务报告修改均保留在工作区供用户审查。
- 未执行自动 `git add`、`git commit`、`git push`、PR、Tag、Release、stash、reset、clean 或 restore。

## 计划执行情况

| Phase | 状态 | 结果 |
| --- | --- | --- |
| Phase 0：基线与证据确认 | COMPLETED | 已完成环境、Git、核心测试、Benchmark Dry、消费者/符号追溯和外部 Provider Gate 核对。 |
| Phase 1：正确性、隔离与诊断 | COMPLETED | P1-T01/P1-T02/P1-T03 均已完成定向修复与 Unit/SQLite 验证。 |
| Phase 2：API 收敛与 Breaking Change | COMPLETED | P2-T01～P2-T04 已完成；非泛型 From/Query/Sql/Procedure 主路径、显式来源定位、条件优先 WhereIf 和高层重复终结 API 均已收敛。 |
| Phase 3：运行时边界与目录重构 | PARTIAL | P3-T01 已完成；Lambda 终结方法已移至 partial 文件，SqlQuery/Runtime Bridge/Builder 等其余大文件尚未完整物理拆分。 |
| Phase 4：测试体系补齐 | PARTIAL | API Roslyn 契约、1～10 From/2～10 Join SQLite 矩阵、规模参数 Unit 已完成；外部 Provider Integration 因 Gate 缺失未运行，共享契约未新增第二套基建。 |
| Phase 5：Benchmark 与性能优化 | PARTIAL | Benchmark 初始化边界、真实 DataFilter、公开 Clone、ShortRun 和 10/100/1000 参数观察已完成；未基于同机前后基线实施热点优化。 |
| Phase 6：注释、文档与发布准备 | COMPLETED | 本轮修改范围的中文 XML 注释、Public API、设计/治理/迁移/追溯文档、Release Notes 和最终回归已同步。 |

## Phase 0 基线

### 环境

- OS：Windows 10.0.19045，RID `win-x64`
- .NET SDK：10.0.300；MSBuild 18.6.3
- 运行时：.NET 6.0.36、8.0.27 已安装
- global.json：不存在
- BenchmarkDotNet：0.14.0
- 测试：xUnit VSTest Adapter 2.5.7

### Git

- `git status --short`：仅 `?? ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/`
- `git diff --stat`：无已跟踪差异
- 计划目录是本任务自身产物，不视为用户既有修改。

### 基线测试

| 命令 | 结果 |
| --- | --- |
| `dotnet test .\\framework\\tests\\Bing.Data.Sql.Tests\\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal` | PASS，2392/2392，net6/net8；保留 36 个 Data.Sql 警告，主要为 Legacy 隐藏成员与 RS0026/RS0027。 |
| `dotnet test .\\framework\\tests\\Bing.Dapper.Core.Tests\\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal` | PASS，262/262，net6/net8。 |
| `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests\\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal` | PASS，222/222，net6/net8；net8 保留 NETSDK1206。 |
| `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests.Integration\\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal` | PASS，284/284，net6/net8；net8 保留 NETSDK1206。 |
| `dotnet run -c Release --project .\\framework\\tests\\Bing.Data.Sql.Benchmarks\\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*" --job Dry` | PASS，可运行；Dry 为单次冷启动迭代，不作为性能收益结论。现有 Root 基线约 32.97 KB/1 表、94.34 KB/10 表，包含 `DispatchProxy.Create` 和查询重建成本。 |

### Phase 0 已确认事实

- 非泛型 `SqlLambdaQuery`、连续 `From`、生命周期、Clone、实例级缓存、动态过滤旁路、QueryContext/Execution 诊断和非泛型 Raw 主路径均已有真实实现。
- `ISqlQuery` 仍同时公开两参数非泛型 `From` 与一参数泛型兼容 `From`；`SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery` 和 Legacy 转发仍存在。
- `Query<TResult>`、`Sql<TResult>`、`SqlInterpolated<TResult>`、`Procedure<TResult>` 仍公开；当前 `WhereIf` 不是条件优先；条件组嵌套默认按 AND；`ToDictionary` 先物化 List。
- `IsExecutionContextRequired()` 仍以 Logger 是否为 NullLogger 单例判断，未基于实际日志级别判断。
- Root/Join Benchmark 的 `DynamicFilterRender` 不是动态 IDataFilter 场景，Clone 测量的是 Builder Clone，Benchmark Dry 不能证明优化收益。
- 外部 Provider 集成是否可运行仍需根据实际 Gate 逐项确认，不猜测连接信息。

### Phase 0 消费者与发布矩阵

| 符号/入口 | 生产消费者 | 测试/基线消费者 | 文档/生成物 | 当前结论 |
| --- | --- | --- | --- | --- |
| `ISqlQuery.From<TEntity>(string alias = null)` | `Bing.Dapper.Core.SqlQueryBase` 实现 | `SqlQueryApiContractTest` 明确断言兼容入口；Provider 测试仍有一元调用 | `PublicAPI.Shipped.txt`、设计/治理/使用文档仍记录 | 本次 Breaking Change 目标是删除，不可继续按 Shipped 兼容保留。 |
| `SqlLambdaQuery<TEntity>` | `SqlLambdaQuery.Legacy.cs`、`SqlQueryRuntimeFactory.CreateLambdaQuery<TEntity>` | API Contract、条件组测试和部分历史追溯使用 | `PublicAPI.Shipped/Unshipped.txt`、XML 生成物 | 需删除 Legacy 类型和泛型工厂，测试改为非泛型主路径。 |
| `SqlMultiLambdaQuery` | `SqlLambdaQuery.Legacy.cs` 继承桥接 | Public API Unshipped 和 XML 生成物记录 | 历史 V4/v6 追溯中保留 | 仅作为历史证据，最终程序集不得导出。 |
| `Query<TResult>` | `SqlQueryBase` | SQLite Unit/Integration、SQL Server 测试大量使用 | usage 文档/追溯 | 按 P2-T02 审计；当前仍是早期结果固定入口。 |
| `Sql<TResult>` / `SqlInterpolated<TResult>` / `Procedure<TResult>` | `SqlQueryBase` | Provider 测试和过程测试大量使用 | Shipped/Unshipped 与 usage 文档 | 按 P2-T02 收敛到非泛型描述和终结类型。 |
| `SetRoots` | `FromClause` internal 实现 | 未发现当前测试直接调用；历史追溯记录 1～10 来源测试 | XML 生成物 | 不是当前公开调用点，最终 API 测试不得依赖它。 |
| `SqlQueryRuntimeFactory` | Data.Sql 内部创建链 | API 基线仍含泛型 Lambda 工厂 | `PublicAPI.Unshipped.txt` | 按真实跨程序集消费者审计并最小化。 |

### 测试追溯与缺口

- `SqlQueryApiContractTest` 当前仍验证兼容泛型 `From`、`SqlLambdaQuery<>` 存在，与本任务最终 Breaking Change 目标相反；Phase 2 必须改为唯一非泛型入口和删除 API 的反向编译契约。
- `SqlQueryDescriptionTest` 已覆盖非泛型 Raw 描述、双根来源、条件组和重复渲染，但仍存在 `Query<TResult>` 和历史泛型入口混用；需迁移到最终 API 后重跑完整 SQL 断言。
- `SqliteExecutionIntegrationTest` 已真实覆盖分页、流式、取消、Clone、动态过滤和诊断；1～10 根表、2～10 Join、显式 alias、自连接最终 API 矩阵尚未完成。
- `SqlServerRoutingAndExecutionTest` 已覆盖诊断身份、错误、Activity、Logger Scope、参数脱敏、资源释放和 Provider 路由；其旧入口使用不代表最终公共 API 已收敛。
- `Bing.Data.Sql.Benchmarks` 的 Root/Join 场景仍包含 `DispatchProxy`、查询重建或非动态过滤路径，当前只作为 Dry 可运行性证据。

### 外部 Provider Gate

- 共享门控：`RUN_INTEGRATION_TESTS=true`，或 Provider 专用 `RUN_MYSQL_INTEGRATION_TESTS`、`RUN_POSTGRESQL_INTEGRATION_TESTS`、`RUN_SQLSERVER_INTEGRATION_TESTS`、`RUN_ORACLE_INTEGRATION_TESTS`。
- 连接变量：`ConnectionStrings__MySqlConnection`、`ConnectionStrings__PostgreSqlConnection`、`ConnectionStrings__SqlServerConnection`、`ConnectionStrings__OracleConnection`；缺失时才回退 `ConnectionStrings__DefaultConnection`。
- 当前会话未检测到已授权的外部测试连接配置，未运行外部数据库 Integration，状态为 `NOT_RUN_EXTERNAL_GATE_MISSING`；SQLite 不受此 Gate 影响。

## Phase 1：正确性、隔离与诊断

### P1-T03 诊断按需创建：COMPLETED

- 根因：`IsExecutionContextRequired()` 以 Logger 是否为 `NullLogger.Instance` 判断，Logger 工厂存在但实际未启用 Trace 时仍创建消息、参数诊断和 Scope。
- 修改：使用 `Logger.IsEnabled(LogLevel.Trace)`；`BeginExecutionLogScope` 在消息为空或 Trace 未启用时直接返回空释放对象。
- 直接测试：
	- `ExecuteSql_WhenTraceLoggerIsEnabled_ShouldBeginStructuredScope`
	- `ExecuteSql_WhenLoggerIsRegisteredButTraceIsDisabled_ShouldSkipDiagnosticsAndScope`
	- `ExecuteSql_WhenAllDiagnosticsAreEnabled_ShouldShareExecutionIdentityOnError`
	- 既有 Activity、Listener、同步/异步/流式和关闭诊断测试
- 验证：`Bing.Dapper.SqlServer.Tests` Release，net6.0/net8.0，564/564 PASS；保留既有 net8 `NETSDK1206` 警告。
- 影响边界：Trace 日志仍复用执行 SQL、参数脱敏和统一 Scope；仅 Listener 或 Activity 启用时创建执行消息但不创建无效 Logger Scope。

### P1-T01 mutation/version 与缓存失效：COMPLETED

- 已盘点的查询描述修改器：`From`、`FromTable`、`FromSubquery`、`Select`、`AppendSelect`、`ClearSelect`、`Distinct`、`Aggregate`、`Where`、`WhereIf`、`WhereGroup`、四类 Join、派生表 Join、`CrossJoin`、`GroupBy`、`OrderBy`、`Having`、`Skip`、`Take`、`SplitOn`。
- 当前矩阵：

| MutationKind | Touch | SQL impact | Parameter impact | 失败/空操作处理 |
| --- | --- | --- | --- | --- |
| From/FromTable/FromSubquery | 成功提交后 | From 来源或派生表变化 | 通常无；派生表合并时可能重命名 | 输入校验和候选失败在 Touch 前抛出 |
| Select/AppendSelect/ClearSelect/Distinct/Aggregate | 成功提交后 | Select 投影变化 | 聚合或表达式参数可能变化 | 解析或 Provider 能力失败在 Touch 前抛出 |
| Where/WhereIf(true) | 成功提交后 | Where 条件变化 | 比较条件、IN 和动态过滤可能新增参数 | 解析/参数校验失败不 Touch；`WhereIf(false)` 不 Touch |
| WhereGroup | 非空组成功提交后 | 嵌套 Where 变化 | 使用候选 Builder，提交时合并参数 | 空组、未知来源、参数超限均保持原状态 |
| Join/Left/Right/Full/Cross Join | 候选成功后 | Join 图和 On 条件变化 | Join 条件可能新增参数 | 候选失败不 Touch，保留原 SQL/参数 |
| GroupBy/OrderBy/Having | 成功解析后 | 分组、排序或 Having 变化 | Having 可能新增参数 | 表源/Provider/表达式失败不 Touch |
| Skip/Take | 成功调用后 | Limit/Offset 变化 | Provider 可能生成分页参数 | Builder 校验失败不 Touch |
| SplitOn | 修改后 | SQL 文本不变 | 不变；执行计划 SplitOn 改变 | 空白输入在 Touch 前抛出 |

- 生产修改：`SqlQuery.SplitOn()` 现在在成功更新 `_splitOn` 后调用 `Touch()`，避免执行计划语义变更与实例版本脱节。
- 直接测试：`SplitOn_WhenChanged_ShouldInvalidateCacheAndUpdateExecutionPlan` 验证 SQL 文本保持一致、缓存重新渲染、后续 `SqlQueryPlan.SplitOn` 使用新值；既有 WhereGroup 原子性、WhereIf、Join 失败回滚和动态过滤缓存测试继续保留。
- 验证：`Bing.Data.Sql.Tests` Release，net6.0/net8.0，`2394/2394 PASS`。
- 尚待继续：CTE/Union 等低层 Builder 直接修改入口的可见性与版本边界、参数替换/恢复路径、P1-T02 的 Clone/并发/流式资源矩阵；这些路径不能通过查询 façade 的当前 API 直接修改，需在后续 Runtime/SPI 审计中确认是否属于本描述生命周期。

### P1-T02 Clone、并发和执行资源隔离：COMPLETED

- Unit 证据：
	- `Clone_WhenSourceIsCompleted_ShouldCreateIndependentDraftWithParentContext`
	- `Clone_WhenSourceAndCloneAreMutated_ShouldKeepBothStatesIndependent`
	- `Clone_WhenExecutedConcurrently_ShouldKeepParameterSnapshotsIsolated`
	- 既有执行租约、重入拒绝、异常恢复、同步流 Dispose 释放测试
- SQLite Integration 证据：既有 `LambdaClone_WhenSourceAndCloneExecute_ShouldRemainIndependent`、Clone 动态过滤隔离、分页 Count/Data 独立执行标识、流式提前停止、同步/异步取消后重试和资源恢复测试。
- 验证：`Bing.Data.Sql.Tests` Release，net6.0/net8.0，`2402/2402 PASS`；SQLite Unit `222/222 PASS`；SQLite Integration `284/284 PASS`。
- 不变量：来源与 Clone 使用独立 Builder/参数快照；Clone 新建 QueryContextId 并记录 Parent；同一描述禁止并发重入；Count/Data 使用同一 QueryContextId 但独立 ExecutionId/Phase；异常、取消和 Dispose 后执行租约恢复。

### P2-T01 删除泛型 Lambda 兼容路径并统一 From：COMPLETED

- 生产变更：
	- `ISqlQuery` 仅保留 `From<TEntity>(string alias = null, string schema = null) -> SqlLambdaQuery`。
	- `SqlQueryBase` 统一实现同一入口，并保留默认实体投影行为。
	- 删除 `SqlLambdaQuery<TEntity>`、`SqlMultiLambdaQuery` Legacy 文件和 `SqlQueryRuntimeFactory.CreateLambdaQuery<TEntity>`。
	- 删除非泛型 façade 中已无消费者的 `LegacySelect`、`LegacyAppendSelect`、`LegacyGroupBy`、`LegacyOrderBy` 转发。
- 契约变更：`SqlQueryApiContractTest` 改为断言唯一非泛型 From、默认参数和旧泛型类型不导出；Public API Shipped/Unshipped 与 Dapper Core 基线同步迁移。
- 消费者验证：SQLite Unit `222/222 PASS`，SQLite Integration `284/284 PASS`，Dapper Core Unit `262/262 PASS`，SQL Server Provider Unit `564/564 PASS`，Data.Sql Unit `2402/2402 PASS`。
- 保留风险：`WhereIf` 参数顺序、显式右侧 alias、起始阶段泛型 Query/Raw/Procedure 和终结 API 仍待后续 Phase 2 任务处理；当前未运行外部数据库 Gate。

## 历史执行转折点

本节原先记录进入 Phase 2 P2-T02 的中间状态；P2-T02 及后续 API 收敛已在后续记录中完成。最终阶段状态、验证结果和遗留项以文末“最终收口记录”为准。当前未执行自动 `git add`、`git commit`、`git push`、PR 或 destructive Git 操作。

## 继续执行记录：显式来源与最终 API 契约

### P2-T03 显式来源定位：PARTIAL -> 核心缺口已修复

- 修复 `SqlLambdaQuery.Where<TEntity, TValue>(column, value, alias, operator)`：不再直接调用按实体类型解析的 `WhereClause`，而是通过指定 `TableSource` 生成带实际来源 alias 的列 SQL，再提交参数条件。
- 增加 `SqlLambdaQuery.Aggregate<TEntity>(function, column, alias, columnAlias, distinct)`；聚合列通过 `ColumnCollection` 的显式 alias 路径写入，避免同类型多来源时退化为最后注册的实体 alias。
- 新增/更新完整 SQL 测试：
	- `Lambda_WhenSameEntitySourcesUseExplicitAliases_ShouldBindStableSources`
	- `Lambda_WhenTwoSameEntitySourcesUseExplicitAliases_ShouldBindExpressionParameters`
	- `Lambda_WhenAggregateUsesExplicitAlias_ShouldBindSelectedSource`
	- `Lambda_WhenExplicitAliasIsMissing_ShouldKeepQueryStateUnchanged`
- 仍保留唯一来源时的参数名便捷推断；同类型多来源的显式 alias 解析失败不写入 SQL、参数或 ShapeVersion。

### P2-T04 终结 API审计：COMPLETED（高层路径）

- 高层非泛型 Lambda、Fluent、Raw Text 和 Procedure 描述不再导出 `SingleOrDefault` 或 `ToDictionary`。
- `ToEntity<TResult>` 表达 0/1/多行唯一语义；字典结果由 `ToList<TResult>()` 后的 LINQ `ToDictionary` 明确完成。
- `SqlLambdaQueryCore`、`SqlQuery` 和 `SqlQueryPlanExecutor` 中仍存在的同名成员属于内部/Advanced 执行链，不是普通非泛型 façade 入口；未将低层执行 SPI 与高层推荐 API 混合。
- `SqlQueryApiContractTest.QueryDescriptions_WhenPublicApiInspected_ShouldNotExposeDuplicateHighLevelTerminals` 锁定该边界。

### P4-T01 API Contract 与 Roslyn 编译契约：PARTIAL

- 新增 `SqlOperationCompileContractTest` 正向契约：`From<TEntity>`、`Query()`、`Sql()`、`Procedure()` 返回非泛型描述，结果类型在 `ToList<TResult>`/`ExecuteList<TResult>` 终结阶段选择。
- 新增负向契约：`Query<TResult>()`、`SqlLambdaQuery<TEntity>` 和三参数 `Where<T1,T2,T3>` 消费者不能编译。
- Analyzer 全量验证：`Bing.Data.Sql.Analyzers.Tests`，`23/23 PASS`，net8.0。
- 仍需继续补齐最终 1～10 来源及 2～10 Join 的独立 Roslyn/SQLite 消费者矩阵。

### P6-T01/P6-T02 文档与注释：PARTIAL

- 已更新：
	- `docs/sqlquery-lambda-usage.md`
	- `docs/sqlquery-usage.md`
	- `ai_docs/sql-lambda-query-design.md`
	- `ai_docs/sql-public-api-governance.md`
	- `ai_docs/sql-metadata-test-traceability.md`
	- `docs/ReleaseNotes.md`
- 文档已统一为：唯一非泛型 `From<TEntity>(alias, schema)`、非泛型 `Query/Sql/Procedure` 描述、终结方法选择结果类型、显式 alias、自连接和 Breaking Change 迁移语义；删除 `From<TEntity>(null, null)` 的推荐用法。
- 新增公开聚合重载和内部聚合 alias 支持已登记 `PublicAPI.Unshipped.txt`。
- 中文 XML 注释仍需按 `chinese-comments` Skill 对本次新增 public/protected/interface 成员逐项补齐 `<param>`、`<typeparam>`、`<returns>`、异常和生命周期说明；当前只完成部分新增成员注释。

### 本轮验证

| 命令 | 结果 |
| --- | --- |
| `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal` | PASS，`2418/2418`，net6.0/net8.0；保留既有 RS0026/RS0027 可选参数重载警告 |
| `dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal` | PASS，`23/23`，net8.0 |
| `git diff --check` | 待最终验证阶段执行 |

外部 MySQL/PostgreSQL/SQL Server/Oracle Integration 仍未运行，当前状态为 `NOT_RUN_EXTERNAL_GATE_MISSING`；未猜测连接信息、未连接生产数据库。

## 最终收口记录

### 最终 API

普通查询主路径为：

```csharp
SqlLambdaQuery From<TEntity>(string alias = null, string schema = null);
SqlFluentQuery Query();
SqlTextQuery Sql(string sql, object parameters = null);
SqlTextQuery SqlInterpolated(FormattableString sql);
SqlProcedureQuery Procedure(string procedure, object parameters = null);
```

结果类型后置到 `ToEntity<TResult>`、`ToList<TResult>`、分页、标量和同步/异步流式终结方法；Procedure 使用 `ExecuteList<TResult>` 等终结方法。高层非泛型 Lambda/Fluent/Text/Procedure 描述不再导出 `SingleOrDefault` 或 `ToDictionary`。Dapper 泛型多映射保留在隐藏的 Advanced 路径，不作为普通 IntelliSense 推荐入口。

### 主要实现与边界

- 删除 `SqlLambdaQuery.Legacy.cs`、泛型 Lambda 包装器和 Legacy Factory；程序集不再导出公开 `SqlLambdaQuery<TEntity>` 或 `SqlMultiLambdaQuery`。
- `Where`、`Select`、`AppendSelect`、`GroupBy`、`OrderBy`、`Having`、`Aggregate` 和 Join 均有显式来源定位入口；多来源默认解析按表达式参数位置，不读取 Lambda 参数名。
- `WhereIf` 条件参数前置；条件组支持明确的 `AndGroup`/`OrGroup` 语义；失败候选不 Touch，动态过滤环境旁路不稳定 SQL 缓存。
- Runtime SPI 类型使用 `EditorBrowsable(Never)`；Data.Sql 友元程序集仅为测试/Integration/Benchmark 消费者；`SqlQueryPlan` 公共属性不泄露 Builder、连接或事务。
- 仅将 Lambda 终结方法移动到 `SqlLambdaQuery.Terminals.cs`；未对所有大文件进行无行为物理拆分。

### 最终测试与构建

| 项目 | 结果 |
| --- | --- |
| `Bing.All.sln` Release build | PASS，0 error，101 warnings |
| `Bing.Data.Sql.Tests` | PASS，2428/2428，net6/net8 |
| `Bing.Data.Sql.Analyzers.Tests` | PASS，25/25，net8 |
| `Bing.Dapper.Core.Tests` | PASS，262/262，net6/net8 |
| `Bing.Dapper.Sqlite.Tests` | PASS，222/222，net6/net8 |
| `Bing.Dapper.Sqlite.Tests.Integration` | PASS，284/284，net6/net8 |
| `Bing.Dapper.MySql.Tests` | PASS，354/354，net6/net8；保留既有 CS8632 |
| `Bing.Dapper.PostgreSql.Tests` | PASS，268/268，net6/net8 |
| `Bing.Dapper.SqlServer.Tests` | PASS，564/564，net6/net8 |
| `Bing.Dapper.Oracle.Tests` | PASS，180/180，net6/net8 |
| `Bing.Data.Sql.Benchmarks` build | PASS，net8 |
| `git diff --check` | PASS；Git 仅报告 CRLF/LF 转换提示，无 whitespace error |

外部 MySQL/PostgreSQL/SQL Server/Oracle Integration 均为 `NOT_RUN_EXTERNAL_GATE_MISSING`。未猜测连接字符串、未绕过 Gate、未连接生产数据库。

### Benchmark 证据

正式 `ShortRun` 使用 .NET 8.0.27、BenchmarkDotNet 0.14.0、Windows x64，同机输出位于 `BenchmarkDotNet.Artifacts/results/`：

- Root `SetRootsAndRender`：1/2/5/10 来源约 6.39/7.47/10.76/21.06 us，约 17.6/21.0/33.0/57.6 KB 分配。
- Root `RenderExistingRoots`：1/2/5/10 来源约 112/123/136/172 ns，约 520 B 分配，证明实例缓存路径与重建路径不同。
- Join `BuildJoinAndRender`：1/2/5/10 Join 约 16.9/16.5/44.6/68.6 us，10 Join 约 200 KB 分配。
- 参数规模 `RenderInParameters`：1000 参数约 508～563 us、约 2.09～2.13 MB 分配，并出现 Gen1/Gen2 计数；该数据用于暴露规模成本，不宣称 0 GC 或性能收益。
- `DynamicFilterRender` 使用真实 `DataFilter.Disable<ISoftDelete>()`；`CloneQuery` 测量公开 `SqlLambdaQuery.Clone()`。

未执行同机旧版本对照，因此没有性能回归/收益结论。P5-T02 保留为后续独立优化任务，避免以 ShortRun 三次迭代数据驱动无证据优化。

### 注释、追溯与安全检查

- 本轮新增/修改的 public、internal、private C# 成员已补充中文 XML 注释；实现优先使用既有契约语义，不编辑 bin/obj 生成物。
- Public API Shipped/Unshipped、设计、治理、Lambda 使用、通用使用、traceability 和 Release Notes 已同步；Roslyn 正/负编译契约验证最终入口及删除入口。
- SQL 文本仍使用参数绑定；未新增字符串拼接外部输入为 SQL 值的路径。未发现本轮新增的硬编码凭据、生产连接、认证绕过、命令执行或资源未释放路径。
- 工具输出未发现 prompt injection；扫描和验证结果均来自仓库源代码、测试和构建日志。

### 未完成项与风险

- P3-T02：`SqlQuery`、`SqlBuilderRuntimeBridge`、`SqlBuilderBase`、`JoinClause` 等大文件尚未按计划完成全部物理拆分。
- P5-T02：尚未基于同机旧/新基线进行统计显著的热点优化。
- P4-T04/P6-T03：外部 Provider Integration 无授权配置，不能标记 PASS；当前状态已诚实记录为 `NOT_RUN_EXTERNAL_GATE_MISSING`。
- Public API 的 RS0026/RS0027 optional-overload 警告和仓库既有 net6 EOL/包支持/隐藏成员警告仍存在，未通过关闭 Analyzer 或删测试规避。

### Git 状态与交付边界

- 工作区修改保留给用户审查；未执行 `git add`、`git commit`、`git push`、PR、Tag、Release、stash、reset、clean 或 restore。
- 本报告终态为 `PARTIAL`，原因仅为上述明确的物理拆分和无证据性能优化遗留，不影响已完成 API 收敛和测试验证结果。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：同一实体类型存在多个已绑定来源时，部分 Lambda 操作按来源注册顺序选择来源，可能静默生成语义错误的 SQL。
- 修复：统一要求无 alias 的表达式参数对实体类型只能解析到唯一来源；二元/多来源测试消费者改为传入明确 alias；失败路径保持查询 SQL、参数、别名和 ShapeVersion 不变。
- 验证：
	- `Bing.Data.Sql.Tests`：PASS，`2478/2478`
	- `Bing.Dapper.Sqlite.Tests`：PASS，`222/222`
	- `Bing.Dapper.Sqlite.Tests.Integration`：PASS，`284/284`

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：Data.Sql Unit 缺少推荐非泛型 API 的 1～10 根来源、2～10 连续 Join 完整 SQL 矩阵，IN 规模测试只断言 SQL 片段。
- 修复：新增完整 SQL、参数名和值断言的 From/Join 矩阵；将 10/100/1000 IN 规模测试改为完整 SQL 相等断言，并同步更新生产符号到测试方法的追溯映射。
- 验证：
	- `Bing.Data.Sql.Tests`：PASS，`2478/2478`
	- `Bing.Data.Sql.Analyzers.Tests`：PASS，`25/25`
	- `git diff --check`：PASS

### Round 1 汇总

- MUST_FIX：2
- 已完成：FIX-001、FIX-002
- PARTIAL：无
- BLOCKED：无
- FAILED：无
- 回归验证：Data.Sql、SQLite Unit、SQLite Integration、Analyzer 均通过。
- SHOULD_FIX/OPTIONAL：未处理，保留给后续独立 Review 或专项任务。
- 下一步：再次进行独立 Review；本轮不自动 commit/push。

### Round 2

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- 根因：上一轮只收紧了表达式谓词、投影、分组、排序和 Having 的来源解析；值型 `Where`、值型 `WhereIf` 和默认实体 `Select` 仍直接委托 Builder。Builder 通过实体类型的 alias 注册表解析来源，重复实体注册时会覆盖为最后 alias，导致语义错误的 SQL 被静默生成。
- 修复：
	- 值型 `Where<TEntity,TValue>` 在提交条件前调用 `ResolveSource<TEntity>(null)`，并复用显式 `WhereValueCore` 生成绑定到唯一 `TableSource` 的参数条件。
	- 值型 `WhereIf<TEntity>` 在 condition 为 true 时复用同一唯一来源和显式来源条件路径；condition 为 false 时不解析来源、不写参数、不 Touch。
	- 默认实体 `Select<TEntity>` 在 Builder 投影前要求实体来源唯一，避免 `SelectClause` 的默认 alias 冻结逻辑掩盖重复来源。
	- 新增 Equal、IN、`WhereIf(true)` 和默认实体投影的重复来源负例；统一断言完整 SQL、参数为空、异常消息和 `ShapeVersion` 保持不变。
- 验证：
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Tests\\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal`：PASS，`2486/2486`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests\\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal`：PASS，`222/222`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests.Integration\\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal`：PASS，`284/284`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Analyzers.Tests\\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal`：PASS，`25/25`，net8.0。
	- `dotnet build .\\Bing.All.sln -c Release -nologo -v minimal`：PASS，0 error，105 warnings。
	- `git diff --check`：PASS；无 whitespace error。

### Round 2 汇总

- MUST_FIX：1
- 已完成：FIX-001
- PARTIAL：无
- BLOCKED：无
- FAILED：无
- 回归验证：Data.Sql Unit、SQLite Unit、SQLite Integration、Analyzer 和全方案 Release Build 均通过。
- SHOULD_FIX/OPTIONAL：未处理，保持 Reviewer 独立决定范围。
- 下一步：再次进行独立 Review；本轮不自动 commit/push。

### Round 3

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlConditionGroup.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlConditionGroup.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Internal/Helper.cs`
	- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
	- `framework/tests/Bing.Data.Sql.Tests/WhereGroupAtomicityTest.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `framework/tests/Bing.Dapper.Sqlite.Tests/Metadata/SqlQueryDescriptionTest.cs`
	- `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：`SelectSubquery` 将完整来源图直接交给 DTO 位置绑定器，`WhereGroup` 又通过 Lambda 参数名选择来源；同类型多来源因此可能依赖来源加入顺序或局部参数名称生成语义错误 SQL。值型 `Where` 的显式来源修复还必须保留实体列元数据，否则 Provider 参数类型和大小会回退。
- 修复：
	- `SelectSubquery` 单/双来源无 alias 路径统一使用严格唯一来源解析；新增单/双来源显式 alias overload，并将解析后的 `TableSource` 列表传入 Core，失败发生在 Clone、SQL、参数和 ShapeVersion 变化前。
	- `ISqlConditionGroup` 与 `SqlConditionGroup` 增加单/双来源 `And`/`Or` alias overload；`ResolveConditionSources` 仅按实体类型、显式 alias 和参数位置绑定，不再读取 Lambda 参数名。
	- 显式来源值型条件通过 `FromClause` 使用实际来源 alias 生成列，同时复用实体列映射元数据创建参数，保留 SQL Server `DbType`、Size 等参数契约。
	- 迁移 SQLite/SQL Server 中同类型重复来源的 `SelectSubquery`、`Select` 和 `Where` 消费者，补充完整 SQL、参数值和失败原子性测试，并同步 Public API 与生产符号追溯。
- 验证：
	- `Bing.Data.Sql.Tests`：PASS，`2498/2498`，net6.0/net8.0。
	- `Bing.Data.Sql.Analyzers.Tests`：PASS，`25/25`，net8.0。
	- `Bing.Dapper.Sqlite.Tests`：PASS，`222/222`，net6.0/net8.0。
	- `Bing.Dapper.Sqlite.Tests.Integration`：PASS，`284/284`，net6.0/net8.0。
	- `Bing.Dapper.SqlServer.Tests`：PASS，`564/564`，net6.0/net8.0。
	- `dotnet build .\Bing.All.sln -c Release -nologo -v minimal`：PASS，0 error，105 warnings。
	- `git diff --check`：待最终收口命令执行。

### Round 3 汇总

- MUST_FIX：1
- 已完成：FIX-001
- PARTIAL：无（本轮 Review Fix 范围内）
- BLOCKED：无
- FAILED：无
- 回归验证：Data.Sql、Analyzer、SQLite Unit、SQLite Integration、SQL Server Unit 和全方案 Release Build 均通过；保留仓库既有编译警告。
- 下一步：再次进行独立 Review；本轮不自动 commit/push。

### Round 4

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/BING-SQL-QUERY-REFACTOR-20260824-001/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
	- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：二元显式 alias 的列投影、DTO 投影、SelectSubquery、Where、GroupBy、OrderBy 和 Having 入口分别解析两个 alias，但未验证两个结果是否为不同的 `TableSource`；同一 alias 两次会让两个 Lambda 参数绑定同一查询来源。
- 修复：新增 `ResolveTwoSources<TFirst,TSecond>` 共享解析路径，先按显式 alias 解析两个来源，再通过对象身份拒绝重复 `TableSource`；七个二元入口统一使用该路径。失败发生在核心 Builder 提交前，不修改 SQL、参数、缓存版本、来源图或 `ShapeVersion`。
- 测试：新增七个重复 alias 原子失败测试，覆盖列 Select、DTO Select、SelectSubquery、Where、GroupBy、OrderBy 和 Having；新增投影、谓词、分组、排序的不同 alias 完整 SQL 测试，并断言 Lambda 参数名称不影响来源绑定。
- 验证：
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Tests\\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal --filter "FullyQualifiedName~SqlQueryLifecycleTest"`：PASS，`134/134`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Tests\\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal`：PASS，`2514/2514`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Analyzers.Tests\\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal`：PASS，`25/25`，net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests\\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal`：PASS，`222/222`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests.Integration\\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal`：PASS，`284/284`，net6.0/net8.0。
	- `dotnet test .\\framework\\tests\\Bing.Dapper.SqlServer.Tests\\Bing.Dapper.SqlServer.Tests.csproj -c Release -nologo -v minimal`：PASS，`564/564`，net6.0/net8.0。
	- `dotnet build .\\Bing.All.sln -c Release -nologo -v minimal`：PASS，0 error，105 warnings。
	- `git diff --check`：PASS；无 whitespace error，仅有既有 CRLF/LF 转换提示。

### Round 4 汇总

- MUST_FIX：1
- 已完成：FIX-001
- PARTIAL：无（本轮 Review Fix 范围内）
- BLOCKED：无
- FAILED：无
- 回归验证：专项测试、Data.Sql、Analyzer、SQLite Unit、SQLite Integration、SQL Server Unit 和全方案 Release Build 均通过。
- Review 文件保持 Reviewer 原始证据，未修改 `review.md`。
- 下一步：再次进行独立 Review；本轮不自动 commit/push。
