# Bing.Data.Sql / Bing.Dapper 发布前硬化与完善实施计划

<!-- AI_PLAN_STATUS: READY_FOR_EXECUTION -->
<!-- AI_TASK_ID: BING-SQL-RELEASE-HARDENING-20260831 -->

## 1. 任务元数据与指令裁决

| 项 | 值 |
| --- | --- |
| Task ID | `BING-SQL-RELEASE-HARDENING-20260831` |
| 实际计划路径 | `artifacts/plans/BING-SQL-RELEASE-HARDENING-20260831-plan.md` |
| 类型 | release-hardening / implementation-and-verification |
| 优先级 | P0 |
| 当前结论 | `READY_FOR_EXECUTION`；实施初始状态必须为 `PARTIAL` |
| 技术栈 | .NET，生产库主要为 `netstandard2.0`，Oracle Provider 为 `netstandard2.1`；测试为 `net6.0;net8.0`；xUnit；Dapper `2.1.28`；BenchmarkDotNet `0.14.0` |
| SDK | `global.json` 固定 `8.0.405` |
| 版本基线 | `version.props` 为 `7.0.0`，本任务允许主版本发布前 Breaking Change |
| 自动 Git 操作 | 禁止 commit、push、PR、tag、release |

### 1.1 指令冲突

用户要求“生成计划后继续实施”，但当前会话处于 `plan-writer` 模式，且 `.github/prompts/create-plan.prompt.md` 明确规定本阶段只能创建或更新实际 `plan.md`，禁止修改源码、测试、配置、数据库及执行实施任务。最新且更具体的 Agent 模式约束优先，因此本次只写计划并停止；后续通过 `/execute-plan BING-SQL-RELEASE-HARDENING-20260831`、`/run-plan` 或等价入口实施。

用户指定计划路径为 `artifacts/plans/BING-SQL-RELEASE-HARDENING-20260831-plan.md`，覆盖默认 `ai_docs/tasks/<taskId>/plan.md`。实施期报告仍按用户要求写入 `artifacts/test-results`、`artifacts/benchmarks`、`artifacts/reports`；执行器还应在 `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/` 建立 `execution.md`、`progress.md` 和 `review.md` 以适配仓库既有工作流，但不得复制本计划形成第二份冲突计划。

### 1.2 安全与证据边界

- 外部 Provider 只能使用专用测试数据库、Provider 专属 gate、Provider 专属连接变量及 `ALLOW_DATABASE_RESET_FOR_TESTS=true`；不得使用生产数据库。
- 不在日志、TRX、Markdown 或 JSON 中记录连接字符串、主机、用户名、密码或 Secret 值。
- 不读取、覆盖或提交本地 `integration.runsettings`、`appsettings*.json` 中的用户凭据；只使用维护者显式授权的环境变量或受保护 CI。
- 默认 Skip、`-ValidateOnly`、runner self-test、DI 注册测试、静态 Capability 声明、DryJob 均不能算真实 Provider 执行成功。
- 不新增生产程序集之间的 `InternalsVisibleTo`。现有 IVT 目标均为 UnitTests、IntegrationTests 或 Benchmarks，当前未发现生产 Provider 友元。
- 不为 0GC 引入 Span 化公共 API、`ref struct`、对象池或 Source Generator；只对 FormalHost 证实的热点做最小优化。

## 2. 仓库认知与当前实现状态

### 2.1 已读取的直接证据

- 约束与工作流：`AGENTS.md`、`.github/copilot-instructions.md`、`.github/prompts/create-plan.prompt.md`、plan-writer Agent 配置。
- 前序任务：`BING-SQL-RC-HARDENING-20260825-001` 至 `BING-SQL-RC-HARDENING-20260828-004` 的计划、执行、验证、Benchmark 与 Review 证据。
- 设计与治理：`ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`docs/integration-testing.md`、`docs/migrations/sql-transaction-api-vNext.md`、`docs/sqlquery-usage.md`、`docs/sqlquery-lambda-usage.md`。
- 生产实现：`SqlProviderProfile`、`SqlLambdaQuery`/`SqlLambdaQueryCore`、`SqlTransactionScopeFactory`、`SqlTransactionScopeLease`、`SqlTransactionAsyncAdapter`、Dapper 执行/Procedure/Streaming/Multiple Result 链路、五个官方 Provider Profile。
- 测试与基准：五个 Provider Unit 项目、六个 Provider Integration 项目（含 Doris）、`Bing.Test.Shared`、`Bing.Data.Sql.Benchmarks`。
- Public API：`Bing.Data.Sql`、`Bing.Dapper.Core` 与五个 Provider 均已配置 `Microsoft.CodeAnalysis.PublicApiAnalyzers`，并已有 Shipped/Unshipped 文件。

### 2.2 当前已经真实实现的能力

| 领域 | 当前实体实现 | 判定 |
| --- | --- | --- |
| 查询入口/API | 根入口已收敛为 `Query()`、`Sql(...)`、`SqlInterpolated(...)`、`Procedure(...)`、`From<TEntity>()`；结果类型在 terminal 选择。 | 已实现，已做过主版本 Breaking 收敛。 |
| Terminal | 当前公开主路径包含 `ToEntity`、`First`、`FirstOrDefault`、`Single`、`ToList`、`Scalar`、分页和流式；`SingleOrDefault` 已因与 `ToEntity` 重复而删除。 | 基本完成；仍需最终语义审计和 PublicAPI 冻结。 |
| QueryPlan/资源 | 查询计划、执行快照、ExecutionId、租约、Reader/Connection/Transaction cleanup、同步/异步流均有实体实现和大量替身/SQLite 测试。 | 已实现且覆盖较深。 |
| Mutation | Insert/Update/Delete/SoftDelete/Restore/Purge、批量规划、Returning/SQL Server OUTPUT、并发校验已有实现；SQLite Returning 有真实执行。 | 已实现；跨 Provider 实证不完整。 |
| Capability | `SqlProviderProfile` 已拆 Query/Mutation/Execution/Transaction/Procedure/Limits；官方 Provider 均声明 Profile。 | 部分完成；Transaction 只有总开关，能力来源与实现缺口无法区分。 |
| Fail Fast | Right/Full Join、Returning、Multiple Result、Procedure、只读数据源、Provider 不匹配等多条链路已在连接前拒绝。 | 已实现较多；尚未形成统一 reason model。 |
| Transaction | `ISqlTransactionScope` 是唯一公开事务入口；Scope 固定上下文并共享 Query/Executor；未完成 Dispose 自动回滚。 | 已实现。 |
| Async Transaction | `SqlTransactionAsyncAdapter` 反射调用原生 Begin/Commit/Rollback，缺失时同步回退；取消前后检查已有直接测试。 | 行为已实现，但 Capability/Diagnostics 不可观察。 |
| Cancellation | 预取消、QueryPlan、Multiple Result、Streaming、事务开始和资源恢复已有 Unit/SQLite 测试。 | 部分完成；各外部 Provider 的“执行中取消”证据不足。 |
| Procedure | 公共描述、参数 Binder、Output Snapshot、同步/异步执行已实现；MySQL 有 Output/InputOutput 真实测试，SQL Server 有大量替身测试。 | 部分完成；Provider 真实矩阵严重不对称。 |
| Provider SPI | Provider、Clause Factory、Table Parser、Pagination Renderer、参数管理、Mutation 方言等已有公共 SPI；无 production IVT。 | 部分完成；Lambda Core 仍依赖具体 Clause。 |
| Public API baseline | Data.Sql、Dapper Core、五 Provider 已启用 Analyzer 与 Shipped/Unshipped。 | 已建立；发布前仍需整理 Unshipped 和全包 Gate。 |
| Benchmark | Root/Join/IN/Raw/Metadata/Parameter/Mutation/Returning/Debug/SQLite Dapper E2E 已有大量 BenchmarkDotNet 场景。 | 基础较好；用户要求的完整矩阵、同源 baseline/report 尚未闭环。 |
| 文档 | Query、Lambda、Mutation、Transaction、跨库和集成测试文档已存在。 | 部分完成；目录不统一，缺 Provider Capability、Procedure、Streaming、Diagnostics 完整发布文档。 |

### 2.3 已确认问题

1. **Provider 集成深度不均。** Oracle Integration 目前只有 `SELECT 1 FROM DUAL`；SQL Server 真实库只有连接 Smoke、聚合、层级查询和 Insert OUTPUT 等少量用例，明显低于 SQLite/MySQL/PostgreSQL。
2. **Capability 声明强于证据。** MySQL、PostgreSQL、SQL Server、Oracle 均声明 Stored Procedure/Output Parameters 支持；PostgreSQL 未发现真实 Procedure 用例，Oracle 只有连接 Smoke，SQL Server 主要由替身测试证明。
3. **Transaction Capability 过粗。** `SqlProviderTransactionCapabilities` 仅有 `SupportsTransactions`，无法表达 Native Async Begin/Commit/Rollback、同步回退或禁止回退。
4. **同步回退不可诊断。** `SqlTransactionAsyncAdapter` 会反射尝试异步成员并静默同步回退，当前没有稳定 execution-mode 诊断字段。
5. **Lambda Provider SPI 泄漏具体实现。** `SqlLambdaQueryCore` 对 `SelectClause`、`GroupByClause`、`OrderByClause`、`FromClause` 做具体类型转换，表面接口化但第三方 Clause 实现无法完整支持 Lambda 多源能力。
6. **Provider Profile 缺失时静默 fail-closed。** `SqlProviderCapabilityResolver.GetProfile` 对未实现 Profile 的 Provider 返回空 Profile；执行链通常会拒绝，但错误不能区分“数据库不支持”“Provider 未实现”“Profile 未声明/配置错误”。
7. **数据源命名不一致。** 配置对象使用 `DefaultDataSourceKey`/`SqlDataSourceDescriptor.Key`，公共 Factory/Scope/Context 仍广泛使用 `dbKey`/`DbKey`。实际语义大多是 Logical Data Source，属于已 Shipped 的主版本命名债务。
8. **Provider Integration Contract 尚未形成。** `Bing.Test.Shared` 当前仅有 gate、安全校验和通用断言，没有 Query/Mutation/Transaction/Streaming/Cancellation/Procedure 的可复用 Provider 合同。
9. **Oracle 安全 fixture 缺失。** Oracle 没有固定前缀对象、初始化、清理、资源所有权和 reset 合同，无法安全扩展为真实 DDL/DML 测试。
10. **SQL Server fixture 覆盖窄。** 现有脚本只管理聚合与层级表，未覆盖 CRUD、Batch、Procedure、Multiple Result、Cancellation 和资源生命周期。
11. **Integration 报告语义不完整。** 必须区分 Passed/Failed/Skipped/Not Executed/Unsupported/Implementation Gap；当前历史报告虽有部分区分，但不是统一 Provider Capability Matrix。
12. **Benchmark 发布证据不完整。** 前序 RC 的 diagnostics benchmark 已隔离，但缺完整同 key before/after；用户要求的 Batch 10000、Streaming 100000、Parallel Scope、Transaction 等场景未确认完整存在。
13. **文档结构分散。** 用户建议的 `docs/sql/` 发布文档树尚不存在；当前文档可复用但缺统一入口和最终迁移清理。

### 2.4 待验证问题

- 各 ADO.NET Driver 的原生 Async Begin/Commit/Rollback 实际覆盖：`Microsoft.Data.SqlClient 2.1.7`、`Oracle.ManagedDataAccess.Core 3.21.90`、MySQL、Npgsql、SQLite 对当前目标框架的具体 API。
- PostgreSQL Procedure 的目标语义应使用 Procedure 还是 Function，以及 Output/ReturnValue 在当前 Npgsql/Dapper 版本中的可移植边界。
- MySQL ReturnValue 与 Multiple Result 的真实 Driver 行为。
- Oracle Guid、DateTime、Nullable、Output、InputOutput、ReturnValue 和 Multiple Result 的可支持范围；不能依据数据库理论能力直接标记 Provider 支持。
- Doris 是否继续只读 Query Compatibility，还是存在正式 Mutation/Transaction/Procedure 目标；当前应默认只读且 Unsupported。
- `ISqlQueryClauseAccessor` 与各 Clause interface 是否可直接增加最小多源方法，或需要单独的能力接口；必须先盘点第三方 Custom Provider 编译合同。
- `DbKey` 重命名的实际外部迁移成本。由于已在 Shipped baseline 广泛出现，不应在没有全仓消费者迁移矩阵时一次性机械替换。
- 当前工作树未提交改动与基线 HEAD。实施 Phase 0 必须记录并隔离用户现有改动，不得覆盖。

### 2.5 完成度判断

按“实现存在、直接 Unit、SQLite 真执行、外部 Provider 真执行、发布证据”五层评估：

- 核心 Query/Mutation/资源生命周期：约 **80%-90%**，实现与直接测试较成熟。
- Provider SPI/API 冻结：约 **65%-75%**，基础已建立但具体 Clause 依赖和命名债务未收口。
- Provider 真实能力证明：约 **40%-55%**，SQLite/MySQL/PostgreSQL 较深，SQL Server 较浅，Oracle 接近 Smoke，Doris 仅只读探针。
- Benchmark/报告/文档/Release Gate：约 **45%-60%**，已有基础但本次要求的统一报告与完整矩阵未形成。
- 综合发布完成度：**约 65%，状态 PARTIAL**。当前不足以证明所有官方 Provider 可进入 Release Candidate。

## 3. 质量审计结论

### 3.1 性能与资源

- 已确认资源治理较强：执行租约、流式 Reader、Connection Ownership、事务失败 cleanup、AsyncLocal Scope 恢复均有直接测试。
- 明显风险在真实 Driver 路径：取消期间 Reader/Connection/Transaction 的状态恢复尚未跨 Provider 证明。
- `SqlTransactionAsyncAdapter` 使用反射探测每次调用是否构成热点尚无证据；先 Benchmark/缓存可行性验证，禁止直接复杂化。
- Benchmark 参数规模必须设置内存/时长保护；100000 Streaming 与 10000 Batch 应单独 job，不能拖慢普通 CI。

### 3.2 复杂度、耦合与结构

- `SqlBuilderBase`、`SqlQueryBase` 已使用 partial 文件分责，不能按文件长度机械再拆。
- `SqlLambdaQueryCore` 同时承担 terminal 转发、来源绑定、Clause 具体实现协调和 Query lifecycle，是本次最明确的耦合热点。
- `SqlTransactionScopeFactory` 内嵌 Scope、状态机、cleanup 和 Async Adapter，职责较多但已有成熟测试；只有在 Capability/Diagnostics 改动后仍显著阻碍维护时再按真实责任拆分。
- Provider Integration Tests 大量重复，但应先提取小型合同/fixture protocol，避免创建七层测试抽象。

### 3.3 开发体验与 API

- 根查询入口和 terminal 已基本合理，符合 .NET 的 `First`/`Single` 语义；`ToEntity` 当前表达“至多一行/default”，命名仍需最终确认，但不得无证据同时保留 `SingleOrDefault` alias。
- `DbKey` 与 DataSource Key 混用会增加理解成本。推荐最终语义为 `DataSourceKey`，但迁移必须一次完成实现、PublicAPI、tests、samples、docs；若外部成本过高，本次可冻结旧名并在 8.0 迁移文档中登记，不能再新增新的 `DbKey` API。
- Unsupported 错误需要结构化原因，至少区分 `DatabaseUnsupported`、`ProviderImplementationGap`、`ProviderProfileMissing`；避免增加大量 Provider-specific overload。

## 4. Breaking Change 与迁移策略

| 候选变更 | 默认决策 | 迁移策略 |
| --- | --- | --- |
| 最小 Provider Clause SPI | 允许 Breaking；优先给现有 Clause interface 增加真实需要的多源能力，或新增少量 `IMultiSource*Clause`。 | 官方 Provider 与 CustomProvider 同步迁移；新 API 进入 Unshipped；XML、Analyzer compile contract、provider unit 直接覆盖。 |
| Transaction async capabilities | 允许新增 `SupportsNativeAsyncBegin/Commit/Rollback` 或等价最小属性；默认不得谎报。 | 官方 Provider 明确声明；执行器按声明与实际对象能力校验；PublicAPI/Docs 同步。 |
| Async fallback policy | 默认不新增全局复杂 Options；先采用内部 `Allow` 与 Diagnostics。只有真实需求证明需要时才公开单一枚举/配置。 | 若公开，默认保持当前 `Allow` 行为；`Warn/Throw` 作为显式选择。 |
| Capability failure reason | 优先新增结构化内部 reason + 稳定异常信息；只有第三方 Provider 必须消费时才公开最小枚举/异常属性。 | 不新增 Provider-specific overload；更新错误合同测试。 |
| `DbKey` -> `DataSourceKey` | 本次先做消费者和 baseline 审计后决策。不得保留三套同义 alias。 | 分支 A：7.0 一次性 Breaking rename 并更新全仓；分支 B：冻结现名、禁止新增、记录 8.0 迁移。 |
| Terminal API | 默认不新增/恢复 alias。仅在测试证明语义错误时重命名或删除。 | 更新 Analyzer consumer、Samples、Docs 和 migration guide。 |
| Procedure 差异 | 不增加 Provider-specific overload。 | 通过 Capability 与通用参数方向模型表达；不支持项明确 Unsupported/Implementation Gap。 |
| 生产 IVT | 禁止新增。 | 跨程序集协作必须使用公开且最小的 SPI，建议 `[EditorBrowsable(EditorBrowsableState.Never)]`。 |

## 5. Provider Capability Matrix 基线与目标

执行器在 Phase 0 必须把下表拆为每 Provider 的 `Declared / Unit Proven / Real Integration Proven / Unsupported / Implementation Gap / Not Executed` 六态矩阵。静态 Profile 只能填 `Declared`。

| 能力域 | SQLite | MySQL | PostgreSQL | SQL Server | Oracle | Doris |
| --- | --- | --- | --- | --- | --- | --- |
| Query/Scalar/List/Paging/Aggregate/Join | 真实覆盖较深 | 真实覆盖较深 | 真实覆盖较深 | 部分真实覆盖 | 仅 Scalar Smoke | 常量 Query/参数/Limit Offset |
| Raw/Interpolated/Lambda | SQLite 深 | MySQL 部分 | PostgreSQL 部分 | SQL Server 缺统一深度 | 未证明 | 只读兼容探针 |
| Streaming/Cancellation | SQLite 深 | 有真实用例 | 有真实用例 | 缺真实矩阵 | 未证明 | 未证明/默认不承诺 |
| Multiple Result | SQLite 真实 | 待验证 | 待验证 | 替身深、真实缺 | Profile 未声明支持 | 默认 Unsupported |
| Mutation/Batch/Concurrency | SQLite 深 | 部分真实 | 部分真实 | OUTPUT 真实、CRUD/Batch 缺 | Unit 方言为主，真实缺 | 只读 Unsupported |
| Transaction | SQLite 深 | Commit/Rollback 有用例 | Commit/Rollback 有用例 | 真实缺统一矩阵 | 未证明 | Unsupported |
| Procedure/Output | SQLite Unsupported | Output/InputOutput 真实 | 未发现真实用例 | 替身深、真实缺 | 未证明 | Unsupported |

## 6. 文件修改清单

### 6.1 已确认实施期会创建的文件

- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/progress.md`
- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/execution.md`
- `ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/review.md`
- `artifacts/test-results/unit-test-report.md`
- `artifacts/test-results/integration-test-report.md`
- `artifacts/benchmarks/benchmark-report.md`
- `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`

### 6.2 已确认修改范围

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/SqlProviderProfile.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/ISelectClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/IGroupByClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/IOrderByClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/IFromClause.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/IJoinClause.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase*.cs`、`SqlExecutorBase*.cs`、Multiple Result/Procedure 文件，仅限 Capability/Cancellation/Diagnostics 真实调用链
- 五个官方 Provider 的 `*SqlProvider.cs` Profile 声明
- `framework/tests/Bing.Test.Shared/**`，增加最小 Provider Integration Contract 与报告模型
- Oracle/SQL Server Unit 与 Integration 项目及受控 fixture/script
- MySQL/PostgreSQL/SQLite/Doris Integration 项目，仅补合同缺口，不重复已有深度用例
- `framework/tests/Bing.Data.Sql.Tests/**`
- `framework/tests/Bing.Data.Sql.Analyzers.Tests/**`
- `framework/tests/Bing.Dapper.Core.Tests/**`
- `framework/tests/Bing.Data.Sql.Benchmarks/**`
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`（所有受影响包）
- `ai_docs/sql-public-api-governance.md`
- `ai_docs/sql-metadata-test-traceability.md`
- 现有 SQL 文档与 ReleaseNotes

### 6.3 候选文件，证实需要后再修改/创建

- `framework/tests/Bing.Test.Shared/Sql/SqlProviderQueryContract.cs`
- `SqlProviderMutationContract.cs`、`SqlProviderTransactionContract.cs`、`SqlProviderStreamingContract.cs`、`SqlProviderCancellationContract.cs`、`SqlProviderProcedureContract.cs`、`SqlProviderCapabilityContract.cs`
- Oracle Integration 的 `Infrastructure/OracleIntegrationDatabaseFixture.cs`、`DatabaseScript.cs`、collection；只有先建立安全 schema/user/固定前缀清理合同才创建。
- SQL Server Integration 的 Query/Mutation/Transaction/Procedure/MultipleResult 测试文件；按职责拆，不继续塞入 Smoke 类。
- `docs/sql/` 下用户建议的发布文档树；优先迁移/重组现有文档，避免同一主题双份冲突。
- `AsyncFallbackBehavior`、`TransactionExecutionMode` 或结构化 capability reason 类型；只有 Phase 3 设计闸门通过后创建。
- `appveyor.yml`、`eng/ci/Invoke-ProviderIntegrationTests.ps1`；只有需要扩展 Oracle/Doris lane 或报告字段时修改。

## 7. 分阶段实施计划

### Phase 0 - 基线、完成协议与报告骨架

#### REL-P0-01 [P0] 冻结真实基线与工作树

**依赖：** 无。  
**目标：** 防止复用历史计数或覆盖用户改动。

**步骤：**

1. 记录实际分支、HEAD、dirty path 列表、SDK、runtime、OS、CPU、内存、当前日期和所有相关包版本。
2. 建立 `progress.md`/`execution.md`，状态使用 `PENDING/IN_PROGRESS/COMPLETED/PARTIAL/BLOCKED/FAILED`。
3. 创建三类用户要求报告与 final report 骨架，状态先为 `PENDING`；不得复制历史数字。
4. 扫描现有 PublicAPI、IVT、测试项目引用、Benchmark 类型和 Integration gate，生成机器可追溯的当前清单。
5. 记录用户现有未提交改动；与任务重叠时在原状态上工作，不回退。

**验收：** 所有结论绑定当前 HEAD/worktree；报告不含凭据；总状态为 `PARTIAL`。

#### REL-P0-02 [P0] 建立六态 Capability Matrix

**目标：** 把“声明支持”和“真实验证”分开。

**步骤：**

1. 对 Query/Execution/Mutation/Transaction/Procedure 各能力记录 `Declared`、`Unit Proven`、`Real Integration Proven`、`Unsupported`、`Implementation Gap`、`Not Executed`。
2. 每个格子链接到生产符号、测试方法、Provider/DB 版本、TRX 和运行时间。
3. 定义 Unsupported 与 Implementation Gap 的判定责任：数据库语义证据、Driver 能力、Bing Provider 实现分别记录。
4. 定义 Release Gate：只有必须能力 real proven 或明确 Unsupported，且没有阻塞发布的 Implementation Gap，Provider 才通过。

**验收：** 静态 Profile 不再等同于通过；Doris 明确只读边界；Oracle 当前只能标 Scalar Smoke。

### Phase 1 - 统一 Provider Integration Contract

#### REL-P1-01 [P0] 设计最小共享合同

**依赖：** REL-P0-02。  
**目标：** 在 `Bing.Test.Shared` 复用断言和场景协议，不强迫所有 Provider 继承复杂基类。

**步骤：**

1. 盘点 SQLite/MySQL/PostgreSQL 已有用例，提取场景描述、fixture 操作、expected capability 和结果断言，不移动 Provider-specific SQL。
2. 优先创建组合式 contract runner/abstract fixture interface；每个领域只有出现三个以上重复实现时才建抽象。
3. Query Contract 覆盖 Scalar/Single/List/Paging/Aggregate/GroupBy/Join/Subquery/Raw/Interpolated/Lambda。
4. Execution Contract 覆盖 sync/async/cancellation/stream/IAsyncEnumerable/multiple result。
5. Mutation Contract 覆盖 CRUD、SoftDelete/Restore/Purge、Batch、Concurrency、Returning/Output。
6. Transaction Contract 覆盖 Begin/Commit/Rollback/Dispose without commit、Query+Executor share、async completion、cancellation。
7. Procedure Contract 覆盖 Input/Output/InputOutput/ReturnValue/nullable/string/decimal/datetime/guid/sync/async/cancel/multiple result。
8. Capability Contract 验证 Profile 声明与已注册 contract 场景一致，防止声明支持但没有任何实证入口。

**测试：** `Bing.Test.Shared` 自身测试合同发现、Unsupported/Gap 分类、Provider 名规范化和报告输出。  
**风险：** 过度抽象导致 Provider 语义被抹平。  
**验收：** 共享层不含连接字符串和 Provider-specific DDL；Provider 可选择能力场景；重复代码有实际减少。

#### REL-P1-02 [P0] 接入 SQLite/MySQL/PostgreSQL 作为合同基准

**依赖：** REL-P1-01。  
**步骤：**

1. 先让 SQLite 对本地可支持合同全绿，作为无外部依赖基准。
2. 将 MySQL/PostgreSQL 现有深度用例映射到合同，不重写已稳定测试。
3. 对合同缺口新增最小真实用例，继续使用专用 gate、安全 reset 和既有 fixture。
4. 生成每 Provider 场景列表，明确哪些是已存在映射、哪些是新增。

**验收：** 合同不能把默认 Skip 计为 Passed；SQLite 必须真执行；MySQL/PostgreSQL 缺外部环境时准确 `NOT EXECUTED/BLOCKED`。

### Phase 2 - Oracle 与 SQL Server 补齐

#### REL-P2-01 [P0] 建立 Oracle 安全集成 fixture

**依赖：** REL-P1-01；维护者提供专用 Oracle 测试 schema/user。  
**步骤：**

1. 定义 Oracle 安全对象前缀、允许的 schema/user 命名、禁止系统 schema 和固定对象清理范围。
2. fixture 仅创建/删除固定前缀表、序列、过程；不得 drop schema/user/database。
3. 接入 Provider gate、连接解析、安全校验、连接池清理和可重入 reset。
4. 缺少安全外部条件时标记 `BLOCKED`，不要扩展连接 Smoke 后宣称完成。

**验收：** reset 可重复，失败不泄露连接信息，清理只影响固定对象。

#### REL-P2-02 [P0] Oracle Query/Mutation/Transaction/Procedure 合同

**依赖：** REL-P2-01。  
**用例矩阵：**

| Given | When | Then |
| --- | --- | --- |
| 固定测试表含 null/guid/datetime/decimal | Raw/Lambda/List/Scalar/Paging/Aggregate/Join | 值与 Oracle 方言结果正确 |
| 固定实体 | Insert/Update/Delete | affected rows 与最终数据正确 |
| 事务内写入 | Commit/Rollback/Dispose | 分别持久化/回滚，资源释放 |
| 异步查询与长操作 | 预取消/执行中取消 | 抛取消异常，后续同 Factory 可复用 |
| 固定过程 | Input/Output/InputOutput/ReturnValue | 只对真实支持方向标 Proven |
| Query/Executor Dispose | 自有/Scope 资源 | Ownership 与连接池状态正确 |

Oracle 本身或 Driver 不支持的能力标 `Unsupported`；数据库支持但 Bing 未实现标 `Implementation Gap`。  
**验收：** 不再仅有 DUAL Smoke；Capability Matrix 与真实结果一致。

#### REL-P2-03 [P0] SQL Server 完整真实合同

**依赖：** REL-P1-01。  
**步骤：**

1. 扩展固定前缀脚本覆盖 Query、CRUD、Batch、Transaction、Procedure、Multiple Result 和 cancellation 所需对象。
2. 增加 Raw/Lambda/Join/Aggregate/Paging、sync/async stream、执行中取消与取消后复用。
3. 增加 Insert/Update/Delete/Batch/Concurrency/OUTPUT。
4. 增加 Commit/Rollback/Dispose/Async Begin+Commit+Rollback、Query+Executor share。
5. 增加 Multiple Result 与 early dispose。
6. 增加 Stored Procedure 的 Input/Output/InputOutput/ReturnValue、nullable/string/decimal/datetime/guid、sync/async/cancel。

**验收：** SQL Server 真实合同深度至少达到当前 PostgreSQL 的核心领域；替身测试不替代真实数据库证据。

### Phase 3 - Transaction / Async Capability 与 Diagnostics

#### REL-P3-01 [P0] 锁定现有回退语义

**依赖：** Phase 1。  
**步骤：**

1. 为 Adapter 的 native async、同步 fallback、预取消、fallback 前取消、原生 async 抛错、cleanup 抛错增加直接 Unit。
2. 对五个官方 Driver/Connection/Transaction 类型做 API 能力探测测试，记录版本，不使用字符串类型名猜测。
3. 证明当前反射路径是否每次调用重复查找；只有 Benchmark 显示热点才考虑缓存 MethodInfo/delegate。

**验收：** 当前行为在任何重构前被直接测试锁定。

#### REL-P3-02 [P0] 扩展最小 Transaction Capability

**依赖：** REL-P3-01。  
**目标：** 表达 Native Async Begin/Commit/Rollback 与 fallback，而不制造复杂 Options。

**步骤：**

1. 在 `SqlProviderTransactionCapabilities` 增加最小属性，优先命名 `SupportsNativeAsyncBegin`、`SupportsNativeAsyncCommit`、`SupportsNativeAsyncRollback`；若 Driver 版本差异导致静态 Provider 声明不可靠，则改为 `Native/RuntimeDetected/Unavailable` 的最小枚举。
2. 默认保持当前同步回退兼容行为；不立即公开 `AsyncFallbackBehavior`。
3. 执行链校验 Profile 与运行时对象实际能力；声明 Native 但运行时缺失属于 `ProviderImplementationGap/ProfileMismatch`，不能静默伪装。
4. 更新五 Provider Unit 与 PublicAPI baseline。

**验收：** 官方 Provider 声明有直接测试；第三方缺 Profile 保持 fail-closed 且错误原因明确。

#### REL-P3-03 [P1] 增加 Transaction execution-mode Diagnostics

**依赖：** REL-P3-02。  
**步骤：**

1. 沿用现有 Diagnostics 事件风格，增加 `NativeAsync` / `SynchronousFallback` execution mode 字段或标签。
2. 不在普通无监听路径创建额外快照或字符串。
3. Unit 断言 Begin/Commit/Rollback 三阶段的 mode；Integration 至少在 SQLite 和一个外部 Provider 验证。
4. 只有维护者明确需要强制策略时，再评估公开 `AsyncFallbackBehavior.Allow/Warn/Throw`；否则保持非公开。

**验收：** fallback 可观察，Diagnostics OFF 热路径无明显回归。

### Phase 4 - CancellationToken 全链路

#### REL-P4-01 [P0] 静态与 Unit 传递审计

**依赖：** Phase 3。  
**步骤：**

1. 追踪 `ISqlQuery/ISqlExecutor -> description -> QueryPlan/Prepare -> CommandDefinition -> Dapper/ADO.NET`。
2. 对 Query、Scalar、Paging Count/Data、Streaming、Multiple Result、Mutation Batch、Procedure、Transaction Begin/Commit/Rollback 分别验证同一个 token 到达边界。
3. 预取消必须早于 capability、Hook、连接、命令和枚举外部输入；已有测试复用并补缺。
4. 取消与 Reader dispose/rollback/lease release 同时失败时保持主异常优先与 cleanup 顺序。

**验收：** 每个终端有 production symbol -> direct test 映射。

#### REL-P4-02 [P0] 真实数据库取消矩阵

**依赖：** Provider fixtures。  
**场景：** cancel before execute、during execute、during streaming、after reader created、transaction begin、transaction operation、dispose after cancellation。  
**额外断言：** 同一 Factory/Executor/Connection Factory 后续查询成功；无 lease、Reader、Connection、Transaction、AsyncLocal 残留。  
**验收：** SQLite 必过；MySQL/PostgreSQL/SQL Server/Oracle 按 Driver 支持真实执行或明确 Gap；不得使用 `Task.Delay` 竞态作为唯一触发机制，优先数据库可控等待/阻塞语句和超时保护。

### Phase 5 - Stored Procedure 完整验证

#### REL-P5-01 [P0] 统一 Procedure Contract 与语义差异

**依赖：** REL-P1-01。  
**步骤：**

1. 锁定公共参数方向、Output Snapshot、DBNull/null、类型转换和 ReturnValue 语义。
2. 为 MySQL、PostgreSQL、SQL Server、Oracle 分别定义 Provider fixture procedure/function，不增加公共 Provider-specific overload。
3. Multiple Result 仅对 Driver/数据库真实支持者启用；不支持者明确 Unsupported。
4. Capability 声明必须有 Unit + 至少一个 real integration 方法映射，否则标 Implementation Gap。

**验收：** MySQL 现有 Output/InputOutput 纳入合同；PostgreSQL/SQL Server/Oracle 补真实证据或降级 Profile 声明。

#### REL-P5-02 [P0] Procedure 真实矩阵

**矩阵：** Input、Output、InputOutput、ReturnValue、Nullable、String、Decimal、DateTime、Guid、Sync、Async、Cancellation、Multiple Result。  
**风险：** PostgreSQL procedure/function 和 Oracle ref cursor 语义不同。  
**验收：** 报告逐格写 Proven/Unsupported/Gap，不做跨数据库伪统一。

### Phase 6 - Provider SPI 治理

#### REL-P6-01 [P0] 消除 Lambda Core 具体 Clause 强转

**依赖：** Phase 1 contract，避免只做架构重构不验证行为。  
**步骤：**

1. 列出 `SqlLambdaQueryCore` 对 `FromClause.ResolveMultiSource*`、`SelectClause.Select`、`GroupByClause.AddBoundColumns/SetBoundHaving`、`OrderByClause.AddBoundColumns`、`JoinClause` 的全部具体依赖。
2. 按真实能力选择最小接口：优先在现有 interface 添加职责清晰的方法；仅当第三方普通实现不应承担时新增少量 `IMultiSource*Clause`。
3. 新 SPI 放在 `Builders/Providers/Abstractions` 或现有 Builders 抽象目录，公开时加 `[EditorBrowsable(EditorBrowsableState.Never)]`。
4. 默认 Clause 与五官方 Provider/Custom Provider 迁移；删除所有相关 `as ConcreteClause`。
5. 增加第三方 consumer compile contract 与一个 custom clause runtime test，证明接口不是骨架。

**验收：** `SqlLambdaQuery -> Stable Provider SPI -> Provider Clause`；无 production IVT；没有一次新增几十个接口。

#### REL-P6-02 [P1] Capability Fail Fast reason model

**依赖：** REL-P6-01。  
**步骤：**

1. 盘点 Returning/Output/Multiple Result/Update From/Delete Using/Multi Row Values/Procedure/Streaming/Async Transaction/Cross Database 的 fail-fast 点。
2. 统一内部 reason：Database Unsupported、Provider Implementation Gap、Profile Missing/Mismatch。
3. 错误必须在 Build/Prepare/Capability Validation 前移，且测试断言连接/命令未访问。
4. 只在第三方 Provider 需要编程消费 reason 时公开最小稳定属性。

**验收：** 同一能力的 Unit/Provider/Integration 错误分类一致。

### Phase 7 - API 最终冻结与 IVT

#### REL-P7-01 [P0] Public API 全量审计

**依赖：** Phase 3、5、6。  
**步骤：**

1. 导出 Data.Sql、Dapper Core、五 Provider 的 public/protected API，与 Shipped/Unshipped 对照。
2. 审计根入口、terminal 语义、Procedure、Transaction、DataSource、Provider Profile、SPI。
3. 删除重复/alias/占位 API；不恢复 legacy API；每项删除给 migration replacement。
4. 审计 `GetCurrentProviderProfile` 类调用：执行态使用 Required/fail-closed，描述态才允许 Try；避免返回空 Profile 掩盖配置错误。
5. 对 `DbKey` 决策执行分支闸门：一次性 rename 或冻结旧名，禁止同时新增 alias。

**验收：** Analyzer 无 RS0016/17/18；Samples/Docs/Tests 同步；API 变更清单完整。

#### REL-P7-02 [P0] IVT 与测试 helper 治理

**步骤：**

1. 扫描相关所有 AssemblyInfo/csproj。
2. 保留 UnitTests、IntegrationTests、Benchmarks 和必要测试辅助项目；删除无实际使用的 friend。
3. 测试共享逻辑优先移入 `Bing.Test.Shared`，不为测试把 internal 改 public。
4. 增加反射/编译合同，确保官方生产 Provider 不在 friend list。

**验收：** 无 production-to-production IVT；每个保留 IVT 有具体消费者证据。

### Phase 8 - 内部复杂度治理

#### REL-P8-01 [P1] 按职责重构热点

**依赖：** Provider Contract、API Freeze、SPI 完成。  
**范围：** `SqlBuilderBase`、`SqlQueryBase`、`SqlExecutorBase`、`JoinClause`、`WhereClause`、`SqlLambdaQuery`、`MutationClauseExtensions`。  
**步骤：**

1. 使用调用图、变更频率和测试定位真实职责混合，不按 500 行阈值拆。
2. 已有 partial 文件优先继续按 Render/Parameters/Filters/Validation/Clone/Lifecycle/Execution/Mutation Planning 划分。
3. 只有多个真实实现时创建 interface；单实现协作者用 internal sealed class/static helper。
4. 每次重构先锁行为测试，再小批移动，避免大规模格式化。

**验收：** public API 与 SQL 输出不变；复杂度下降有具体职责/依赖证据；无 Interface->Abstract->Base->Default 过度层级。

### Phase 9 - Unit Test 闭环

#### REL-P9-01 [P0] 职责级直接测试

**依赖：** 各实现 Phase。  
**覆盖：** Public API、internal、Extension、Builder、Clause、Parser、Metadata、Parameter Binder、QueryPlan、Mutation Planner、Transaction、DataSource、Scope、Capability、Diagnostics。  
**边界：** Null、Empty、Invalid、Boundary、Large Input、Concurrency、Cancellation、Dispose、AsyncDispose、Exception Path。  
**规则：** SQL 输出断言完整字符串；缓存覆盖 hit/miss/isolation；Provider 每个受影响分支有成功+失败；测试方法英文、中文 XML 测试目的、AAA。  
**验收：** 更新 `ai_docs/sql-metadata-test-traceability.md` 的最终 production symbol -> test method 映射。

### Phase 10 - Integration 执行与报告

#### REL-P10-01 [P0] 执行 Provider 矩阵

**依赖：** Provider fixture 和外部授权。  
**Provider：** SQLite、MySQL、PostgreSQL、SQL Server、Oracle、Doris。  
**步骤：**

1. SQLite 本地双 TFM 真执行。
2. 外部 Provider 使用专属 gate、安全库和 reset；每个 Provider 至少 net8.0，仍支持 net6.0 的项目执行代表性 net6.0。
3. 保存 TRX，解析 Total/Passed/Failed/Skipped/Not Executed；记录 DB/Driver 版本和是否真实连接。
4. Doris 只读合同单列，不因 Mutation Unsupported 失败。
5. 无外部条件时继续完成无依赖工作，并在 final report 标 `BLOCKED`。

**验收：** 默认 Skip 不算通过；每个 real run 有当前源码 identity 和无密 artifact。

#### REL-P10-02 [P0] 生成测试报告

`unit-test-report.md` 至少包含执行时间、Commit/worktree、Runtime、TFM、项目、Total/Passed/Failed/Skipped/Duration、失败原因和 Coverage（若有）。  
`integration-test-report.md` 至少包含 Provider、Database/Version、Environment、Total/Passed/Failed/Skipped/Not Executed、场景 Matrix、是否真实连接。  
**验收：** 报告数字来自当前 TRX，不手填历史计数。

### Phase 11 - Benchmark 基线与优化

#### REL-P11-01 [P1] Benchmark 缺口审计与补齐

**依赖：** API/SPI 稳定。  
**现有可复用：** Raw、Lambda Root/Join、IN 参数、Metadata、Mapping、QueryPlan prepare、Clone、Mutation、Returning、Diagnostics、SQLite E2E。  
**补齐重点：** Join 3/5/10 的明确场景、Cache Hit/Miss 分离、Parameter 1/10/100/1000/2100、Batch 10/100/1000/10000、Streaming 100/10000/100000、ToList 100/10000、Diagnostics OFF/ON、Database Scope 1/10/100、Parallel Scope、Transaction、SQLite Dapper E2E。

**步骤：**

1. 使用 BenchmarkDotNet `MemoryDiagnoser`；正式 job 保持仓库 `FormalHost`（3 launch、6 warmup、15 iteration）或记录经批准的等价配置。
2. 大规模场景独立 filter/job，设置超时和磁盘清理；不进入普通单元 CI。
3. 保存 raw csv/json/markdown/log、命令、SDK/runtime/OS/CPU/GC、source identity、SHA-256。
4. 历史制品只有 case key 完全一致才做 Baseline/Current/Delta。
5. 性能问题标 `Confirmed/Potential/Needs Benchmark`。

**验收：** Smoke 与 FormalHost 分开；无同 key baseline 时 `NOT_COMPARABLE`。

#### REL-P11-02 [P2] 仅优化 Confirmed Hot Path

**步骤：**

1. 对 Mean/Median/StdDev/Ratio/Allocated/Gen0/1/2/LOH 证实的回归定位。
2. 优先减少重复解析、重复反射、无效快照或重复渲染；不改变公共 API。
3. 优化前后同机同 key；正确性回归矩阵先通过。
4. 未达到预设收益或维护成本过高则撤回。

**验收：** 不要求 0 B；无明显核心 regression；报告包含 Baseline/Current/Delta。

#### REL-P11-03 [P1] 生成 Benchmark 报告

`artifacts/benchmarks/benchmark-report.md` 记录日期、Commit/worktree、OS/CPU/Memory、Runtime、BDN 版本、Benchmark、Mean/Median/StdDev/Ratio/Allocated/Gen0/1/2、Baseline/Current/Delta/Regression/Improvement。  
**验收：** 不使用“completed successfully”替代数据。

### Phase 12 - Public API、XML、文档与发布准备

#### REL-P12-01 [P0] Public API Baseline Release Gate

**步骤：**

1. 清理所有受影响包的 Unshipped：确认发布 API 移入 Shipped，删除不再发布符号。
2. CI/build 验证新增、删除、修改 API；不得关闭 Analyzer。
3. final report 列 Breaking/API changes 和 migration path。

**验收：** 所有项目 RS0016/17/18 为 error 且全绿。

#### REL-P12-02 [P1] XML Documentation

**步骤：**

1. 覆盖所有新增/修改 public API 的 summary/typeparam/param/returns/exception。
2. override/interface implementation 优先 `inheritdoc`。
3. 复杂 internal 只说明 Why/Invariant/Boundary/Concurrency/Ownership。
4. 构建检查 XML warning；不机械改动未触及 API。

**验收：** 新 API 无缺失说明，异常语义与测试一致。

#### REL-P12-03 [P1] SQL 用户文档统一

**步骤：**

1. 复用现有 `sqlquery-usage.md`、Lambda、Mutation、Transaction、跨库文档，决定迁入 `docs/sql/` 或建立单一 toc 链接；禁止双份冲突内容。
2. 最终覆盖 Getting Started、Query、Lambda、Fluent、Raw、Interpolated、Mutation、Batch、Transaction、Streaming、Stored Procedure、Multi Database、Read/Write、Provider Capabilities、Diagnostics、Migration Guide。
3. 删除旧 GetBuilder、旧 Fluent/Raw/Transaction API 示例。
4. 更新 `toc.yml`、README/ReleaseNotes（仅真实结果）。

**验收：** 文档示例通过 Analyzer consumer compile 或 Sample build；Provider capability 与报告一致。

### Phase 13 - Release Gate、Final Report 与独立 Review

#### REL-P13-01 [P0] 执行最终发布门禁

**真实命令入口（实施前需按当前项目确认 `--no-restore` 条件）：**

```powershell
dotnet build .\Bing.All.sln -c Release -nologo -v quiet -clp:ErrorsOnly
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net8.0 --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net8.0 --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 --nologo
```

外部 Provider 命令使用项目既有 gate/runner；若 runner 未扩展 Oracle/Doris，则执行器先更新并自测 runner，再运行。  
**门禁：** Correctness、API、Resource、Performance、Documentation 五类全部逐项记录。

#### REL-P13-02 [P0] Final Report

生成 `artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`，严格包含用户要求的 18 节，以及新增/修改/删除/重命名文件清单。  
**状态规则：** 任一必须 Provider 无 real evidence、核心 Benchmark 无可比较数据且发生热点改动、PublicAPI/文档未闭环或有未解决 MUST_FIX，则输出 `BLOCKED` 或 `PARTIAL`，不得写“任务完成”。

#### REL-P13-03 [P0] 独立 Review 与 fix-review

1. 独立 reviewer 审查 plan、diff、源码、tests、TRX、Benchmark raw artifacts、PublicAPI、文档和 final report。
2. Review 优先检查 capability 声明与证据、Oracle/SQL Server 深度、取消后复用、SPI 具体依赖、production IVT、Dry/FormalHost 区别和报告真实性。
3. 使用 `fix-review` recommended 范围处理 MUST_FIX + SHOULD_FIX；不篡改 review.md。
4. 修复后按影响重跑最小测试与 Release Gate，直至 PASS 或有明确外部 BLOCKED。

## 8. Given/When/Then 核心用例矩阵

| 领域 | Given | When | Then | 测试层级 |
| --- | --- | --- | --- | --- |
| Capability | Provider 声明支持 Procedure | 未注册任何真实 Procedure contract | Matrix 标 Implementation Gap，Release Gate 失败 | Unit/report contract |
| SPI | 自定义 Clause 实现最小多源 SPI | Lambda Select/Group/Order/Join | 不依赖 Bing concrete Clause，完整 SQL 正确 | Analyzer + CustomProvider Unit |
| Async Tx | Driver 有原生 async | Begin/Commit/Rollback Async | 调用原生成员，Diagnostics=NativeAsync | Unit + Integration |
| Async Tx | Driver 无原生 async | Async operation | 按允许策略同步回退，Diagnostics=Fallback，取消仍优先 | Unit |
| Cancellation | 已创建 Reader | 执行中取消 | Reader/lease/connection 释放，后续执行成功 | SQLite + Provider Integration |
| Procedure | Output/InputOutput/ReturnValue 混合 | Sync/Async 执行 | 只暴露输出方向，类型/null 转换正确 | Unit + Provider Integration |
| Mutation | Provider 不支持 Returning | Build/Prepare | 连接前按 DatabaseUnsupported/Gap 分类拒绝 | Provider Unit |
| Transaction | Query+Executor 同 Scope | 写后查询并 Commit/Rollback | 同事务可见性与最终持久化正确 | Provider Integration |
| Resource | Dispose without commit | Scope Dispose/DisposeAsync | Rollback exactly once，子对象失效 | Unit + Integration |
| Benchmark | 同机同 key baseline/current | FormalHost | 报告 Delta；不同 key 为 NOT_COMPARABLE | Benchmark artifact review |

## 9. 风险与缓解

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 外部数据库/CI 不可用 | Oracle/SQL Server/其他 Provider 无 real proof | 无依赖 Phase 继续；精确 BLOCKED；列所需变量/权限，不伪造通过。 |
| Oracle reset 不安全 | 破坏共享 schema | 仅固定前缀对象；无安全 schema/user 则不执行 DDL/DML。 |
| SPI Breaking 面扩大 | 第三方 Provider 编译失败 | 最小接口、CustomProvider consumer contract、migration guide、Unshipped 审计。 |
| `DbKey` 全量 rename 过大 | 大 diff、消费者迁移风险 | Phase 7 决策闸门；要么一次完成，要么冻结旧名，禁止 alias 并存。 |
| Capability 静态声明与 Driver 版本不符 | 运行时误报 | Runtime detection/Profile mismatch 测试；报告记录 Driver 版本。 |
| Procedure 跨库语义差异 | 公共 API 污染 | 通用方向模型 + Capability；Provider-specific fixture，不增加 overload。 |
| 大 Benchmark 耗时/内存 | CI 不稳定 | 独立 FormalHost/filter；普通 CI 只 Smoke；原始制品留存。 |
| 过度重构 | 回归与计划失控 | API/SPI/contract 完成后才做；小批职责拆分；每批直接测试。 |
| 历史报告数字混入 | 发布结论失真 | 当前 TRX/raw artifact 唯一来源；记录 source identity/hash。 |

## 10. 最终验收标准

### 10.1 Correctness

- Unit Tests 全绿；SQLite Integration 双 TFM 全绿。
- MySQL、PostgreSQL、SQL Server、Oracle 有 current real non-skip 证据；Doris 为 Passed 或能力明确的只读 Unsupported。
- Query/Mutation/Transaction/Cancellation/Procedure 核心合同逐 Provider 有状态和证据。

### 10.2 API / Architecture

- 根查询入口唯一；无重复 terminal/alias/无意义兼容 API。
- Provider SPI 不依赖具体 Bing Clause；无 production IVT。
- Capability 能区分 Database Unsupported、Provider Gap、Profile Missing/Mismatch。
- PublicAPI Shipped/Unshipped 整理完成，Analyzer Gate 全绿。

### 10.3 Resource

- Cancellation、IDisposable/IAsyncDisposable、Reader Dispose、Connection Ownership、Transaction Rollback、Execution Lease、Query Re-entry、AsyncLocal cleanup 全部有直接测试和至少 SQLite real proof。
- 取消后 Factory/Query/Executor 可复用，不存在状态污染。

### 10.4 Performance

- `benchmark-report.md` 存在，原始 artifact 可追溯。
- 发生热点改动的场景具有同 key before/after；无明显核心 regression。
- 不以 0 B Allocated 作为通用门槛。

### 10.5 Documentation / Reports

- Unit、Integration、Benchmark、Final 四份用户要求报告存在、UTF-8、内容一致且无敏感值。
- Getting Started、Query、Lambda、Raw、Mutation、Transaction、Multi Database、Provider Capabilities、Migration Guide 与最终 API 一致。
- `ai_docs/sql-metadata-test-traceability.md` 包含最终生产符号到测试方法映射。
- 独立 Review 无未解决 MUST_FIX/SHOULD_FIX。

### 10.6 完成状态

只有以上全部满足才可标记 `COMPLETED`。否则 Final Report 必须写：

- `BLOCKED` 或 `PARTIAL`
- 阻塞项
- 影响范围
- 是否阻塞发布
- 所需外部条件
- 建议后续动作

## 11. 下一步

通过 `/execute-plan BING-SQL-RELEASE-HARDENING-20260831`、`/run-plan` 或等价 `execute-plan` 入口开始实施。执行器从 REL-P0-01 开始，不重新规划整体方案，不自动 commit/push/PR。
