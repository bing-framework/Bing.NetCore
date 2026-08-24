<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: sql-query-dev-v6-api-refactor
AI_EXECUTION_FINISHED_AT: 2026-08-22T22:01:44.1674068+08:00

# 实施执行报告

## 执行结论

本次 dev_v6 查询 API 重构已完成核心实现、调用链迁移和本地验证，状态为 `PARTIAL`。非泛型 `SqlLambdaQuery`、连续来源、方法级泛型、原子 Join、WhereGroup、冻结/执行租约、SQL/参数快照、窄 Runtime Bridge、诊断上下文和 Provider 消费者迁移均已落地并通过相关测试。

标记为 `PARTIAL` 的原因是外部 MySQL/PostgreSQL/Oracle 等真实数据库 Gate 未配置而未运行，以及新 API 的可选参数兼容性仍有 RS0026/RS0027 警告；未通过关闭 Analyzer、猜测凭据或跳过本地测试规避这些事项。

## 任务信息

- Task ID：`sql-query-dev-v6-api-refactor`
- 执行器：Copilot plan-executor，source `codex`
- 计划：[plan.md](ai_docs/tasks/sql-query-dev-v6-api-refactor/plan.md)
- 执行报告：[execution.md](ai_docs/tasks/sql-query-dev-v6-api-refactor/execution.md)
- 执行期间未自动执行 `git add`、`git commit`、`git push`、PR、reset、clean 或 restore。

## 计划执行情况

| Phase | 状态 | 结果 |
| --- | --- | --- |
| Phase 0：基线、引用和 API 合同 | COMPLETED | 完成生产符号、消费者、Public API 和 IVT 审计。 |
| Phase 1：独立描述与生命周期 | COMPLETED | Draft/Frozen/Executing/Completed、执行租约、重复执行、失败清理和流式释放已接通。 |
| Phase 2：连续 From 与非泛型 Lambda | COMPLETED | 删除公开 arity 类型和旧生成器，迁移 SQLite、SQL Server、MySQL、EF Core、Admin 消费者。 |
| Phase 3：原子 Join、WhereGroup 和失败回滚 | COMPLETED | 一元/二元方法级 Lambda、原子 Join、CrossJoin 无 On、条件组和候选提交已验证。 |
| Phase 4：终结方法和结果物化 | COMPLETED | Lambda 终结方法显式指定 `TResult`，同步/异步/分页/流式路径均接入 QueryPlan。 |
| Phase 5：QueryContext、ExecutionId 和 Trace | PARTIAL | 诊断上下文、Activity 标签、Correlation/Trace 回退和 QueryPlan 生命周期已实现；日志 Scope 的结构化采集缺少独立断言。 |
| Phase 6：Shape 缓存和 Clone | PARTIAL | 实例 Shape 缓存和 Count/Data 独立 Builder 已实现并测试；完整 Provider/Mapping/Tenant 指纹矩阵和性能前后正式基线仍待补充。 |
| Phase 7：Runtime SPI 和 IVT | COMPLETED | Dapper Core 已迁移到 Runtime Bridge/快照，生产 `InternalsVisibleTo("Bing.Dapper.Core")` 已删除。 |
| Phase 8：文件、命名和 Generator 治理 | COMPLETED | 公开 arity 文件、旧子查询类型和旧生成器已删除，XML 说明已更新。 |
| Phase 9：Unit、SQLite 和 Provider 消费者 | COMPLETED | Unit、SQLite 本地 Integration、SQL Server Provider 及仓库内编译消费者已迁移。 |
| Phase 10：Benchmark、Public API、文档和最终回归 | PARTIAL | Benchmark Dry、本地全量回归和文档收口完成；外部 Gate 缺失和 RS0026/RS0027 作为遗留项保留。 |

## 已完成事项

- 唯一公开 Lambda 描述收敛为非泛型 `SqlLambdaQuery`，通过组合内部 `SqlLambdaQueryCore` 保持公共边界安全。
- 实现连续 `From<TEntity>()`、`FromTable`、`FromSubquery`、一元/二元 Lambda、原子 Inner/Left/Right/Full Join、无 On 的 CrossJoin 和 `WhereGroup`。
- 删除 `SqlLambdaQuery<TEntity>`、高元数 `SqlMultiLambdaQuery.Arity02..10`、`SqlSubqueryLambdaQuery` 和高元数生成脚本。
- `SqlQuery` 实现 Draft/Frozen/Executing/Completed 生命周期、重复执行、冻结后修改拒绝、并发执行租约、失败/取消/流式提前结束清理。
- `SqlQueryPlan` 使用独立 Builder Clone；Count/Data 计划复制 QueryContext，并分别标记 Phase 和 ExecutionId。
- `SqlBuilderExecutionSnapshot` 只保存 SQL、参数和按需 DebugSql；数组、字典、集合和增强参数值按边界快照，未持有连接、事务、Builder 或诊断 Scope。
- QueryPlan Trace 路径使用同一渲染快照生成 SQL 和 DebugSql；Trace 未启用时不调用 `ToDebugSql()`。
- 修正 Provider 参数前缀保留和 `DefaultSqlParameterBinder` 的参数集合兼容性，解决 SQL Server `@_p_0` 参数绑定不一致。
- 删除生产 Dapper Core IVT，保留窄 Runtime Bridge 和内部计划 Builder 边界。
- 诊断消息增加 QueryContextId、ParentQueryContextId、ExecutionId、Phase、TraceId、SpanId、CorrelationId；实现 Activity、Correlation Provider、TraceIdContext 回退优先级、Activity Tags 和日志 Scope。
- 迁移 SQL Server、SQLite、MySQL 集成测试，EF Core 测试和 Admin EFCore/FreeSQL 存储到 dev_v6 调用方式。
- 清理已删除 API 的 PublicAPI Shipped 失效记录；新增公开成员登记到 Unshipped。
- 更新 Lambda 设计、API 治理、SQL 使用和追溯文档；补充 Shape 缓存直接测试、生命周期测试、计划上下文测试、WhereGroup 原子性测试和诊断回退测试。

## 修改文件

- 生产核心：`framework/src/Bing.Data.Sql`、`framework/src/Bing.Dapper.Core` 的查询描述、计划、Runtime Bridge、快照、诊断、参数绑定、Provider/IVT/API 基线。
- 测试：`Bing.Data.Sql.Tests`、`Bing.Dapper.Core.Tests` 相关生命周期/计划/条件组测试；SQLite Unit/Integration；SQL Server Routing/Execution；MySQL Integration；EF Core 查询工厂。
- 消费者：`modules/admin/src/Bing.Admin.Data.EFCore`、`modules/admin/src/Bing.Admin.Data.FreeSQL` 的 `ResourcePoStore`。
- 文档与基准：`ai_docs/sql-lambda-query-design.md`、`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`docs/sqlquery-usage.md`、Root/Join Benchmark。
- 删除：旧泛型查询类型、旧 arity partial 文件、旧 `SqlLambdaQuery` 生成脚本。

## API/数据/配置变化

- 这是计划允许的未发布 API Breaking Change；未添加 compatibility shim 或默认 `[Obsolete]` 转发层。
- `ISqlQuery` 和 `SqlQueryBase` 不再暴露旧高元数 Lambda From；Lambda 终结方法显式指定结果类型。
- Runtime 计划 Builder 仍为 `internal`，Dapper Core 只通过 Bridge、Plan 和执行快照工作。
- 未新增数据库 Schema、迁移、连接字符串、凭据或外部服务配置。

## 测试结果

| 命令 | 结果 |
| --- | --- |
| `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal` | PASS，`2370/2370`，net6/net8。 |
| `dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -nologo -v minimal` | PASS，`262/262`，net6/net8。 |
| `dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal` | PASS，`19/19`，net8。 |
| `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal` | PASS，`222/222`，net6/net8。 |
| `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal` | PASS，`266/266`，net6/net8，真实 SQLite。 |
| `dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -c Release -nologo -v minimal` | PASS，`556/556`，net6/net8。 |
| `dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release -nologo -v minimal` | PASS，12 个 Data.Sql RS0026/RS0027 警告。 |
| `dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -- --filter "*SqlLambda*" --job Dry` | PASS，Root/Join/DynamicFilter Dry 场景产生有效 Mean 结果；Dry Job 为单次迭代，不作为性能收益结论。 |
| `dotnet build .\Bing.All.sln -c Release -nologo -v minimal` | PASS，仓库全量项目编译；存在 net6 EOL、依赖支持和既有项目警告。 |

## Build/Typecheck/Lint/Format

- C# 编译与项目级错误检查：PASS。
- `get_errors` 对本轮修改的生产、测试和 Admin 文件：无错误。
- `git diff --check`：PASS；仅有 Git 关于工作区 CRLF 将转换为 LF 的提示，没有空白错误。
- 未运行独立 Formatter/Linter；仓库未提供比编译和 Analyzer 更具体的统一 Formatter 命令。

## 计划偏差

- 计划要求的完整动态过滤环境指纹缓存矩阵尚未扩展为独立测试；当前采用实例 ShapeVersion、参数布局快照和不保存执行资源的实现，失败候选不 Touch 已有直接断言。
- 计划要求的 Logger Scope 字段采集缺少独立测试 Logger 断言；实际调用链已在 QueryPlan 同步、异步和流式路径接入 `BeginExecutionLogScope`。
- 外部 Provider Integration 没有 Gate 时按计划不执行，不设置猜测凭据，也不写入仓库。
- SQL Metadata 追溯文档保留了明确标记为历史的 V4 符号行；dev_v6 当前映射已改为非泛型/连续 From 口径。

## 基线问题

- `Bing.Data.Sql` 当前仍有 12 个 RS0026/RS0027，集中在新公开方法的可选参数重载兼容性规则；这些不是编译错误，也未通过关闭 Analyzer 处理。
- 全仓库保留 net6.0 EOL、依赖包不支持 net6 和若干既有成员隐藏警告。
- 外部 MySQL/PostgreSQL/Oracle/其它真实数据库测试状态：`NOT_RUN_EXTERNAL_GATE_MISSING`。

## 已知问题

- 未实现计划中可选的公开 Clone 查询描述入口；当前执行计划 Clone、子查询快照和 Builder Clone 均已隔离，查询描述自身仍通过独立工厂创建。
- 缓存当前为实例级 Shape/SQL/参数布局缓存，没有全局缓存，也没有完整的跨租户/Provider 指纹命中矩阵。
- `PublicAPI.Shipped.txt` 的旧已删除符号已清理；后续发布流程仍需按仓库版本策略将 Unshipped 迁移到正式基线。

## 风险与回归关注点

- 这是公开 Lambda API 的有意收敛，仓库外部调用方需要迁移到连续 From 和显式方法级泛型。
- SQL Server 分页 Count/Data 参数顺序和 Trace DebugSql 已通过全量 Provider 测试；继续关注动态过滤、分页派生和参数前缀在新增 Provider 中的实现差异。
- 外部数据库 Gate 启用后应在受控测试库执行对应 Integration 矩阵，不得复用生产连接。
- Benchmark Dry 仅证明场景可运行；不能将单次 ColdStart Mean 解释为性能收益。

## Reviewer 注意事项

- 重点 review `SqlBuilderRuntimeBridge` 的执行 Builder 快照、`DefaultSqlParameterBinder` 的元数据来源分支和 `SqlQueryBase.Diagnostics` 的 Trace 回退优先级。
- 重点 review Public API Shipped/Unshipped 删除记录是否符合仓库发布策略。
- 重点 review `WhereGroup`、Join 候选失败路径和冻结后修改拒绝是否覆盖所有新增公开入口。

## Git 状态

- 工作区包含本任务的生产、测试、文档和 Benchmark 修改；未执行自动提交。
- 未自动执行 `git add`、`git commit`、`git push`、创建 PR、reset、clean 或 restore。

### P1/P2 API 合同验证

- `Bing.Data.Sql.Tests` 新版非泛型 API 合同在 `-p:RunAnalyzers=false` 隔离条件下通过：net6/net8 共 `2340/2340`，退出码 0。
- 同一测试在默认 Analyzer 配置下未进入测试执行：`PublicAPI.Unshipped.txt` 仍声明已 internalize 的 `SqlLambdaQuery<T...>`、`SqlSubqueryLambdaQuery<TProjection>` 和旧高元数 `ISqlQuery.From`，触发 RS0017 并因项目警告策略构建失败。
- 该失败归类为公共 API 基线未同步，不是新版合同测试失败；下一步同步 Data.Sql/Dapper Core 的 Unshipped 后重新运行默认测试。

警告为仓库既有警告，主要包括 net6.0 EOL/TMF 支持提示、现有成员隐藏警告和依赖包兼容提示；本次尚未修改生产代码。

### 最近实施与验证进展

- 已完成非泛型 `SqlLambdaQuery` 的 SQLite 元数据测试迁移，覆盖 `From`、二元 `Where`/`Join`、DTO 投影、`WhereGroup`、重复 `ToSql` 和 `WhereIf(false)`；net6.0/net8.0 均为 `109/109` 通过。
- 已完成 SQLite Integration 查询执行迁移，覆盖连续 `From<TEntity>()`、二元原子 Join、DTO 物化、分页和相关查询链路；Release net6.0/net8.0 均为 `133/133` 通过。
- Integration 项目 Release 构建已通过，C# 编译错误为 0。
- 已修复 `OrderByClause.Exists()` 对 Raw 排序项 `Column == null` 的空引用问题；已通过自连接 Lambda 参数名与显式 alias 对齐，修复自连接来源解析失败。
- 迁移后的高风险 Lambda Integration 过滤测试 net6.0/net8.0 均为 `3/3` 通过。
- 当前 P2/P9 的调用迁移和 SQLite 正确性证据已完成；公共 API 基线、生命周期直接测试、窄 Runtime SPI、诊断/缓存隔离和旧 arity 物理文件治理仍为 `PARTIAL`，尚未宣告完成。
- 上述测试均使用完整 SQL 字符串、参数值/顺序和真实 SQLite 物化断言；没有删除失败测试或关闭功能规避问题。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
- 修复范围：FIX-001 至 FIX-007；FIX-008 为 SHOULD_FIX，本轮未扩大处理。

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryPlan.cs`
  - `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.QueryPlan.Paging.cs`
  - `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.QueryPlan.Streaming.cs`
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：分页和流式路径没有把描述级生命周期通知接入真实 Dapper 执行围栏。
- 修复：分页由原始计划包裹一次描述租约，Count/Data 使用 `acquireExecutionLease: false` 且派生计划不复制生命周期回调；同步/异步流在实际枚举开始时通知开始，在枚举结束、取消、异常、提前 Dispose 和清理异常聚合后通知完成。
- 验证：
  - `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal`：PASS，`272/272`，net6/net8。
  - 新增 `ExecutePage_WhenObserved_ShouldKeepCountAndDataExecutionIdentityDistinct`，真实 SQLite 断言 Count/Data 共享 QueryContext、ExecutionId 不同、Phase 为 Count/Data。
  - SQL Server 全量回归：`558/558`，net6/net8。

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- 根因：动态 `IDataFilter` 状态不一定触发 ShapeVersion 变化，实例缓存可能返回上一环境的 SQL。
- 修复：`RequiresExecutionSnapshot()` 为动态过滤环境旁路 `_cachedSql`，稳定形状继续使用实例缓存；执行快照不持有 Builder、连接、事务或诊断资源。
- 验证：
  - `ToSql_WhenDataFilterStateChanges_ShouldRenderCurrentEnvironment`：PASS，禁用软删除过滤时不含谓词，恢复后完整 SQL 含 `IsDeleted` 谓词。
  - Data.Sql 全量 Unit：`2376/2376`，net6/net8。
  - SQLite Integration 全量：`272/272`，net6/net8。
  - 既有 `WhereIf`、IN/null、raw SQL、Provider/Mapping/Tenant 隔离和失败候选回归均保持通过；不稳定动态环境采用 bypass 而非不完整指纹缓存。

#### FIX-003

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Diagnostics/DiagnosticsMessage.cs`
  - `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- 根因：执行实现仍保留 `Procedure<TResult>`，但接口和 Shipped 基线曾被不一致地修改；`OperationId` 删除也缺少兼容处理。
- 修复：恢复 `ISqlQuery.Procedure<TResult>` 和 Shipped 条目；恢复 `DiagnosticsMessage.OperationId` 为指向 `ExecutionId` 的 `[Obsolete]` 兼容别名，新代码统一使用 `QueryContextId`/`ExecutionId`。dev_v6 计划允许删除的旧 Lambda arity/From 实现继续按计划进入 Unshipped 删除口径，不通过空基线隐藏新增 API。
- 验证：
  - `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release -nologo -v minimal`：PASS，无 RS0016/RS0017/RS0018，仅 12 个既有 RS0026/RS0027。
  - `SqlQueryApiContractTest`、Dapper Core、SQL Server Provider 全量通过。
  - API 追溯矩阵和 `ai_docs/sql-public-api-governance.md` 已明确 Shipped 兼容入口与计划内未发布 Breaking Change 的边界。

#### FIX-004

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.NonGeneric.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/SqlQueryRuntimeFactory.cs`
  - `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs`
  - `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
  - `framework/src/Bing.Dapper.Core/PublicAPI.Unshipped.txt`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
  - 仓库内 Dapper/Provider/EF Core/Analyzer 普通 Raw 调用方
- 根因：Raw 结果类型仍主要在 `Sql<TResult>` 创建处固定，批准计划要求结果由终结方法选择。
- 修复：新增非泛型 `SqlTextQuery`、非泛型 `Sql`/`SqlInterpolated` 工厂和 `ToEntity`、`ToList`、`ToDictionary`、First/Single、Scalar、同步/异步流式终结方法；普通 Raw 调用方全部迁移到非泛型入口。已发布的泛型 Raw 入口和 2 至 7 段低层多映射保留为兼容边界，不再作为新主路径扩散；非泛型 Raw 自动分页明确沿用 Runtime Bridge 的结构化查询限制。
- 固定语义：`ToEntity` 0 行返回 default、1 行返回实体、2 行抛 `InvalidOperationException`；列表完整物化；字典由 selector 构造；sync/async、取消和参数化插值对称。
- 验证：
  - `RawQuery_WhenPublicApiInspected_ShouldSelectResultAtTerminal`：PASS。
  - `RawQuery_WhenResultTypeIsSelectedAtTerminal_ShouldMaterializeExpectedShapes`：真实 SQLite net6/net8 PASS，覆盖同步/异步 ToEntity、ToList、ToDictionary、0/1/2 行。
  - SQLite Integration 全量：`272/272`；Analyzer：`19/19`。
  - `docs/sqlquery-usage.md`、`docs/sqlquery-lambda-usage.md`、公共 API 治理与追溯文档已统一为同一合同。

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：子查询虽生成 ParentQueryContextId，但 From/Join 提交后没有登记到外层 Builder/Plan。
- 修复：类型化 From、所有类型化派生表 Join 重载在候选提交成功后登记父上下文，`SqlQuery.GetPlan()` 将其写入 `SqlQueryPlan.SetContext()`；Clone 的 Parent 指向来源 QueryContextId。失败候选不提前污染父上下文。
- 验证：
  - `Lambda_WhenDtoSubqueryJoined_ShouldExecuteAndMaterializeProjectedMembers`：真实 SQLite BeforeExecute 消息断言 ParentQueryContextId 非空。
  - SQLite Integration 全量：`272/272`。

#### FIX-006

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Diagnostics.cs`
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
  - `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`
- 根因：执行前诊断曾仅由 DiagnosticListener 是否启用决定，Activity/Logger-only 场景提前返回 null。
- 修复：新增 `IsExecutionContextRequired()`，DiagnosticListener、Activity、Logger 各自独立触发同一 `DiagnosticsMessage`；Activity Tags、Logger Scope、Before/After/Error 共享 QueryContextId、ExecutionId、Phase。
- 验证：
  - `ExecuteQuery_WhenOnlyActivityIsActive_ShouldWriteExecutionTags`：真实 SQLite，无订阅者时 Activity Tags 非空且 Phase 为 Data。
  - `ExecuteSql_WhenOnlyLoggerIsConfigured_ShouldBeginStructuredScope`：SQL Server 测试 Logger 关闭 Trace 时仍捕获结构化 Scope。
  - SQL Server 全量：`558/558`；SQLite Integration 全量：`272/272`。

#### FIX-007

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- 根因：此前只有 Builder/Plan Clone，没有公开查询描述 Clone。
- 修复：公开 `SqlLambdaQuery.Clone()`，创建独立 Builder、参数、缓存和 QueryContextId，Parent 指向来源；来源 Frozen/Completed 可克隆为 Draft，来源继续冻结，Clone 可独立修改和执行。
- 验证：
  - `Clone_WhenSourceIsCompleted_ShouldCreateIndependentDraftWithParentContext`：Data.Sql net6/net8 PASS。
  - 动态过滤、来源/Clone 修改隔离和重复执行回归均通过。

### Round 1 汇总

- MUST_FIX：7 项。
- 已完成：FIX-001、FIX-002、FIX-003、FIX-004、FIX-005、FIX-006、FIX-007。
- PARTIAL：无。
- BLOCKED：无；外部 MySQL/PostgreSQL/Oracle Gate 仍按计划未启用。
- SHOULD_FIX 未处理：FIX-008 的完整 Benchmark 矩阵和剩余 RS0026/RS0027；当前构建无新增 RS0016/RS0017/RS0018，保留 12 个既有可选参数兼容警告。
- 下一步：执行 `task-finish.mjs`，交回独立 Reviewer 再次验收。

### Round 2

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
- 修复范围：FIX-001、FIX-002、FIX-003、FIX-004、FIX-006、FIX-007；FIX-005 已在 Round 1 解决；FIX-008 为 SHOULD_FIX，本轮未扩大处理。

#### Round 2 FIX-001

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`；生产分页/流式生命周期实现沿用 Round 1 修复。
- 根因：原有测试只证明 Root 执行租约，未证明同一个公开 Lambda 描述在分页和流式枚举期间的描述级租约。
- 修复：新增 `LambdaDescription_WhenPagedOrStreamed_ShouldUseOneLeaseAndRecoverAfterCancellation`，在同一 `SqlLambdaQuery` 上验证分页 Count/Data、同步流重入、异步取消、Dispose 和后续重复执行；Count/Data 各执行一次并分别使用 `Count`/`Data` Phase。
- 验证：SQLite Integration 定向 `8/8`（net6/net8）和全量 `282/282`（net6/net8）通过；Data.Sql 生命周期全量 `2384/2384` 通过。

#### Round 2 FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`、`framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`、`framework/tests/Bing.Data.Sql.Tests/WhereGroupAtomicityTest.cs`。
- 根因：动态过滤环境需要绕过实例 SQL 缓存；旧 `_cachedParameterLayout` 没有读取点，继续保留会制造错误的缓存合同；参数形状和失败候选缺少直接回归矩阵。
- 修复：动态执行快照旁路 `_cachedSql`；删除未使用的 `_cachedParameterLayout`；补充完整 SQL、参数名称/值/顺序断言，覆盖 IN 长度、null/IS NULL、WhereIf true/false、失败 Join、DataFilter、Clone、Count/Data；保留 EntityMappingResolver 的 Provider/Mapping/Tenant/DbKey 缓存隔离测试和反射资源边界测试。
- 验证：`SqlQueryLifecycleTest` 定向 `26/26`（net6/net8）；Data.Sql Unit 全量 `2384/2384`；SQLite Integration 全量 `282/282`；全解 Release Build `0 errors`。测试已同步移除对已删除缓存字段的反射依赖。

#### Round 2 FIX-003

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`、`framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt`、`ai_docs/sql-public-api-governance.md`。
- 根因：Round 2 复审指出已发布 Lambda/From 符号不能只通过删除 Shipped 基线消失，且治理文档缺少版本、消费者和批准矩阵。
- 修复：恢复并保持两个 Shipped 文件与 HEAD 逐字一致；治理文档新增 `Shipped/Unshipped` 发布审计矩阵，记录仓库 `version.props` 的 `7.0.0` 基线、仓库消费者、外部消费者未知状态和保留/删除批准；明确一元 Lambda/Raw 泛型兼容入口按 Shipped 保留，非泛型 dev_v6 主路径和高元数未发布收敛按 Unshipped/计划内删除处理。
- 验证：两个 Shipped 文件无 Git Diff；Data.Sql 默认 Analyzer 无 `RS0016/RS0017/RS0018`；Data.Sql Unit `2384/2384`、Dapper Core `262/262`、Analyzer `19/19`、SQLite Unit `222/222`、SQL Server `562/562` 均通过。

#### Round 2 FIX-004

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：Raw 非泛型终结、Runtime Bridge、PublicAPI.Unshipped、API Contract 和 SQLite 分页测试沿用 Round 1 实现；本轮更新 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs` 的真实 Clone/分页断言。
- 根因：Raw 统一分页需要真实 Count/Data 计划、绑定参数、安全排序和取消恢复证据，不能由“不支持”说明替代。
- 修复：保留非泛型 `SqlTextQuery.ToPage<TResult>`/`ToPageAsync<TResult>` 及 Runtime Bridge Count/Data 包装、offset/limit 参数绑定和 `ValidateRawPageOrder` 安全校验；SQLite 覆盖普通参数、已知 TotalCount、0/1/多行相关页结果、取消后重试。
- 验证：`RawQuery_WhenPaged_ShouldExecuteCountAndDataWithBoundParameters`、`RawQuery_WhenPageIsCancelled_ShouldReleaseResourcesAndAllowRetry` 定向通过；SQLite Integration 全量 `282/282`；SQL Server 全量 `562/562`；Analyzer `19/19`。

#### Round 2 FIX-006

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.Diagnostics.cs` 沿用 Round 1 实现；`framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/SqlServerRoutingAndExecutionTest.cs`。
- 根因：已有 Activity-only、Logger-only 和 Before/After 测试没有覆盖 Error 路径与全部关闭边界的统一身份。
- 修复：新增全部通道组合 Error 测试，断言 DiagnosticListener Before/Error、Activity Tags、Logger Scope 共享 QueryContextId、ParentQueryContextId、ExecutionId 和 Phase；新增全部诊断通道关闭时不创建执行消息的测试。
- 验证：诊断定向 `6/6`（net6/net8）；SQL Server 全量 `562/562`；Dapper Core 全量 `262/262`；SQLite Activity/分页诊断集成全量 `282/282`。

#### Round 2 FIX-007

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`、`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`、`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs` 沿用 Round 1 实现；`framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`。
- 根因：原有公开 Clone 只有 Mock executor Unit，未覆盖真实 SQLite 的条件、参数、分页和重复执行隔离。
- 修复：真实 SQLite 测试验证来源完成后 Clone、Clone 独立追加稳定名称条件、来源重复执行、Clone Count/Data 分页、不同 QueryContextId 和 Clone Parent 精确等于来源 QueryContextId；测试不依赖不稳定的自增 Id。
- 验证：`LambdaClone_WhenSourceAndCloneExecute_ShouldRemainIndependent` 定向通过；SQLite Integration 全量 `282/282`；Data.Sql 生命周期全量 `2384/2384`。

### Round 2 汇总

- MUST_FIX：6 项。
- 已完成：FIX-001、FIX-002、FIX-003、FIX-004、FIX-006、FIX-007；FIX-005 在 Round 1 已解决。
- PARTIAL：无。
- BLOCKED：无；外部 MySQL/PostgreSQL/Oracle Gate 未配置，按计划未运行并标记为不可验证。
- 回归验证：Data.Sql `2384/2384`；Dapper Core `262/262`；SQLite Unit `222/222`；SQLite Integration `282/282`；SQL Server `562/562`；Analyzer `19/19`；`Bing.All.sln` Release Build `0 errors/133 warnings`。
- 警告与剩余风险：FIX-008 SHOULD_FIX 未处理；Data.Sql 保留 12 个既有 RS0026/RS0027；全解保留 net6 EOL、依赖 TFM、NETSDK1206 和既有成员隐藏警告；未执行外部数据库 Gate。
- 下一步：执行 `node .agents/scripts/task-finish.mjs sql-query-dev-v6-api-refactor`，随后交回独立 Reviewer 再次验收；不修改 `review.md`，不自动 commit/push。

### Round 3

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
- 修复范围：FIX-002、FIX-003、FIX-005、FIX-007；均为 `MUST_FIX`。FIX-008 为 `SHOULD_FIX`，本轮未处理。

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryPlanContextTest.cs`
- 根因：已有缓存旁路实现缺少 Reviewer 要求的直接责任级矩阵，未同时锁定 Raw、Provider 方言、MappingProfile/DbKey、Tenant 参数、Clone 以及 Count/Data 派生计划的隔离关系。
- 修复：新增 Raw 查询无结构化 `_cachedSql` 共享和参数快照测试；新增不同映射配置、数据源、租户和方言的完整 SQL/参数隔离测试；新增 Count/Data 派生计划独立 ExecutionId/Phase/Builder 测试。动态过滤参数从同一执行快照读取，避免误读原始 Draft Builder 的空参数状态。
- 验证：
  - `SqlQueryLifecycleTest`、`SqlQueryPlanContextTest` 定向测试：PASS，`27/27`，net8.0。
  - `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 ...`：PASS，`1196/1196`。

#### FIX-003

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
  - `docs/sqlquery-usage.md`
  - `docs/sqlquery-lambda-usage.md`
  - `ai_docs/sql-lambda-query-design.md`
  - `ai_docs/sql-public-api-governance.md`
  - `ai_docs/sql-metadata-test-traceability.md`
- 根因：零/一参数 `From<TEntity>` 的实际重载解析仍返回已发布泛型兼容描述，但文档和追溯内容将其描述为直接进入唯一非泛型主路径。
- 修复：增加 API Contract 反射测试锁定两参数非泛型主入口与一参数 Shipped 泛型兼容入口的参数数、默认值和静态返回类型；文档明确兼容 wrapper 委托新核心，主路径使用 `From<TEntity>(alias, schema)`，无 alias/schema 时显式传入 `null, null`。未修改任何 `PublicAPI.Shipped.txt`。
- 验证：
  - `From_WhenOverloadsAreResolved_ShouldKeepCompatibilityAndMainPathDistinct`：包含在 Data.Sql 定向 `27/27` 和全量 `1196/1196` 中，PASS。
  - Shipped API 文件未修改；`git diff --check`：PASS。

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlMultiLambdaQuery.cs`
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.NonGeneric.cs`
  - `framework/src/Bing.Data.Sql/AssemblyInfo.cs`
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：真实 SQLite 测试只断言子查询诊断中的父上下文非空，没有把它与创建派生表的源描述上下文建立精确等值断言。
- 修复：向 SQLite Integration 测试程序集开放只读内部 `QueryContextId` 访问；测试保存 `summarySource.QueryContextId`，并断言真实 SQLite BeforeExecute 消息的 `ParentQueryContextId` 与该值完全相等。
- 验证：
  - `Lambda_WhenDtoSubqueryJoined_ShouldExecuteAndMaterializeProjectedMembers`：PASS，真实 SQLite。
  - 与 FIX-007 合并定向 SQLite 测试：`2/2`，net8.0；SQLite Integration 全量：`142/142`，net8.0。

#### FIX-007

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- 根因：公开 Clone 既有 SQLite 测试未覆盖 `IDataFilter`/`ISoftDelete` 状态切换，无法证明副本不复用过期 SQL 或参数，并且 Count/Data 结果没有在真实数据库中验证。
- 修复：复用现有 `soft_delete_samples` 表和生产 `IsDeletedFilter`，插入可见/已删除数据；在过滤启用和 `Disable<ISoftDelete>()` 状态下分别执行来源与 Clone，断言完整结果、分页 TotalCount、Count/Data Phase、不同 QueryContextId 以及 Clone Parent 精确指向来源上下文。未新增数据库 Schema 或外部依赖。
- 验证：
  - `LambdaClone_WhenDataFilterStateChanges_ShouldKeepSourceAndCloneIsolated`：PASS，真实 SQLite。
  - 与 FIX-005 合并定向 SQLite 测试：`2/2`，net8.0；SQLite Integration 全量：`142/142`，net8.0。

### Round 3 汇总

- MUST_FIX：4 项。
- 已完成：FIX-002、FIX-003、FIX-005、FIX-007。
- PARTIAL：无。
- BLOCKED：无；外部 MySQL/PostgreSQL/Oracle Gate 未配置，仍不可验证。
- SHOULD_FIX 未处理：FIX-008 的公开 Clone Benchmark/FormalHost 证据；未扩大本轮范围。
- 回归验证：Data.Sql 定向 `27/27`、Data.Sql 全量 `1196/1196`、SQLite 定向 `2/2`、SQLite Integration 全量 `142/142`，均为 net8.0 PASS；`git diff --check` PASS；变更文件编辑器诊断无错误。
- 下一步：执行 `node .agents/scripts/task-finish.mjs sql-query-dev-v6-api-refactor`，交回独立 Reviewer 再次验收；不修改 `review.md`，不自动 commit/push。

### Round 4

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
- 修复范围：FIX-003；本轮唯一开放的 `MUST_FIX`。FIX-008 仍为 `SHOULD_FIX`，未扩大处理。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `docs/sqlquery-lambda-usage.md`
  - `docs/sqlquery-usage.md`
  - `ai_docs/sql-metadata-test-traceability.md`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- 根因：当前 dev_v6 使用文档仍使用三参数 `Join`/`Select` Lambda，并宣称 Lambda 参数列表可扩展到十个来源；这与公开 API 仅支持一元/二元表达式、通过重复二元 Join 追加来源的实际合同不一致。
- 修复：
  - 将 Lambda 使用文档改为连续二元 `LeftJoin<TLeft,TRight>`/`Join<TLeft,TRight>`，投影改为二元 `Select<TFirst,TSecond,TProjection>`。
  - 将通用 SQL 使用文档中的多表示例改为显式二元泛型调用。
  - 将 dev_v6 顶层追溯表从“1～10/2～10 高元数 API”改为“来源数量不编码在公开类型中、通过重复二元 From/Join 追加”的当前合同；历史 V4 追溯段落保持原样。
  - 强化 `LambdaQuery_WhenPublicApiInspected_ShouldUseMethodLevelUnaryOrBinaryExpressions`，反射检查所有公开 `Expression<Func<...>>` 参数的委托泛型参数数量只能对应一元或二元 Lambda，并继续禁止 `On`/`As`。
- 验证：
  - `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 ... --filter "FullyQualifiedName~SqlQueryApiContractTest"`：PASS，`8/8`。
  - 同命令 `-f net8.0`：PASS，`8/8`。
  - `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 ...`：PASS，`1196/1196`。
  - 同命令 `-f net8.0`：PASS，`1196/1196`。
  - 扫描 `docs/**/*.md` 和 `ai_docs/*.md`：未发现“最多支持十个 Lambda 参数”、三参数 `Join`/`Select` 示例或 `Join<Payment>` 高元数调用。
  - `get_errors`：修改的测试和文档无错误。
  - `git diff --check`：PASS；仅有既有 CRLF/LF 转换提示，无空白错误。

### Round 4 汇总

- MUST_FIX：1 项。
- 已完成：FIX-003。
- PARTIAL：无。
- BLOCKED：无。
- SHOULD_FIX 未处理：FIX-008 的公开 Clone Benchmark/FormalHost 证据，未与本轮文档合同修复共享根因。
- 回归验证：API Contract net6/net8 各 `8/8`；Data.Sql 全量 net6/net8 各 `1196/1196`；修改文件诊断无错误；`git diff --check` PASS。
- 下一步：执行 `node .agents/scripts/task-finish.mjs sql-query-dev-v6-api-refactor`，交回独立 Reviewer 再次验收；未修改 `review.md`，未执行 commit/push。

### Round 5

- Review 状态：NEEDS_FIX
- Review 文件：`ai_docs/tasks/sql-query-dev-v6-api-refactor/review.md`
- 修复范围：FIX-003；本轮唯一开放的 `MUST_FIX`。FIX-008 仍为 `SHOULD_FIX`，未扩大处理。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `docs/ReleaseNotes.md`
  - `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- 根因：活动 7.0.0 发行说明仍把“1～10 个实体来源”写成公开 API 元数合同；API Contract 只枚举有限 `Func<>` 定义，更高元数委托会被筛选遗漏。
- 修复：
  - 将发行说明改为连续 `From<TEntity>(alias, schema)` 追加来源，并明确 `Join`、`Where`、`Select` 等公开 Lambda 仅接受一元或二元来源参数。
  - 将“1～10”明确限定为测试覆盖范围，不再表达为公开 API 固定元数或上限。
  - API Contract 遍历所有公开 `Expression<TDelegate>` 参数，通过委托 `Invoke` 方法的输入参数数量统一检查，移除有限 `Func<>` 白名单。
  - 增加 `Func` 五输入和六输入委托的负向证据，确认同一 `Invoke` 计数逻辑会将其识别为超过二元限制。
  - 未修改 `review.md`、生产实现或两个 `PublicAPI.Shipped.txt`。
- 验证：
  - `SqlQueryApiContractTest` net6.0：PASS，`8/8`。
  - `SqlQueryApiContractTest` net8.0：PASS，`8/8`。
  - Data.Sql 全量 net6.0：PASS，`1196/1196`。
  - Data.Sql 全量 net8.0：PASS，`1196/1196`。
  - 活动 `docs/**/*.md` 扫描：未发现旧“1～10 个实体来源”API 合同、十元 Lambda 宣称或三参数 `Join`/`Select` 示例。
  - 顶层 dev_v6 追溯文档复核：当前表述已保持连续来源/二元 Join 口径；历史 V4 段落继续按历史标记保留。
  - `get_errors`：修改的测试文件无错误。
  - `git diff --check`：PASS；仅有既有 CRLF/LF 转换提示，无空白错误。
  - `PublicAPI.Shipped.txt`：Data.Sql 与 Dapper Core 均无 Git Diff。

### Round 5 汇总

- MUST_FIX：1 项。
- 已完成：FIX-003。
- PARTIAL：无。
- BLOCKED：无；外部 MySQL/PostgreSQL/Oracle Gate 未配置，按计划未运行。
- 回归验证：API Contract net6/net8 各 `8/8`；Data.Sql 全量 net6/net8 各 `1196/1196`；修改文件诊断无错误；`git diff --check` PASS。
- 警告与剩余风险：构建仍保留既有 36 个 CS0108/RS0026/RS0027 警告；FIX-008 的公开 Clone Benchmark/FormalHost 证据仍为 `SHOULD_FIX`，未与本轮 FIX-003 共享根因。
- 下一步：执行 `node .agents/scripts/task-finish.mjs sql-query-dev-v6-api-refactor`，交回独立 Reviewer 再次验收；未修改 `review.md`，未执行 commit/push。
