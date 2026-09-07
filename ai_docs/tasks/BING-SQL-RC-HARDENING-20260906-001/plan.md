# BING-SQL-RC-HARDENING-20260906-001 实施计划

## 0. 计划元数据与边界

- Task ID：`BING-SQL-RC-HARDENING-20260906-001`
- 日期：2026-09-07
- 优先级：P0（`Bing.Data.Sql` / `Bing.Dapper` Release Candidate 门禁）
- 当前状态：待批准实施
- 正式计划路径：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260906-001/plan.md`
- 前置基线：提交 `405e67b7`（`feat(sql): 加固Provider发布证据与RS0026契约`）及任务 `BING-SQL-RC-HARDENING-20260904-001`
- 工具链：Windows、PowerShell、.NET SDK `8.0.424`、xUnit、Dapper、BenchmarkDotNet `0.14.0`、AppVeyor
- 本轮 `/create-plan` 只新增本计划及必要目录，不修改业务源码、测试、配置、数据库、构建文件或既有报告，不执行 Git commit/push/merge/PR。

用户附件要求先写 `artifacts/plans/BING-SQL-RC-HARDENING-20260906-001-plan.md` 后继续实施；当前被明确调用的 `create-plan` 技能要求只生成 `ai_docs/tasks/<taskId>/plan.md` 并停止。因此本文件是唯一实施契约，不创建会分叉的第二份计划，也不在本轮进入实现。批准后应使用 `execute-plan BING-SQL-RC-HARDENING-20260906-001` 执行。

## 1. 当前真实状态

### 1.1 已真正实现且应复用

1. Query、Raw SQL、Lambda、Mutation、Transaction、Streaming、Multiple Result 主链已存在；Provider Profile 会进入 `SqlQueryBase` 的过程、输出参数和事务运行时 fail-fast 路径，而非仅为声明对象。
2. `ProviderContractRunner`、`ProviderCapabilityCatalog`、`ProviderReleaseEvidenceValidator/Writer`、运行元数据 sidecar 和 `Bing.ProviderEvidence.Cli` 已存在；证据链已校验 TRX、RunId、EvidenceSessionId、SourceIdentity、二进制哈希、Provider/数据库/驱动版本、TFM、时间窗口及 clean source。
3. `eng/ci/Invoke-ProviderIntegrationTests.ps1`、`Invoke-SqliteContractTests.ps1`、`Invoke-ReleaseCandidateValidation.ps1` 已存在。Provider runner 已拒绝全局 gate、其他 Provider gate/连接、默认连接、不安全数据库名、未授权 reset、零发现、零执行、失败和核心 skip。
4. MySQL 已有 Batch CRUD、失败回滚和 Multiple Result 生命周期合同；PostgreSQL 已有原生 Function 结果集及预取消合同；SQL Server 已有 Batch、事务、Procedure Output/InputOutput/ReturnValue、Multiple Result、early dispose 和 2098/2099/2100/2101 参数边界合同。
5. 外部 Provider 项目仅在非 CI 且文件存在时自动加载 Git 忽略的 `integration.runsettings.local`；受保护 CI 从专属环境变量注入，不回退 `ConnectionStrings__DefaultConnection`。
6. `common.props` 已对七个核心生产项目将 `RS0026` 提升为错误；七个生产项目均使用 `Microsoft.CodeAnalysis.PublicApiAnalyzers`，并已有 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`。
7. Benchmark 项目已有 `FormalHost`（3 launch、6 warmup、15 iteration）、MemoryDiagnoser、CI/E2E smoke 和附件点名的主要 Benchmark 类；不需要重建性能框架。
8. 现有代码未发现 Provider 生产程序集通过 `InternalsVisibleTo` 依赖 `Bing.Data.Sql` internal 实现；测试、Integration、Benchmark 的 friend assembly 策略可继续沿用。

### 1.2 已确认问题

| 优先级 | 问题 | 当前证据 | 发布影响 |
| --- | --- | --- | --- |
| P0 | PostgreSQL Capability 自相矛盾 | `PostgreSqlSqlProvider.Profile` 同时声明 `SupportsStoredProcedures=true`、`SupportsOutputParameters=true`；Catalog 的 Function 结果集有真实测试，但 Output Parameter 为 `ImplementationGap`；真实 Function 测试通过 `Sql(...) + SELECT function(...)`，没有通过 `Procedure(...)` 或 OUT/INOUT。 | `ProviderReleaseReadiness` 遇到核心 Provider 的 `ImplementationGap` 必然失败，Profile 又会放行运行时输出参数。 |
| P0 | AppVeyor lane 没有在仓库内实际 materialize | `appveyor.yml` 只按 `PROVIDER_TEST_LANE` switch，未定义 environment matrix/jobs；缺省值只运行 `common`。 | mysql/postgresql/sqlserver/release/aggregate 分支存在但不能证明会被 CI 调度。 |
| P0 | RC Unit Gate 不完整 | `Invoke-ReleaseCandidateValidation.ps1` 的 `$unitProjects` 仅含 `Bing.Core.Tests`、`Bing.Dapper.Core.Tests`、`Bing.Data.Sql.Tests`、`Bing.Test.Shared`。 | MySQL/PostgreSQL/SQL Server/SQLite/Oracle 和 CustomProvider 直接单测不在正式 RC Unit 报告内。 |
| P0 | 公共 API 尚未最终冻结 | `ISqlTransactionScopeFactory` 和实现仍公开带 optional `dbKey` 的 Shipped overload；`ToEntity*` 位于 Unshipped 且真实语义等于 SingleOrDefault；`DbKey`/`DataSourceKey` 混用。 | RS0026 历史结果为 0 不等于 API 设计已收敛；后续变更可能成为 Breaking Change。 |
| P0 | Public API Analyzer 门禁范围未统一 | `RS0026` 在 `common.props`；`RS0016/RS0017/RS0018` 仅见于部分 Provider `.csproj`，未形成七项目公共门禁。 | Public API baseline 完整性依赖项目局部配置，容易漏检 `Bing.Data.Sql` / `Bing.Dapper.Core`。 |
| P0 | 本 Task 没有新鲜发布证据 | 现有 TRX、矩阵、RS0026 日志和最终报告属于 2026-09-04 任务及旧 HEAD/旧 session；当前工作树还含与本任务无关的 `.agents/.codex/docs` 修改。 | 历史 PASS 只能作为回归线索，不能证明当前 Task/commit 为 RC READY。 |
| P0 | FormalHost 未闭环 | `artifacts/benchmarks/benchmark-report.md` 明确为 `BLOCKED / NOT COMPARABLE`；只有 DryJob 和部分元数据日志。 | 性能 Gate 不满足，不能给出无严重回归结论。 |
| P1 | RunSettings 文档与仓库事实有偏差 | 外部 Provider 已删除受跟踪 `integration.runsettings`，仅本机 `.local` 自动加载；文档仍描述仓库内公共 `integration.runsettings` 模板。 | 开发者无法准确判断本地和 CI 的配置来源。 |
| P1 | Provider 启动诊断只覆盖成功前检 | runner 有 Provider、数据库名、gate、connection configured 等结构化字段，但 reachability 只在测试成功后写为 true；preflight/连接失败主要通过异常退出。 | BLOCKED 报告未必能完整区分 Missing、Unsafe、Reset denied、Unreachable。 |
| P1 | PostgreSQL/外部 Provider cancellation 主要为预取消 | PostgreSQL Function 合同只有 pre-cancel；现有多处外部 Provider 测试也偏向 pre-cancel。 | 执行中、Streaming 中取消及取消后复用证据不足。 |
| P1 | SQL Server 上限模型只表达数据库最大值 | Profile 只有 `MaxParameterCount=2100`；真实 Dapper `IN` 展开因附加参数表现为 2098 成功、2099 起数据库拒绝。 | 若把 2100 直接当作 IN item 安全上限，会误导 planner/调用方。 |
| P1 | 文档导航与能力事实未集中 | 不存在 `docs/sql/`；能力、事务、查询和测试说明散落在多个专题文件。 | RC 用户文档无法从单一入口确认 Provider 差异和迁移方式。 |

### 1.3 待执行阶段验证的问题

- 当前 HEAD 对七个生产项目的真实 `RS0026` 数量及 RS0016/17/18 结果；规划阶段不复用旧日志宣称当前通过。
- `Procedure(...)` 在当前 Npgsql 版本下对 PostgreSQL `CALL` procedure 的真实行为；Function 结果集不能替代该验证。
- MySQL、PostgreSQL、SQL Server、SQLite 在同一 clean commit、同一 EvidenceSessionId、net6.0/net8.0 下的完整受保护运行。
- AppVeyor 对受保护 secret 的 job scope、artifact 传递和 aggregate fan-in 能否在真实平台上工作。
- FormalHost 全量默认程序集扫描是否只产生 `FormalHost` CSV，是否会混入不符合 validator 约束的 DryJob/其他 Job 报告。
- 现有 `ToEntity` 是否已有外部消费者或兼容承诺；若无正式稳定发布，可在本 RC 前做最后 Breaking Change。
- Oracle/Doris 的 Profile、Catalog、文档是否有“Supported / ImplementationGap / NotExecuted”冲突；它们不阻塞核心 RC，但不得伪造支持。

### 1.4 完成度判断

- 主体 SQL/Dapper 功能：约 90%，已过“继续大规模加功能”的阶段。
- Provider 合同实现：约 80%，核心缺口是 PostgreSQL Capability 语义及取消/边界的定向补强。
- CI 与 Evidence 基础：约 75%，fail-closed 组件已存在，但 CI job 编排、完整 Unit 输入和同源正向链未获证明。
- Public API 治理：约 75%，baseline 和 RS0026 gate 已存在，Transaction/terminal 命名与 RS0016-18 公共门禁尚未冻结。
- Benchmark 与最终报告：约 35%，框架完整但本版本 FormalHost 结果缺失。
- 当前 RC 状态：`NOT READY`。直接阻塞项为 PostgreSQL Capability 冲突、CI lane 不可证明、完整 release evidence 缺失和 FormalHost 未完成。

## 2. 关键设计决策

### 2.1 PostgreSQL Procedure / Function / Output 语义

1. 不把 `SELECT function(...)` 返回的结果集包装成 OUT/InputOutput 参数。
2. `SupportsOutputParameters` 表示当前统一 `Procedure(...)` + 参数方向 API 能否真实返回 OUT/InputOutput，而不是数据库是否存在相似概念。若当前 Npgsql/框架路径不能提供该合同，PostgreSQL 必须设为 `false`，失败原因优先复用 `DatabaseUnsupported`；只有数据库能力存在而框架未实现时才使用 `ProviderImplementationGap`。
3. `SupportsStoredProcedures` 只表示 `CommandType.StoredProcedure` / `Procedure(...)` 当前执行链已经由真实 PostgreSQL procedure integration 证明。Function 的文本 SELECT 能力应在 Catalog/文档中单列，不得借此自动把 `SupportsStoredProcedures` 判为 true。
4. 如果现有二布尔模型无法同时表达“Function result set supported、Procedure command 状态、OUT parameter unsupported”，先评估最小可扩展枚举/能力字段；只有能减少真实歧义时才改公共 Profile，且必须同步 PublicAPI、迁移文档和直接单测。

### 2.2 RunSettings 与秘密

- 不恢复带连接信息的 tracked `integration.runsettings`。
- `.local` 仅用于本机，必须继续 Git ignore；CI 仅使用受保护的 Provider 专属 secret。
- 统一入口优先扩展现有 `Invoke-ProviderIntegrationTests.ps1` 的显式 `-Settings` 本地模式或增加不复制验证逻辑的薄封装。受保护 CI 模式不得加载 `.local`。
- 所有诊断只输出配置状态和经安全校验的测试库名，不输出完整连接串、用户名、主机、端口、密码、Token。

### 2.3 Public API 与 Breaking Change

- 根 Query API 冻结为 `Query/Sql/SqlInterpolated/Procedure/From/FromSubquery`，不增加同义入口。
- `ISqlTransactionScopeFactory` 优先改为显式短重载和完整重载，公共参数使用 `dataSourceKey`；Shipped API 的删除/重命名必须在本 RC 前完成并记录迁移。
- `ToEntity` 与 `SingleOrDefault` 只能选择一组稳定公共名称。基于当前真实语义，推荐将仍为 Unshipped 的 `ToEntity/ToEntityAsync` 收敛为 `SingleOrDefault/SingleOrDefaultAsync`；如因项目既有命名体系选择保留 `ToEntity`，必须通过 API contract 明确禁止再加同义 alias。
- 不通过 `NoWarn`、pragma、SuppressMessage、`.editorconfig` 降级或 Analyzer severity 变更消除诊断。

### 2.4 Evidence 与发布结论

- 复用现有 Runner、Catalog、Writer、Validator、CLI；不创建第二套 Evidence Framework。
- dirty source、TestGenerated、DryJob、Executed=0、Failed>0、CoreSkipped>0、缺 TRX/哈希/版本/session/同源身份一律不能形成 `ReleaseEvidence` 或 `RC READY`。
- 本地 dirty 工作树可以完成实现和 TestGenerated 回归，但最终 release gate 必须由用户提交后的 clean protected CI 运行；本计划不授权自动 Git 操作。
- Oracle 为 non-blocking，Doris 为 non-blocking/read-only；二者的自然 Unsupported/NotExecuted 可以保留，但状态必须与 Profile、Catalog 和文档一致。

## 3. 分阶段实施任务

## Phase A：建立本 Task 的可审计基线

### T01（P0）固定源码身份、配置与历史证据边界

- 目标：阻止 9 月 4 日历史报告或旧二进制被误当作本任务证据。
- 现状/证据：HEAD 为 `405e67b7`；工作树存在用户的 Agent workflow 相关修改；旧报告均属于旧 Task/session。
- 已确认文件：`global.json`、`common.props`、`framework.props`、`framework.tests.props`、`Bing.All.sln`、现有 `artifacts/**`、当前 Git status/diff。
- 修改范围：仅新增本 Task 的报告目录和基线元数据；不得覆盖旧 Task 制品。
- 实施步骤：记录 HEAD、branch、完整 dirty/clean 状态、SDK/runtime/OS；为所有制品统一 TaskId、RunId、EvidenceSessionId、SourceIdentity、SourceState；历史制品只标记 Historical。
- 依赖：无。
- 验证：`git status --short`、`git diff --check`、`git rev-parse HEAD`、`dotnet --version`、`dotnet --info`。
- 风险：将用户未提交文件误归入本任务；不得 reset/clean/checkout。
- 验收标准：每个后续证据可追溯到本 Task 和源码状态；最终报告不引用历史 PASS 作为当前 PASS。

### T02（P0）生成真实 Analyzer / Public API inventory

- 目标：用当前树构建输出确认 RS0026 和 RS0016/17/18，而非源码搜索猜测。
- 现状/证据：旧任务记录七项目 RS0026=0；当前仅 RS0026 在公共 props，RS0016/17/18 配置不统一。
- 已确认文件：`common.props`、七个生产 `.csproj`、十四个 `PublicAPI.*.txt`、`SqlQueryApiContractTest.cs`、`TransactionApiContractTest.cs`。
- 修改范围：`artifacts/reports/rs0026-inventory.md/json`、构建日志；若有诊断，由 T06 修改对应源码和 baseline。
- 实施步骤：逐个 Release build 七个生产项目；记录 Project、Type、Member、File、Line、Overload Family、诊断、处置；同时核对 RS0016/17/18。
- 依赖：T01。
- 验证：对 `Bing.Data.Sql`、`Bing.Dapper.Core`、MySql、PostgreSql、SqlServer、Sqlite、Oracle 的真实 `.csproj` 逐一执行 `dotnet build <project> -c Release --no-restore --nologo`；首次缺 restore 时先执行仓库级 `dotnet restore .\Bing.All.sln`。
- 风险：多次 build 的同一诊断被重复计数；按 Project+TFM+ID+File+Line 去重。
- 验收标准：inventory 覆盖 7/7 项目和全部诊断；RS0026 最终为 0；无 suppress；报告可由 RC 脚本机器校验。

## Phase B：Capability 真相一致性

### T03（P0）修正 PostgreSQL Procedure/Function/Output Profile

- 目标：使 Profile、运行时 gate、Catalog、Integration 和文档表达同一语义。
- 现状/证据：Profile 放行 Output，Catalog 标记 ImplementationGap，Function 测试仅验证结果集。
- 已确认文件：`PostgreSqlSqlProvider.cs`、`SqlProviderProfile.cs`、`SqlQueryBase.cs`、`SqlQueryBase.Transaction.cs`、`SqlProcedureQuery.NonGeneric.cs`、`ProviderReleaseEvidence.cs`、`PostgreSqlProcedureContractTest.cs`、`DatabaseScript.cs`、`PostgreSqlProviderRegistrationTest.cs`。
- 候选文件：新的 PostgreSQL procedure fixture/test；仅在二布尔模型确实不足时修改 `SqlProviderProcedureCapabilities`。
- 修改范围：PostgreSQL Profile、过程/Function fixture、Capability runtime gate、Catalog 及对应 Provider/共享直接测试。
- 实施步骤：先增加 Procedure API 的真实成功/失败探针；按 2.1 决策设置 `SupportsStoredProcedures`；将 `SupportsOutputParameters=false` 并给出准确 FailureReason；将 Function result、Procedure command、Output parameter 分开建 Catalog scenario；验证运行时在建连/执行前对不支持输出参数 fail-fast。
- 依赖：T01。
- 验证：PostgreSQL Provider 直接单测、`Bing.Data.Sql.Tests` Profile/Capability 测试、PostgreSQL integration 双 TFM；完整 SQL 输出断言必须比较完整字符串。
- 风险：Npgsql 版本对 `CommandType.StoredProcedure` 的语义变化；测试和文档必须记录 driver version，不能用 Function SELECT 替代 Procedure 结论。
- 验收标准：不存在 `Profile Supported + Runtime reject + Catalog ImplementationGap`；Function 结果集未伪造成 OUT；核心 Catalog 不再因框架自相矛盾而阻塞 readiness。

### T04（P0）审计六 Provider Capability 一致性

- 目标：统一 SQLite/MySQL/PostgreSQL/SQL Server/Oracle/Doris 的 Profile、runtime、unit、integration、Catalog、Evidence、文档。
- 现状/证据：SQLite procedure/output 明确 Unsupported；MySQL/SQL Server 有真实过程输出合同；Oracle Profile 声明支持但 Catalog fixture gap；Doris Catalog 含非只读能力 gap。
- 已确认文件：五个官方 `*SqlProvider.cs`、Doris integration 工程、`ProviderReleaseEvidence.cs`、`ProviderCapabilityEvidence.cs`、Provider 单测与集成测试。
- 候选文件：Provider capability 文档、按 Provider 新增的职责级 Profile test；不新增第二套状态枚举。
- 修改范围：六 Provider 的 Profile/Catalog 映射、受影响 Provider 直接测试、共享 readiness 测试与能力文档。
- 实施步骤：建立逐能力差异表；核心 Provider 的 Supported 必须绑定直接单测和真实 integration method；Oracle 无安全 fixture 时标为 non-blocking/NotExecuted，不把未知写成支持证据；Doris 只要求 read-only/MySQL protocol 合同，mutation/transaction/procedure 标为不适用或 Unsupported。
- 依赖：T03。
- 验证：各 Provider 单测项目 net6/net8；`Bing.Test.Shared` Catalog 唯一键/状态/序列化/readiness 测试。
- 风险：把数据库天然差异误判为 Framework gap；每个 Unsupported 必须有原因和用户可见说明。
- 验收标准：六 Provider 差异矩阵无相互矛盾状态；Oracle/Doris 不进入核心 release blocker 集合。

## Phase C：Public API 与 Analyzer 最终冻结

### T05（P0）完成 Transaction 与 Terminal API 决策

- 目标：在修 Analyzer 前冻结 API 语义，避免机械删 optional 参数。
- 现状/证据：Transaction Shipped overload 仍带 `dbKey=null`；ToEntity 位于 Unshipped，内部调用 QuerySingleOrDefault；DbKey/DataSourceKey 混用。
- 已确认文件：`ISqlTransactionScopeFactory.cs`、`SqlTransactionScopeFactory.cs`、`SqlQuery/SqlTextQuery/SqlFluentQuery/SqlLambdaQuery` terminal 文件、PublicAPI baseline、API contract tests。
- 候选文件：迁移指南、Release Notes；不引入 Options，除非 overload 数量仍无法形成清晰族。
- 修改范围：Transaction 接口与默认实现、四类 Query terminal、PublicAPI baseline、反射合同和迁移说明。
- 实施步骤：将 Begin/BeginAsync 收敛为无键、dataSourceKey、dataSourceKey+IsolationLevel 的显式重载；完成 ToEntity 二选一；新公开参数统一用 `dataSourceKey`；保留根 Query API 六入口。
- 依赖：T02。
- 验证：反射测试断言方法集合、参数顺序、optional metadata、CancellationToken；同步编译消费测试。
- 风险：Shipped API Breaking Change；必须在 RC 前完成并提供旧到新映射。
- 验收标准：无同义 terminal；Transaction overload 完整且无歧义；PublicAPI diff 与迁移文档一致。

### T06（P0）清零 RS0026 并统一 RS0016/17/18/26 公共 Gate

- 目标：七项目公共 API Analyzer 均为 build gate。
- 现状/证据：RS0026 已公共化，RS0016-18 仍主要在 Provider 项目局部声明。
- 已确认文件：`common.props`、七个生产 `.csproj`、PublicAPI baseline、T02 inventory。
- 修改范围：七项目公共 Analyzer 配置、T02 诊断涉及的 API 源码、PublicAPI baseline、API contract tests 和 gate 制品。
- 实施步骤：根据 T02 逐条修复，不 suppress；将 RS0016/17/18/26 对目标七项目集中配置；更新 Shipped/Unshipped；生成机器可验证 API gate JSON 和人类可读 inventory。
- 依赖：T05。
- 验证：七项目 Release build；`SqlQueryApiContractTest`、`TransactionApiContractTest` net6/net8；`Bing.Data.Sql.Analyzers.Tests` net8。
- 风险：把所有仓库项目突然纳入同等门禁造成无关扩面；公共条件仍限定 SQL/Dapper 七生产项目。
- 验收标准：RS0016/17/18/26 均 0 error；gate 配置唯一；PublicAPI baseline 完整；无 NoWarn/pragma/SuppressMessage。

## Phase D：RunSettings、诊断与 CI 正向链

### T07（P0）统一本地 `.local` 与 protected CI 加载方式

- 目标：让开发者明确每个 Provider 到底读取哪个配置，并保持无密。
- 现状/证据：三个外部 Provider `.csproj` 在非 CI 自动加载 `.local`；`.gitignore` 已覆盖；现有 runner 只消费环境变量。
- 已确认文件：三个 integration `.csproj`、`.gitignore`、`Invoke-ProviderIntegrationTests.ps1`、`docs/testing/database-integration-tests.md`。
- 候选文件：`eng/ci/Invoke-LocalProviderIntegrationTests.ps1` 或 runner 的 `-Settings` 参数，二选一；优先薄封装和结构化 XML 解析。
- 修改范围：Provider runner 或本地薄封装、三个 integration 项目的配置契约、self-test 与数据库集成测试文档。
- 实施步骤：明确本地/CI 两模式；验证 settings 路径在工作区且文件存在；仅加载目标 Provider 的 gate/connection/reset；CI 模式拒绝 settings；更新文档并删除“tracked 外部 Provider 模板存在”的失效描述。
- 依赖：T01。
- 验证：runner self-test 覆盖 settings 缺失/越界、CI 禁止、目标 gate、其他 Provider 污染、大小写布尔值；实际本地命令可使用 `-Settings`。
- 风险：XML 中秘密进入输出；测试只使用合成占位连接且断言日志/JSON 无敏感字段。
- 验收标准：MySQL/PostgreSQL/SQL Server 本地入口一致；CI 不读取 `.local`；Git tracked 文件无连接秘密。

### T08（P0）补齐无密 Startup Diagnostic 与精确失败分类

- 目标：Provider 未运行时也能区分 gate/config/safety/reachability，而不是只有异常文本。
- 现状/证据：成功 summary 字段较完整，但 reachability 只在成功后为 true；失败路径未统一产出 BLOCKED summary。
- 已确认文件：`Invoke-ProviderIntegrationTests.ps1`、三个 Provider fixture/Startup、`Bing.Test.Shared` gate/safety helper、Evidence DTO/CLI。
- 候选文件：共享 `ProviderStartupDiagnostic` DTO 及直接测试类；只在可消除 Provider 复制时新增。
- 修改范围：Provider preflight/summary、共享诊断 DTO、fixture/Startup 诊断接入、CLI 报告和直接测试。
- 实施步骤：形成 Provider、Assembly、TFM、ConfigSource、GlobalGate、ProviderGate、ConnectionConfigured、安全 DatabaseName、ResetAllowed、Reachable、ExecutionStatus、BlockedReason；失败状态统一映射 GlobalGateDisabled/ProviderGateDisabled/SettingsMissing/ConnectionMissing/UnsafeDatabase/ResetDenied/Unreachable/Unsupported/Optional；所有路径脱敏。
- 依赖：T07。
- 验证：每个原因的 runner/shared 单测；不连接真实数据库的 preflight self-test；真实 Provider unreachable 探针仅在安全环境执行。
- 风险：为写报告吞掉原始异常；应保留非零退出和安全错误摘要，不 catch 后假通过。
- 验收标准：失败仍 fail-closed；报告能审计为何未执行；任何制品不含 password/token/connection string。

### T09（P0）使 AppVeyor lane 真正可调度并闭环 artifact fan-in

- 目标：materialize `common/mysql/postgresql/sqlserver/release`，并明确 aggregate 是同 job 顺序聚合还是跨 job 下载后聚合。
- 现状/证据：当前 switch 分支存在，但无 matrix；默认只有 common。RC script 已能串行运行完整流程，更适合由单一 protected `release` job 形成同 session 证据。
- 已确认文件：`appveyor.yml`、三个 CI 脚本、Evidence CLI。
- 候选文件：仅在 AppVeyor 原生 job matrix 无法表达依赖时新增 `eng/ci` 的 artifact aggregation 薄脚本。
- 修改范围：AppVeyor job/matrix、受保护变量作用域、artifact 发布/下载/聚合调用及相关脚本契约测试。
- 实施步骤：定义实际 environment matrix；common 不接触 secrets；各 Provider job 只获得自身 secret；推荐 release job 串行生成同一 EvidenceSessionId 的全部 Provider/SQLite/unit/analyzer/benchmark/aggregate，aggregate-only job 只消费已下载且哈希可验的制品；校验 branch/PR secret policy。
- 依赖：T07、T08。
- 验证：YAML 解析、PowerShell AST parse、runner self-test、无 secret smoke、AppVeyor 真实 job 列表和受保护 dry run。
- 风险：matrix 的多个 job 共享不了本地文件；不得假定工作区或环境变量跨 job 保留。
- 验收标准：每个 lane 在 CI 页面真实出现；release/aggregate 有清晰依赖；缺制品/secret/session 时失败而非跳过。

### T10（P0）修复 RC Validation 输入集合与脚本契约

- 目标：让一个 clean protected release job 可实际走通 Build→Unit→Provider→SQLite→Analyzer→API→FormalHost→Aggregate。
- 现状/证据：脚本已有主链，但 Unit 仅列 4 个工程；FormalHost 完整性目前只检查至少一个 CSV/MD/HTML，manifest 固定声明 3/6/15。
- 已确认文件：`Invoke-ReleaseCandidateValidation.ps1`、`Bing.All.sln`、全部 Unit `.csproj`、Benchmark entry、CLI validators。
- 修改范围：RC validation 的 Unit/Analyzer/API/FormalHost 输入清单、阶段摘要、manifest 生成和 CLI validator 测试。
- 实施步骤：补齐 Data.Sql、CustomProvider、Dapper Core 和五 Provider Unit 项目；明确 Analyzer tests 是否纳入 common 而非 release unit report；逐 TRX 验证 Discovered/Executed/Passed/Failed/Skipped；FormalHost manifest 从实际 report/job 解析而不是仅写常量；RC 脚本输出最终 summary，即使失败也保留已完成的无密阶段状态。
- 依赖：T06、T09。
- 验证：脚本 AST、合成制品 fail-closed 测试、无密环境预检；真实 release job。
- 风险：全量 FormalHost 与双 TFM tests 运行时间长；可以分阶段产生制品，但 readiness 只能由同源完整集合给出。
- 验收标准：用户要求的 Unit 项目全部进入报告；任一阶段失败则 ReleaseReady=false；成功链不依赖历史目录。

## Phase E：职责级测试与核心 Provider 实跑

### T11（P1）补齐本轮改动的直接单元测试和追溯映射

- 目标：满足项目对接口、默认实现、Provider 分支、缓存/SQL/Builder 的直接测试门槛。
- 现状/证据：已有大量综合测试和部分职责级测试，但 PostgreSQL procedure/output profile 缺直接断言，最终生产符号映射尚未针对本 Task 完成。
- 已确认文件：各 Provider Tests、`Bing.Data.Sql.Tests`、`Bing.Dapper.Core.Tests`、`Bing.Test.Shared`、`ai_docs/sql-metadata-test-traceability.md`。
- 修改范围：仅修改 T03-T10 真实生产 diff 对应的职责级测试、共享脚本 self-test 和最终追溯文档。
- 实施步骤：按实际 diff 为 Profile、runtime gate、runner、RC script contract、readiness、API overload 新增独立测试；涉及 SQL 输出必须断言完整 SQL；涉及缓存/clone 时覆盖 hit/miss、隔离、重复渲染并更新 Benchmark 或说明不适用。
- 依赖：T03-T10。
- 验证：所有受影响 Unit 项目 net6/net8；PowerShell self-test；符号→项目→类→方法表逐项核对。
- 风险：为数量复制 SQLite happy path；只补职责、边界、异常和状态转换。
- 验收标准：每个最终生产符号至少映射一个直接测试方法；接口契约、默认实现、每个受影响 Provider 分支均有直接证据。

### T12（P0）四核心 Provider 双 TFM 真实执行

- 目标：在安全测试库实际执行 SQLite/MySQL/PostgreSQL/SQL Server，证明不是 all-skipped 假通过。
- 现状/证据：旧任务曾在 dirty source 上真实通过 MySQL 59/59、PostgreSQL 43/43、SQL Server 21/21+1 optional、SQLite 合同，但不能作为本 Task Release Evidence。
- 已确认文件：四 integration 项目、三个 Provider runner、SQLite runner、各 fixture/database script。
- 修改范围：四 Provider integration fixture/test、运行结果目录、TRX/summary/metadata/matrix 制品；生产代码仅在真实失败定位后进入对应任务修改。
- 实施步骤：先在 dirty/local 模式做 TestGenerated 回归；确认数据库名安全和 reset 授权后运行 net6/net8；最终在 clean protected CI 使用同一 session 重跑；记录数据库/驱动/runtime/OS/TFM；失败后验证 query/connection/transaction 可复用和清理。
- 依赖：T03、T08、T10、T11。
- 验证：`Invoke-SqliteContractTests.ps1` 双 TFM；`Invoke-ProviderIntegrationTests.ps1 -Provider <MySql|PostgreSql|SqlServer> -Framework <net6.0|net8.0> -Configuration Release`；本地按 T07 显式 Settings，CI 由 secret 注入。
- 风险：fixture 会重建/清理对象；仅允许专用 `_test/_tests/_integration/_integration_test` 数据库并显式 reset 授权，禁止生产/共享库。
- 验收标准：每个 Provider/TFM `Discovered>0, Executed>0, Failed=0, CoreSkipped=0`；OptionalSkipped 有白名单和原因；四核心均有新鲜 TRX。

### T13（P1）Cancellation、资源复用与 SQL Server 参数边界定向补强

- 目标：补齐真实风险，不追求机械测试数量。
- 现状/证据：预取消较多；SQL Server 已证明 2098 成功、2099/2100/2101 拒绝，Profile 仍只有数据库最大 2100。
- 已确认文件：四 Provider Streaming/Cancellation/Transaction tests、`ParameterLimitManager`、`SqlMutationBatchPlanner`、SQL Server boundary tests。
- 候选文件：内部 command parameter budget/validator；只有真实 planner 路径需要提前拒绝时新增。
- 修改范围：受影响 Provider 的 cancellation/resource integration tests、参数预算/批规划直接单测及必要的内部校验实现。
- 实施步骤：覆盖 execute 前、command 执行中、streaming 中取消，以及取消后 Query/Connection/Transaction cleanup；driver 无稳定模拟时标 NeedsVerification；区分 Database MaxParameters=2100 与 IN expansion safe budget/final command count，不把 Profile 直接改成 2098。
- 依赖：T12 的基础环境可运行。
- 验证：各 Provider 职责级 unit+integration；SQL Server 2098/2099/2100/2101 完整行为；失败后复用。
- 风险：执行中取消易 flaky；使用数据库原生可控等待、明确 timeout，并把不稳定场景移出 blocking gate而非降低断言。
- 验收标准：核心 cancellation/cleanup 有稳定直接证据；参数模型不会错误承诺 2100 个 IN item。

## Phase F：Evidence、Benchmark、文档与发布结论

### T14（P0）生成同源 Provider Evidence 与能力矩阵

- 目标：从 T12 的真实 TRX 生成唯一、可审计、fail-closed 的矩阵和报告。
- 现状/证据：CLI/validator 已具备基础；旧矩阵 57+ 场景但全是历史/NotVerified，PostgreSQL Output gap 阻塞。
- 已确认文件：`ProviderCapabilityEvidence.cs`、`ProviderReleaseEvidence.cs`、`ProviderContractRunner.cs`、CLI、三个 runner、聚合报告模板。
- 修改范围：现有 Evidence DTO/validator/writer/catalog、CLI 聚合、runner 调用、共享测试和本 Task 报告制品。
- 实施步骤：校验 Provider/数据库/驱动/runtime/OS/commit/session/source/binary hash/TRX/test method/timestamp；聚合四 Provider×双 TFM；生成 `provider-capability-matrix.md/json`、unit/integration reports；状态统一为 PASS/FAILED/NOT VERIFIED/BLOCKED/UNSUPPORTED/IMPLEMENTATION GAP。
- 依赖：T04、T10、T12。
- 验证：共享 validator tests 双 TFM、CLI build、正/负合成制品、真实聚合。
- 风险：单 Provider matrix 被误读为整体 ready；局部输出只能叫 ProviderRunReady，整体 ReleaseReady 只在 8 个核心运行和所有 gate 完整时为 true。
- 验收标准：8/8 唯一核心运行、同 session/source、每个 RealIntegrationProven 绑定真实 passed method；任何缺失均 ReleaseReady=false。

### T15（P0）完成 FormalHost 与性能回归判定

- 目标：生成完整可比较的正式性能制品，而不是 DryJob。
- 现状/证据：主要类和 FormalHost job 已存在；当前报告明确 blocked，旧历史 baseline 环境也与当前 SDK/runtime 不同。
- 已确认文件：`Bing.Data.Sql.Benchmarks.csproj`、Benchmark entry、附件点名七类 Benchmark、现有 `benchmark-report.md`、CLI FormalHost validator。
- 候选文件：只在缺少真实热点场景时扩展现有类；不另建 Benchmark framework。
- 修改范围：Benchmark 启动筛选/manifest/validator、受本轮生产改动影响的既有 Benchmark 场景及正式报告。
- 实施步骤：先修正 runner/filter/manifest 契约；在固定主机执行现有正式类；覆盖 cache hit/miss、query plan/clone、parameters 10/100/1000/2100、batch、streaming、transaction、scope、diagnostics 中现有可稳定场景；记录 CPU/OS/runtime/BDN；有同 key baseline 才计算 ratio/delta。
- 依赖：T10、T11。
- 验证：`dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --artifacts <受控目录>`；validator 校验 raw/log、CSV、GitHub Markdown、HTML、Job/iteration 和哈希。
- 风险：默认 assembly scan 运行时间极长或混入 DryJob；应使用显式 approved type/filter manifest，不能降低 FormalHost 参数冒充正式结果。
- 验收标准：完整 FormalHost manifest 和报告；无 critical regression，或明确 BLOCKED/FAILED；不以 0 GC 为全局目标，不在无数据时引入池化/Span/unsafe 优化。

### T16（P1）收敛 RC 文档与迁移说明

- 目标：让用户从单一入口理解 Query、Mutation、事务、Streaming、Multiple Result、Provider 差异、测试方式和 Breaking Change。
- 现状/证据：专题文档存在但无 `docs/sql/`；测试文档含失效的 tracked runsettings 描述。
- 已确认文件：现有 `docs/sql*.md`、`docs/sqlquery*.md`、`docs/testing/database-integration-tests.md`、`docs/migrations/sql-transaction-api-vNext.md`、`docs/ReleaseNotes.md`。
- 候选文件：`docs/sql/README.md`、`provider-capabilities.md`、`postgresql-functions.md`、`migration-guide.md` 及必要主题页；优先链接/迁移现有内容，不重复维护全文。
- 修改范围：RC SQL 文档导航、Provider Capability/PostgreSQL Function、RunSettings、Breaking Change 迁移和 Release Notes。
- 实施步骤：建立导航；明确 PostgreSQL Function result/RETURNING 与 MySQL/SQL Server OUT 参数差异；记录 Transaction/ToEntity 决策；说明 `.local`/CI；标注 Oracle non-blocking、Doris read-only。
- 依赖：T03-T07、T14。
- 验证：链接检查、命令与真实脚本参数对照、Capability 表与 JSON 自动比对。
- 风险：一次性重写全部文档扩大范围；只新增 RC 必需入口并复用专题文档。
- 验收标准：文档不再承诺不存在的模板或能力；所有 Breaking Change 有迁移示例；矩阵与文档一致。

### T17（P0）执行最终 RC Gate 并生成报告

- 目标：只根据当前 clean commit 的同源证据给出 RC READY 或明确 BLOCKED。
- 现状/证据：旧最终报告为 `RC NOT READY / BLOCKED`，不能覆盖本 Task。
- 已确认文件：`Invoke-ReleaseCandidateValidation.ps1`、全部本 Task 制品、Capability matrix、Benchmark report、traceability。
- 修改范围：`artifacts/reports/BING-SQL-RC-HARDENING-20260906-001-final-report.md` 及本 Task 报告目录。
- 实施步骤：运行完整 protected release job；聚合 Build、Analyzer、Unit、Integration、Evidence、Benchmark、API、docs；分类新增/修改/删除/重命名文件；列出每个重要文件的原因、Breaking Change、Provider 影响；记录无法满足的外部条件但继续完成其他项。
- 依赖：T01-T16。
- 验证：`Invoke-ReleaseCandidateValidation.ps1 -Configuration Release -ResultsRoot artifacts/release-candidate/<本Task运行目录>`；`git diff --check`；人工复核秘密扫描和生产符号→测试方法映射。
- 风险：本计划禁止自动 commit，因此 Executor 结束时工作树通常为 dirty，无法自行生成最终 ReleaseEvidence；应完成代码和 TestGenerated 回归后，明确等待用户提交并由 protected CI 产生最终结论。
- 验收标准：全部 P0 gate 同时满足才写 `RC READY`；否则报告为 `BLOCKED/NOT READY` 并列出 Provider、Gate、配置状态、执行计数、原因和所需外部条件。

## 4. 修改范围总览

### 4.1 已确认会修改或生成

- `framework/src/Bing.Dapper.PostgreSql/**/PostgreSqlSqlProvider.cs`
- `framework/tests/Bing.Dapper.PostgreSql.Tests/**`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/**`
- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
- `framework/tests/Bing.Test.Shared/*Test.cs`
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- `eng/ci/Invoke-ReleaseCandidateValidation.ps1`
- `appveyor.yml`
- `common.props`
- 七个生产项目的 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`（仅按真实 API diff 更新）
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlTransactionScopeFactory.cs`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
- Query terminal 文件及 `SqlQueryApiContractTest.cs` / `TransactionApiContractTest.cs`
- `docs/testing/database-integration-tests.md`、迁移/能力/Release Notes 文档
- `ai_docs/sql-metadata-test-traceability.md`
- 本 Task 的 `artifacts/reports`、`artifacts/test-results`、`artifacts/provider-test-results`、`artifacts/benchmarks` 制品

### 4.2 候选文件，需证据触发后再修改

- `SqlProviderProfile.cs`：仅当现有 Procedure 二布尔模型无法准确表达语义时扩展。
- `SqlQueryBase.cs` / `SqlQueryBase.Transaction.cs`：仅当 PostgreSQL fail-fast 或参数方向 gate 不准确时修改。
- `SqlProcedureQuery.NonGeneric.cs`：仅当公共 Procedure 语义需调整时修改。
- `ParameterLimitManager` / `SqlMutationBatchPlanner`：仅当 final command parameter count 需要框架提前校验时修改。
- 本地 Integration 薄封装脚本：仅当现有 runner 无法安全支持显式 `-Settings` 时新增。
- Benchmark 类：仅当既有类不能覆盖本轮修改的性能风险时扩展，并说明适用原因。

### 4.3 明确不做

- 不新增第二套 Query API、Capability 状态体系、Evidence Framework 或 Benchmark Framework。
- 不为 Oracle/Doris 伪造核心 parity，不让 Oracle 阻塞四核心 Provider RC。
- 不进行与当前 gate 无关的 `SqlBuilderBase`/`SqlLambdaQuery` 大规模拆分。
- 不删除失败测试、不降低断言、不吞异常、不放宽数据库安全规则、不增加 production friend assembly。
- 不自动执行 Git add/commit/push/merge/PR。

## 5. 验证矩阵与最终门禁

### 5.1 Build / Analyzer

- 七个 SQL/Dapper 生产项目 Release build 全部 PASS。
- RS0016、RS0017、RS0018、RS0026 为 0 error，RS0026 inventory 覆盖 7/7 项目。
- PublicAPI baseline 与反射合同 PASS；无 suppress。

### 5.2 Unit

- `Bing.Data.Sql.Tests`、`Bing.Data.Sql.CustomProvider.Tests`、`Bing.Dapper.Core.Tests`、MySQL/PostgreSQL/SQLServer/SQLite/Oracle Tests、`Bing.Test.Shared` 在 net6.0/net8.0 全部 PASS。
- `Bing.Data.Sql.Analyzers.Tests` 按项目支持 TFM PASS。
- 最终维护“生产符号→测试项目→测试类→测试方法→行为”追溯表。

### 5.3 Integration

| Provider | Release blocking | 必须 TFM | Gate |
| --- | --- | --- | --- |
| SQLite | 是 | net6.0、net8.0 | Executed>0、Failed=0、CoreSkipped=0、ReleaseEvidence Valid |
| MySQL | 是 | net6.0、net8.0 | 同上 |
| PostgreSQL | 是 | net6.0、net8.0 | 同上；Function/Procedure/Output 语义一致 |
| SQL Server | 是 | net6.0、net8.0 | 同上；参数边界和资源复用通过 |
| Oracle | 否 | 能运行则记录 | Limited/Blocked 不阻塞核心 RC |
| Doris | 否 | 能运行则记录 | ReadOnly 合同，不要求 mutation/transaction/procedure parity |

### 5.4 Evidence / Benchmark / 文档

- 8 个核心 Provider/TFM run 同一 EvidenceSessionId 和 SourceIdentity，TRX、binary manifest、artifact hash、版本、方法、时间均有效。
- Unit、Integration、Capability matrix Markdown/JSON 和最终报告生成且无敏感信息。
- FormalHost 完整完成，报告含 Mean/Median/StdDev/Ratio/Allocated/Gen0/Gen1/Gen2；无可比 baseline 时明确 `NOT COMPARABLE`，不得虚构 delta。
- 文档与最终 Capability/API/RunSettings 一致，Oracle/Doris 发布定位明确。

## 6. Breaking Change 清单

| 候选变更 | 推荐决策 | 迁移策略 |
| --- | --- | --- |
| `ISqlTransactionScopeFactory.Begin(string dbKey = null)` 系列 | 改为显式 overload，参数命名 `dataSourceKey` | 提供旧签名→新签名表；无参调用迁移到 `Begin()`，有键调用到 `Begin(dataSourceKey)`。 |
| `BeginAsync` optional dbKey/cancellationToken 组合 | 增加无键+token、key+token、key+isolation+token 的明确族 | 所有异步重载保持 `cancellationToken` 名称和末位顺序。 |
| Unshipped `ToEntity*` | 推荐在冻结前改为 `SingleOrDefault*` | 一次性更新调用、PublicAPI、文档；不保留永久同义 alias。若最终保留 ToEntity，则记录为明确决策并禁止 alias。 |
| PostgreSQL `SupportsOutputParameters` | 改为 false 并给出准确失败原因 | 这是声明纠正；依赖错误声明的调用改为 Function result/RETURNING 或数据库原生过程方案。 |
| Procedure capability 模型 | 默认不扩公共模型；确有必要才增加精确字段/枚举 | 同步 PublicAPI、Provider 初始化、snapshot、runtime bridge、所有 Provider 直接测试与文档。 |

## 7. 风险与阻塞处理

1. 外部数据库不可达：该 Provider 标 `BLOCKED`，记录配置状态、reachability、计数和所需条件；继续完成其他任务，但核心 Provider 任一 BLOCKED 时不得 RC READY。
2. clean source 要求与禁止自动 Git 冲突：Executor 不自行提交；先完成实现和 dirty/TestGenerated 验证，等待用户提交后由 protected CI 生成 ReleaseEvidence。
3. CI secret 未配置：不得回退默认连接或使用本机 `.local`；release job 明确失败并保留无密诊断。
4. FormalHost 超时：分组执行可作为诊断，但最终 manifest 只有批准的全量组均完整时才写 Complete。
5. API Breaking Change：在 RC 前完成；若发现真实外部兼容承诺，停止该项并在执行报告列出决策输入，不静默保留两套同义 API。
6. Cancellation flaky：只把可重复的 driver/database 场景纳入 blocking gate；其余标 NeedsVerification，不伪造 PASS。

## 8. Definition of Done

只有以下条件全部成立，任务才可标记完成并建议 RC READY：

1. PostgreSQL Function、Procedure、Output/InputOutput 语义在 Profile/runtime/Catalog/test/evidence/docs 一致，核心无框架自身造成的 ImplementationGap。
2. 六 Provider Capability 真相一致；Oracle non-blocking，Doris read-only/non-blocking。
3. 本地 `.local` 与 protected CI 配置链明确、无密、可复现；失败原因可审计。
4. AppVeyor 实际 materialize 所需 lanes，release/aggregate 正向链可达且 fail-closed。
5. RS0016/17/18/26 公共 gate 通过；RS0026=0；Public API/Breaking Change/baseline 完成。
6. 全部要求 Unit 项目双 TFM（适用时）PASS，并完成最终生产符号到测试方法追溯。
7. SQLite/MySQL/PostgreSQL/SQL Server 双 TFM 均 Discovered>0、Executed>0、Failed=0、CoreSkipped=0。
8. 四核心 Provider Release Evidence 均 Valid，8/8 runs 同 session/source；缺失、旧制品、dirty、哈希/版本不一致均被拒绝。
9. FormalHost 完整完成并生成可审计报告；无关键回归。
10. `unit-test-report.md`、`integration-test-report.md`、`provider-capability-matrix.md/json`、`benchmark-report.md` 和本 Task final report 均生成。
11. 文档与最终 API、Capability、RunSettings 和 Provider 发布定位一致。
12. 未泄漏秘密，未放宽数据库安全规则，未自动执行任何 Git 发布操作。

若任一 P0 外部条件尚未满足，允许实施报告为 `PARTIAL/BLOCKED`，但最终发布结论必须保持 `RC NOT READY`，并继续完成所有不依赖该条件的工作。
