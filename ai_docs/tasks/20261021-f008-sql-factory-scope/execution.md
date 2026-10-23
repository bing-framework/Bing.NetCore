<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261021-f008-sql-factory-scope
AI_EXECUTION_FINISHED_AT: 2026-10-21T01:43:26Z

# F008 实施执行报告

## 当前状态（2026-10-21）

**F008 CLOSED：已通过 Sol/high 第二轮独立验收；计划 7/7，FIX-001/002/003 CLOSED，OPEN_ACTIONABLE=0。** 独立结论见 [review.md](review.md) 与 [final-report.md](final-report.md)。下方实施与各轮修复记录保留当时状态和证据，不回写。

本报告中的原实施证据保留 Round 1 独立 Review 前的结果；下方“Review 修复记录”追加本轮新测试和最终受影响套件结果。Round 1 的 Core、SQLite Integration、EF Core 与多 Provider 结果以新增证据目录为准。

## 执行结论

已完成用户批准的五类默认 SQL 工厂 Scoped 生命周期改动及计划内回归测试、迁移说明和证据采集。状态为“实施完成，待父会话安排独立验收”；本报告不关闭总体 F008。

## 任务与基线

- 任务：`20261021-f008-sql-factory-scope`；用户明确批准默认工厂改 Scoped 并要求实施。
- 起始 HEAD：`5065c38ced05a95088e17d89d96c9a1610e37159`。
- 起始工作区：干净；起始 staged/unstaged diff 为空；无未跟踪文件。
- 旧任务 `ai_docs/tasks/20261021-merged-code-audit/` 未修改。本任务证据和报告均写入新目录。
- 历史 probe 在 `artifacts/initial-sql-scope-probe.log`、`.json` 和 `initial-scope-probe.Program.cs`；改动前两个问题均复现，未打开数据库连接。
- 改注册前新增的正式回归证据位于 `artifacts/regression-before.log` 与 `artifacts/trx/before/scope-regression-before.trx`：2 failed、0 passed。一个失败是 `ValidateScopes=true` 下首次 typed Query Builder 渲染从 root 解析 Scoped `IDataFilter`；另一个是 `ValidateScopes=false` 时请求 scope Disable 状态未影响 SQL。

## 变更摘要

- `DapperServiceCollectionExtensions.AddSqlCore` 将 `ISqlQueryFactory`、`ISqlExecutorFactory`、`ISqlMultipleQueryExecutorFactory`、`ISqlTransactionScopeFactory` 从默认 Singleton 改为 `TryAddScoped`。
- `EfCoreSqlQueryServiceCollectionExtensions.AddEfCoreSqlQueryFactory` 将 `IEfCoreSqlQueryFactory` 从默认 Singleton 改为 `TryAddScoped`，避免 EF adapter 持有 root scope 核心工厂。
- `Bing.Data.Sql/AssemblyInfo.cs` 仅为 MySQL、PostgreSQL、Oracle 测试增加 internal test access，以便直接断言执行 SQL snapshot；未增加公共 API。
- 增加核心注册、Provider 完整 SQL/参数、缺失数据源零连接调用、SQLite scope/过滤/Muation 回归；将 SQLite integration fixture 与 E2E benchmark 根工厂解析改为显式 scope。消费者构造签名未批量修改。
- `SqlFactoryBase`、`IDataFilter`、缓存键、格式化、Clone 算法、Provider SPI、包版本和 API baseline 均未改。

## Change Impact Analysis

| 项目 | 影响 |
|---|---|
| ChangedFiles | 两处生产 DI 注册、internal test access、注册/Provider/SQLite/EF 测试、SQLite fixture、E2E benchmark harness、三个任务文档与证据 |
| ChangedProjects | Dapper Core、Data.Sql、5 个官方 Provider 测试、Custom Provider 测试、SQLite integration/benchmark、EF Core、FreeSQL/Admin 验证 |
| ChangedPublicContracts | 无 |
| ChangedRuntimePaths | factory DI scope → provider runtime/EF adapter → 延迟 Builder/Mutation → scoped filter；事务与 reader 仍按既有 scope/租约工作 |
| ChangedProviders | Provider 运行逻辑未改变；新增 MySQL/PostgreSQL/SQL Server/Oracle/SQLite 直接契约断言 |
| ChangedTFMs | 受影响本地测试主要 net6.0/net8.0；EF Core 与 Admin EF 实际目标为 net6.0；生命周期 probe net8.0 Release |
| ChangedBuildPackaging | 无 |
| ChangedBenchmarkHarness | 仅 SQLite Dapper E2E benchmark 解析工厂改为持有并释放显式 scope；不改 benchmark baseline/预算 |
| RiskLevel | HIGH：改变默认 DI 生命周期并依赖请求 scope 生命周期管理 |

每轮测试按变更影响先跑注册/职责测试，再扩展至 Provider、SQLite、EF、FreeSQL/Admin。没有机械重跑上轮全仓 11796 项测试或 85 个生产项目全量。

## 最终测试结果

以下为当前变更后的最终全量或职责套件。TRX 与完整命令输出在表中路径对应的 `artifacts/trx` 和 `artifacts/*.log`。

| 项目/TFM | 结果 | 主要证据 |
|---|---:|---|
| Bing.Dapper.Core.Tests / net6.0 | 166 passed, 0 failed, 0 skipped | `dapper-core-final2-net6.log`、`trx/final/dapper-core-final2-net6.trx` |
| Bing.Dapper.Core.Tests / net8.0 | 166 passed, 0 failed, 0 skipped | `dapper-core-final2-net8.log`、对应 TRX |
| Bing.Data.Sql.Tests / net6.0 | 1317 passed, 0 failed, 0 skipped | `data-sql-net6.log`、`trx/final/data-sql-net6.trx` |
| Bing.Data.Sql.Tests / net8.0 | 1317 passed, 0 failed, 0 skipped | `data-sql-net8.log`、对应 TRX |
| Bing.Data.Sql.CustomProvider.Tests / net6.0 | 21 passed, 0 failed, 0 skipped | `custom-provider-net6.log`、`trx/final/custom-provider-net6.trx` |
| Bing.Data.Sql.CustomProvider.Tests / net8.0 | 21 passed, 0 failed, 0 skipped | `custom-provider-net8.log`、对应 TRX |
| Bing.Dapper.MySql.Tests / net8.0 | 180 passed, 0 failed, 0 skipped | `provider-mysql-final2-net8.log`、`trx/final/provider-mysql-final2-net8.trx` |
| Bing.Dapper.PostgreSql.Tests / net8.0 | 138 passed, 0 failed, 0 skipped | `provider-postgresql-final2-net8.log`、对应 TRX |
| Bing.Dapper.SqlServer.Tests / net8.0 | 295 passed, 0 failed, 0 skipped | `provider-sqlserver-final2-net8.log`、对应 TRX |
| Bing.Dapper.Oracle.Tests / net8.0 | 93 passed, 0 failed, 0 skipped | `provider-oracle-full-net8.log`、`trx/full/provider-oracle-full-net8.trx` |
| Bing.Dapper.Sqlite.Tests / net6.0 | 112 passed, 0 failed, 0 skipped | `sqlite-unit-net6.log`、`trx/full/sqlite-unit-net6.trx` |
| Bing.Dapper.Sqlite.Tests / net8.0 | 112 passed, 0 failed, 0 skipped | `sqlite-unit-net8.log`、对应 TRX |
| Bing.Dapper.Sqlite.Tests.Integration / net6.0 | 164 passed, 0 failed, 1 gated skip | `sqlite-full-final5-net6.log`、`trx/final/sqlite-full-final5-net6.trx` |
| Bing.Dapper.Sqlite.Tests.Integration / net8.0 | 164 passed, 0 failed, 1 gated skip | `sqlite-full-final5-net8.log`、对应 TRX |
| SQLite Provider evidence contract / net6.0 | 1 passed, 0 failed, 0 skipped | `sqlite-provider-contract-net6-final.log`、`artifacts/provider-contract/net6/` |
| SQLite Provider evidence contract / net8.0 | 1 passed, 0 failed, 0 skipped | `sqlite-provider-contract-net8-final.log`、`artifacts/provider-contract/net8/` |
| Bing.EntityFrameworkCore.Tests / net6.0 | 96 passed, 0 failed, 0 skipped | `efcore-full-net6.log`、`trx/full/efcore-tests-net6.trx` |
| Bing.FreeSQL.Tests / net6.0 | 76 passed, 0 failed, 26 skipped | `freesql-full-net6.log`、`trx/full/freesql-full-net6.trx` |
| Bing.FreeSQL.Tests / net8.0 | 76 passed, 0 failed, 26 skipped | `freesql-full-net8-final.log`、对应 TRX |
| Bing.Admin.EFCore.Tests / net6.0 | 140 passed, 0 failed, 36 skipped | `admin-efcore-net6.log`、`trx/full/admin-efcore-net6.trx` |
| Bing.Admin.FreeSQL.Tests / net6.0 | 150 passed, 0 failed, 35 skipped | `admin-freesql-net6.log`、对应 TRX |

SQLite 完整套件中的唯一跳过测试要求受控证据路径。随后在仓库规定的根级 `artifacts/test-results` 路径下单独完成 net6/net8 两次运行，并将 TRX 和 JSON 原样复制到本任务 artifacts，因此所有本地 SQLite 集成场景均有执行证据。初次将受控结果目录放在任务子目录时，测试在路径校验处失败、未开始场景；保留日志 `sqlite-provider-contract-net6.log`，修正到 harness 要求路径后 `...-final.log` 均通过。

SQLite scope contract 当前含 13 个执行用例（理论用例按参数展开），并由上述 net6/net8 完整套件执行。覆盖 scope 验证开关、完整过滤 SQL、enable/disable/restore、嵌套和异常恢复、跨 await、并发独立 scope、A 工厂不重绑定 B、Query 创建时和 Builder 创建时状态变化、重复渲染不累积参数、Clone/Page、Executor Mutation、raw SQL 不自动过滤、运行时 transient 直接解析和缺失数据源零连接解析调用。

Provider SQL 断言使用独立字面量完整 SQL 与参数集合；MySQL、PostgreSQL、SQL Server、Oracle 的 scope 过滤均有直接职责测试。Oracle Multiple runtime 明确保持不支持。缺失数据源错误路径通过计数 resolver 断言 SQL Server、MySQL、PostgreSQL 和 SQLite 的连接 resolver 调用为零。

SQLite fixture 的显式 scope 同时运行了既有提交/回滚、同步/异步 Begin、Query/Executor 共享事务、事务 ambient 数据源快照、活动 stream 阻止释放、取消/提前终止枚举和连接再使用测试。EF Core 套件覆盖 Shared 与 Independent 连接所有权、EF transaction、ambient context 和缺失具名连接行为。

## 生命周期 Probe 与 Benchmark Smoke

- Probe：net8.0 Release、`ValidateScopes=true`、无数据库执行、60 秒总窗口限制；实际总时间 0.4127652 秒，异常 0，连接 resolver 调用 0，连接打开 0。原始数据：`artifacts/lifecycle-probe.log`。
- `scope-create-resolve-render-dispose`：预热 100 次，132.8098 ms / 3,208,600 B；正式组各 1000 次，完成数均 1000、异常 0；组 1 34.8529 ms / 30,772,192 B，组 2 30.4468 ms / 30,794,280 B，组 3 32.9028 ms / 30,803,488 B。
- `same-scope-repeated-create`：预热 100 次，0.2873 ms / 173,576 B；正式组各 1000 次、异常 0；组 1 2.2271 ms / 1,721,416 B，组 2 2.2096 ms / 1,680,352 B，组 3 2.3148 ms / 1,680,352 B。
- Probe 只记录执行时间、分配和无连接执行事实，不设置性能通过阈值，不声称稳态 P95 或优化收益。
- SQLite Dapper E2E benchmark 使用隔离唯一名称 runner 执行 24 个 BenchmarkDotNet DryJob smoke case，24/24 完成。它验证改动后的 scope 装配和路径，不代表预算或性能基准；DryJob 的最小迭代时间提示已保留在 `artifacts/benchmark-e2e-smoke-runner.log`。
- 未更改 `Bing.Data.Sql.Benchmarks` baseline 或性能预算。本次不改映射缓存键、格式化热路径、Builder Clone 算法，相关性能基线不适用；既有 Data.Sql 职责测试保持通过。

## 生产符号到验证证据

| 生产符号/改动 | 直接测试项目、类、方法 | 证据 |
|---|---|---|
| `DapperServiceCollectionExtensions.AddSqlCore`：四类核心 factory 默认 Scoped | Bing.Dapper.Core.Tests / `DapperCoreServiceCollectionExtensionsTest`: `AddSqlCore_WhenCalledRepeatedly_ShouldKeepScopedFactoryRegistrationsIdempotent`、`AddSqlCore_WhenFactoryIsPreRegistered_ShouldPreserveTypeFactoryAndInstance`、`AddSqlCore_WhenFactoryIsRegisteredAfterCore_ShouldResolveLastRegistration` | `dapper-core-final2-net6/8.log` 与 TRX |
| 默认 Scoped 工厂的捕获 scope 与 filter 渲染路径 | SQLite Integration / `SqlFactoryScopeContractTest`: `Create_WhenScopesAreValidated_ShouldRenderFromRequestScope`、`Create_WhenScopesAreNotValidated_ShouldUseRequestScopeFilterState`、`Filter_WhenScopesRenderConcurrently_ShouldRemainIsolated`、`Factory_WhenInvokedAcrossScopes_ShouldStayBoundToResolutionScope`、`Mutation_WhenFilterChangesInOneScope_ShouldKeepOtherScopeAndRawSqlIndependent` | `sqlite-full-final5-net6/8.log` 与 TRX |
| Query 创建时数据库上下文快照 | Bing.Dapper.Core.Tests / `SqlFactoryTest.Create_WhenCurrentContextHasDataSource_ShouldPreserveContextSnapshot`；EF Core / `Create_WhenAmbientDatabaseScopeIsSet_ShouldUseScopedDataSourceSnapshot`、`Create_WhenExplicitDbKeyAndAmbientDbKeyDiffer_ShouldPreferExplicitDbKey` | Core 与 EF Core final suite |
| Provider runtime 过滤 SQL 与参数 | MySQL `Query_WhenScopedFilterChanges_ShouldRenderCompleteMySqlSql`；PostgreSQL `Query_WhenScopedFilterChanges_ShouldRenderCompletePostgreSqlSql`；SQL Server `Query_WhenScopedFilterChanges_ShouldRenderCompleteSql`；Oracle 对应 scope filter 测试；SQLite `Query_WhenRerenderedAcrossFilterCycles_ShouldNotDuplicatePredicatesOrParameters` | 各 Provider final suite、SQLite final suite |
| 缺失数据源失败在连接解析之前 | MySQL/PostgreSQL `QueryFactory_WhenNamedDataSourceIsMissing_ShouldThrowBeforeExecution`；SQL Server `SqlQueryFactory_Create_WhenNamedConnectionStringMissing_ShouldThrow`；SQLite 同名职责测试 | MySQL、PostgreSQL、SQL Server final2 和 SQLite final5 |
| `EfCoreSqlQueryServiceCollectionExtensions.AddEfCoreSqlQueryFactory` | EF Core / `EfCoreSqlQueryFactoryTest.AddEfCoreSqlQueryFactory_WhenCalledRepeatedly_ShouldRegisterScopedFactory`，及 Shared/Independent 连接所有权测试 | `efcore-full-net6.log`、TRX |
| test-only internal SQL snapshot visibility | `Bing.Data.Sql/AssemblyInfo.cs` 的 MySQL/PostgreSQL/Oracle test access | 对应 Provider 完整 SQL 断言通过；无 public API 改动 |

## 验证边界与状态

- `RUN_INTEGRATION_TESTS` 未开启的外部 MySQL、PostgreSQL、SQL Server、Oracle、Doris 数据库场景及外部 CI 未运行，状态为 `BLOCKED_EXTERNAL`；未使用生产数据库。Doris 继续复用 MySQL Provider，只读/无事务契约由既有本地注册测试覆盖。
- CodeSmith 和正式资源/性能预算矩阵未运行；本任务只执行计划中的受控本地生命周期 probe 与 DryJob smoke。没有修改 API baseline、包版本、预算或既有 benchmark baseline。
- 实施自查没有新增 OPEN_ACTIONABLE finding；独立验收尚未开始，因此不生成 review 结论，也不自动进入 Fix 循环。外部未运行项不阻挡本地实施交付。
- 本地构建输出保留了仓库既有 NETSDK1206 与部分测试源码 nullable/xUnit analyzer warnings；没有由本次生产改动引发的失败。
- 初期测试迭代中曾修正测试期望（类型全名、Oracle bind 值为 0）及 FreeSQL net8 并行构建锁问题；最终顺序运行通过。BenchmarkDotNet 初次发现仓库旧 artifacts 中有重复 runner 名称，改用本任务唯一命名 runner 后 24/24 成功；旧日志仍保留。

## 交付文件

- `plan.md`：用户批准计划，已更新为实施完成、待独立验收。
- `execution.md`：本执行报告、最终 CIA、测试矩阵、traceability 和未运行项。
- `migration.md`：Scoped 生命周期、Singleton/后台 scope、释放边界和兼容说明。
- `artifacts/`：改前失败、改后日志/TRX、Provider/SQLite 证据 JSON、生命周期 probe 与 E2E smoke 原始输出。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`；Review 文件：`ai_docs/tasks/20261021-f008-sql-factory-scope/review.md`。
- Fix Scope：`recommended`；本轮开始时间：`2026-10-21T01:17:35.967Z`。
- 执行状态：FIX-001、FIX-002、FIX-003 均已完成；等待父会话安排同一 Sol/high 独立复审。
- 实施范围：补齐真实 A/B scope、缓存创建链、兼容注册矩阵的职责测试。本轮没有修改生产实现，也没有改写 `review.md` 或 `final-report.md`。

#### FIX-001

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqlTransactionScopeFilterContractTest.cs`、`framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqlQueryFactoryScopeFilterContractTest.cs`。
- 根因：既有事务测试覆盖连接/回滚但未经过事务工厂创建 typed Query 并切换 scoped filter；既有 EF 测试没有把真实 Scoped adapter 产物、A/B filter 状态和 Shared/Independent 连接所有权放在同一 SQLite 回归中。
- 修复：新增 ValidateScopes=true 的独立 A/B scope 测试；同步与异步事务均创建 Query 和实体 Delete Executor。测试在 A 的不同 filter 状态下断言完整 SQL，执行 typed 查询和实体 Mutation，并用内容不同的 first/second SQLite 数据源及回滚后行集证明子 Query/Executor 保持 Begin 固定的数据源和事务。EF Shared/Independent 均用真实 `IEfCoreSqlQueryFactory` 产物切换 A/B filter，断言完整 SQL、参数诊断、行集及 External/Owned 连接归属。
- 方法映射：`SqlTransactionScopeFilterContractTest.Begin_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope`、`BeginAsync_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope`；`EfCoreSqlQueryFactoryScopeFilterContractTest.Create_WhenScopesAreValidated_ShouldKeepFilterStatePerAdapterScope`（Shared/Independent 两个理论用例）。
- 验证：SQLite Integration Release net6/net8 各 167 passed、0 failed、1 gated skip；EF Core Tests Release net6 100 passed、0 failed、0 skipped。TRX/完整 stdout/stderr 见 `artifacts/fix-round-1/`。

#### FIX-002

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqlFactoryCacheScopeContractTest.cs`。
- 根因：旧证据分别验证 scoped factory/filter 与 Singleton mapping resolver，未从同一 scoped factory typed Query 创建链观测冷缓存、热缓存、跨 scope 复用和动态 filter。
- 修复：在全新 ValidateScopes=true SQLite 容器中使用唯一实体及两个 DbKey/Profile。经 A 工厂执行首次 typed render 观测 miss/条目增长，再验证热命中和 B scope 共用相同 Singleton resolver/cache；不同路由映射隔离、同一映射复用均有精确断言。A filter 反复 enable/disable 只影响完整 SQL/参数，B 独立；实际 SQLite 行集随 filter 改变。未改缓存算法或键。
- 方法映射：`SqlFactoryCacheScopeContractTest.TypedRendering_WhenScopesShareCache_ShouldReuseMappingsAndReadCurrentFilters`。
- 验证：SQLite Integration Release net6/net8 全套各 167 passed、0 failed、1 gated skip。Data.Sql resolver/cache/key/Clone 算法和测试未变化，按独立 Review 证据复用，不重跑无关矩阵。

#### FIX-003

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：新增 `framework/tests/Bing.Dapper.Core.Tests/DependencyInjection/SqlFactoryRegistrationContractTest.cs`、`framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqlQueryFactoryRegistrationContractTest.cs`；扩展 `framework/tests/Bing.Dapper.SqlServer.Tests/Metadata/MultiProviderRegistrationTest.cs`。
- 根因：旧兼容注册断言集中在 QueryFactory；其他三类核心工厂、EF adapter、多 Provider 注册顺序和重复注册没有同等直接覆盖。
- 修复：四类核心工厂和 EF 工厂分别验证预注册实现类型/委托/实例及 Singleton 生命周期会被保留且实际可解析；后注册自定义实现按 last-service 解析并检查集合计数。多 Provider 测试覆盖正序/逆序两种顺序、重复 AddSqlCore/Provider、ValidateScopes=true、四工厂描述符数量与 scoped 生命周期、同/异 scope 对象身份、Singleton registry/cache 共享，以及 MySQL/PostgreSQL 具名数据源的完整 typed SQL 路由。
- 方法映射：Core `AddSqlCore_WhenEachFactoryIsPreRegistered_ShouldPreserveDescriptorAndResolvedInstance`、`AddSqlCore_WhenFactoriesAreRegisteredAfterCore_ShouldResolveLastAndRetainCollectionEntries`；EF `AddEfCoreSqlQueryFactory_WhenFactoryIsPreRegistered_ShouldPreserveDescriptorAndResolution`、`AddEfCoreSqlQueryFactory_WhenFactoryIsRegisteredAfterDefault_ShouldResolveLast`；多 Provider `AddProviders_WhenOrderIsReversedAndRegistrationsRepeat_ShouldKeepScopedFactoryContracts`。
- 验证：Dapper Core Tests Release net6/net8 各 168 passed、0 failed、0 skipped；SqlServer.Tests（含五 Provider 注册）Release net6/net8 各 297 passed、0 failed、0 skipped。

### Round 1 回归矩阵

| 项目/TFM/配置 | 通过 | 失败 | 跳过 | 最终证据 |
|---|---:|---:|---:|---|
| Bing.Dapper.Core.Tests / net6.0 / Release | 168 | 0 | 0 | `artifacts/fix-round-1/core-net6-release/` |
| Bing.Dapper.Core.Tests / net8.0 / Release | 168 | 0 | 0 | `artifacts/fix-round-1/core-net8-release/` |
| Bing.EntityFrameworkCore.Tests / net6.0 / Release | 100 | 0 | 0 | `artifacts/fix-round-1/efcore-net6-release-pass-final/` |
| Bing.Dapper.Sqlite.Tests.Integration / net6.0 / Release | 167 | 0 | 1 gated | `artifacts/fix-round-1/sqlite-integration-net6-release-final2/` |
| Bing.Dapper.Sqlite.Tests.Integration / net8.0 / Release | 167 | 0 | 1 gated | `artifacts/fix-round-1/sqlite-integration-net8-release-final/` |
| Bing.Dapper.SqlServer.Tests / net6.0 / Release | 297 | 0 | 0 | `artifacts/fix-round-1/multiprovider-net6-release-final/` |
| Bing.Dapper.SqlServer.Tests / net8.0 / Release | 297 | 0 | 0 | `artifacts/fix-round-1/multiprovider-net8-release/` |

上述命令均由 `artifacts/fix-round-1/run-test.ps1` 限时运行；每个目录保存 status.json、stdout.log、stderr.log 和原始 TRX。SQLite 唯一 skip 是受控 Provider evidence contract 的环境门控项；该契约由原 F008 net6/net8 根级 evidence run 通过，详见既有 `artifacts/provider-contract/`。未使用外部数据库或生产库。

### 失败迭代与证据复用

- 保留的早期失败目录不覆盖：`core-net6/`、`core-net6-rerun/`（测试编译缺少 using/API）；`efcore-net6-release/`、`efcore-net6-release-rerun/`、`efcore-net6-release-final/`、`efcore-net6-release-pass/`、`efcore-net6-release-parameter-diagnosis/`、`efcore-net6-release-message-filter/`、`efcore-net6-release-async-context-capture/`（依次暴露编译引用、fixture logging、断言前缀、并行诊断消息隔离和空快照公开诊断表示问题）；`multiprovider-net6-release/`（新增测试缺少 Configs 命名空间）；`sqlite-integration-net6-release-final/`（扩展事务用例后仍读取已冻结 Query 的内部 Builder）。对应测试候选修正后均由上表最新 Release 全套运行通过；失败日志/TRX保留作过程证据，不计入通过数。
- 复用原 F008 不变范围：`artifacts/review-round-1/evidence-audit.md` 所列 Data.Sql resolver/cache/key/Clone、5 Provider SQL 职责、Custom Provider、自定义 SPI、SQLite 资源矩阵、EF 既有资源测试、FreeSQL/Admin 装配、生命周期 probe 与 SQLite E2E smoke。该轮只新增测试，没有改生产实现、映射缓存算法/键、格式化、Clone、消费者、fixture、benchmark harness 或资源生命周期；因此不重跑全仓矩阵、85 个生产项目 build、probe、smoke 或独立消费者套件。
- 最终 traceability 见本节 FIX-001/002/003 的“方法映射”和各行 TRX；独立 Review 是否关闭 findings 由 Reviewer 判定。
- `review.md` 保持 NEEDS_FIX 原文，`final-report.md` 未改；父会话应安排同一 Sol/high 重新验收。本轮不关闭总体 F008，也不改变外部阻断和其他遗留状态。
- `task-finish.mjs` 收口结果：`ok=true`、`finalStatus=COMPLETED`、`mode=review-fix`、`reviewRound=1`、`notificationStatus=skipped`。

未执行 `git add`、commit、push、tag、reset、clean、PR 或 API baseline/包版本修改。保留独立审查交接给父会话。
