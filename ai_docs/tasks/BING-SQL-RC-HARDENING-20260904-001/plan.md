# BING-SQL-RC-HARDENING-20260904-001 实施计划

## 0. 计划元数据

- Task ID：`BING-SQL-RC-HARDENING-20260904-001`
- 日期：2026-09-04
- 优先级：P0（Bing.Data.Sql / Bing.Dapper RC 发布门禁）
- 计划状态：待批准实施
- 实际计划路径：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/plan.md`
- 前置任务：`BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903`
- 技术栈：.NET SDK `8.0.424`、C#、xUnit、Dapper、BenchmarkDotNet `0.14.0`、PowerShell、AppVeyor
- 本计划只定义实施契约；当前 `/create-plan` 阶段不修改业务代码、测试、配置、数据库或报告制品。批准后通过 `/execute-plan BING-SQL-RC-HARDENING-20260904-001` 进入实施。

## 1. 需求冲突与执行边界

### 1.1 路径与实施时机

用户需求原文要求先生成 `artifacts/plans/BING-SQL-RC-HARDENING-20260904-001-plan.md` 后继续实施；当前 `create-plan` 工作流和 plan-writer 模式要求唯一写入目标为默认任务计划，且计划完成后停止。因此本文件是本轮唯一正式计划，不创建第二份可能分叉的计划，也不在本轮实施。后续 Executor 可在执行报告中引用本文件，无需复制到 `artifacts/plans`。

### 1.2 RunSettings 安全策略

用户需求要求确认三个 Provider 的 `integration.runsettings`；当前仓库已采用更安全且更具体的策略：受版本控制的 `integration.runsettings` 删除，本机只在非 CI 时加载 Git 忽略的 `integration.runsettings.local`，受保护 CI 只接受 Job Secret 注入的专属环境变量。实施不得恢复包含连接信息或自动 reset 授权的跟踪文件。

### 1.3 外部数据库与破坏性操作

MySQL、PostgreSQL、SQL Server fixture 会清理、重建或截断专用测试对象。执行真实测试前必须再次确认：

- 数据库名符合 `_test`、`_tests`、`_integration` 或 `_integration_test` 安全规则；
- `ALLOW_DATABASE_RESET_FOR_TESTS=true` 是用户或受保护 CI 的明确授权；
- 不使用生产、共享开发、系统数据库或 `ConnectionStrings__DefaultConnection`；
- 日志、Markdown、TRX 摘要和 JSON 不输出完整连接字符串、用户名、密码或 Token。

## 2. 当前真实状态与完成度判断

### 2.1 已完成并应复用

1. `common.props` 已将 `RS0026` 加入 `WarningsAsErrors`；七个 SQL/Dapper 生产项目均引用 PublicApiAnalyzers。未发现 `NoWarn`、`SuppressMessage` 或 `#pragma` 对 RS0026 的规避。
2. `SqlTextQuery`、`SqlFluentQuery`、`SqlLambdaQuery` 和 Fluent/Builder 扩展已有一轮显式重载治理，`SqlQueryApiContractTest` / `TransactionApiContractTest` 已覆盖部分 optional metadata 契约。
3. `ProviderContractRunner`、`ProviderCapabilityEvidence`、`ProviderReleaseEvidenceValidator`、`ProviderReleaseEvidenceWriter` 和 `Bing.ProviderEvidence.Cli` 已存在，具备 TRX、RunId、SourceIdentity、路径、时间和 Test Method 绑定能力。
4. Provider runner 已拒绝全局 gate、默认连接、其他 Provider 变量、不安全数据库名、零发现、零执行、核心 skip、failed TRX 和历史/越界制品。
5. SQLite 受控合同已能生成绑定当前运行的 `TestGenerated` 证据，并明确不等于 `ReleaseEvidence`。
6. MySQL/PostgreSQL 已有较完整的 Query、类型、Streaming、Cancellation、Transaction 和部分 Mutation 合同；SQL Server 已新增参数/null、类型物化、Streaming/预取消、提交/回滚和 INSERT OUTPUT 最小合同。
7. BenchmarkDotNet 项目已有 FormalHost、MemoryDiagnoser、Median 和 SQLite E2E 基础，包含 Lambda Root/Join、Metadata、Mutation、Aggregate、Debug SQL 等既有场景；不需要创建第二套 Benchmark 框架。
8. `InternalsVisibleTo` 当前主要指向 Tests、Integration 和 Benchmarks，未发现 Provider 生产程序集通过 friend assembly 依赖 `Bing.Data.Sql` internal 实现的证据。

### 2.2 2026-09-04 当前真实 Provider 运行基线

| Provider | RunId / 制品 | Discovered | Executed | Passed | Failed | CoreSkipped | 当前结论 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| MySQL | `mysql-net8.0-20260904T021758416Z-f4e0d8b19ac1478b83e7bb9b4705d516` | 55 | 55 | 55 | 0 | 0 | 真执行通过，但矩阵仍有 Batch/Multiple Result `ImplementationGap`，`ReleaseReady=false`。 |
| PostgreSQL | `postgresql-net8.0-20260904T021802049Z-42709b7808e7416eb638becbe4028a2f` | 41 | 41 | 41 | 0 | 0 | 真执行通过，但 Procedure/Output 合同仍为 `ImplementationGap`，`ReleaseReady=false`。 |
| SQL Server | `sqlserver-net8.0-20260904T021808021Z-53e8b4571c274d9591f8ca6606e95308` | 12 | 11 | 10 | 1 | 1 | `FAILED`；启动配置测试受 Provider gate 环境污染，MultiProvider 测试跳过，未生成可信摘要/矩阵。 |
| SQLite | 既有受控 net8.0/net6.0 制品 | 1 | 1 | 1 | 0 | 0 | 受控合同通过，当前为 `TestGenerated`，不是核心 Provider Release Evidence。 |

SQL Server 失败根因已定位：`ConfigureServices_WhenGlobalMultiProviderRunEnabled_ShouldRegisterAllProviderDataSources` 只临时设置全局 gate，没有清除 runner 注入的 `RUN_SQLSERVER_INTEGRATION_TESTS=true`，导致 `Startup` 正确进入专属 lane 分支，只注册 `default`，测试却断言 `mysql`/`pgsql`/`sqlserver` 均已注册。这是测试环境隔离问题，不应通过放宽受保护 lane 行为修复。

### 2.3 未完成、部分完成或需要重构

| 范围 | 状态 | 依据 |
| --- | --- | --- |
| RS0026 全量清零 | 部分完成 | error gate 已存在，但本任务尚未对当前工作树执行并保存七项目/全部 TFM 的新 inventory。`ISqlTransactionScopeFactory` 等 Shipped API 仍有多个 optional overload，必须由 Analyzer 和 API 设计共同复核。 |
| Public API 最终冻结 | 部分完成 | `ToEntity<T>` 当前实际映射 `SingleOrDefault`，契约测试禁止公开 `SingleOrDefault` alias；该方向尚未形成最终 RC 决策。`DbKey` / `DataSourceKey` 命名仍混用。 |
| RunSettings / Gate | 基础完成 | `.local` 与 CI 已隔离，但缺少可审计的“testhost 实际加载、gate 状态、数据库可达”安全诊断与报告汇总。 |
| MySQL 合同 | 部分完成 | 核心 55 项真执行通过；缺 Batch Mutation、Batch Failure/Large Batch、QueryMultiple/Multiple Result、资源生命周期闭环。 |
| PostgreSQL 合同 | 部分完成 | 核心 41 项真执行通过；缺 Function/Procedure、输出语义、过程异步/取消、Streaming exception cleanup。 |
| SQL Server 合同 | 未达 RC | 当前真实 lane 有 1 fail/1 skip；缺 CRUD/Batch、Procedure 参数、QueryMultiple、共享事务、dispose without commit、流式取消恢复、参数上限边界等。 |
| Release Evidence | 部分完成 | MySQL/PG 有真实矩阵，但 `DatabaseVersion` / `DriverVersion` 为 `not-recorded`；SQL Server 无成功矩阵；SourceIdentity 仅记录 Git HEAD，不能证明脏工作树构建与提交完全一致。 |
| Unit Test | 较完整但需定向补齐 | Transaction execution mode、资源所有权、resolver、parameter limit、batch planner 已有测试；应按本轮真实改动补职责级边界测试，不以数量为 KPI。 |
| Benchmark | 阻塞 | 当前 `artifacts/benchmarks/benchmark-report.md` 明确为 `BLOCKED / NOT COMPARABLE`，本任务尚无 FormalHost 结果和 Delta。 |
| 文档 | 部分完成 | 当前 SQL 文档是多个已有专题文件，不存在用户列出的 `docs/sql/` 完整目录。不得一次性重写全部文档；先建立 RC 必需的能力、迁移、测试运行和报告入口。 |

### 2.4 整体完成度

- 代码/API 治理基础：约 80%。已有主链、Analyzer gate 和 API 契约，但缺当前树全量 inventory 与最终 API 决策。
- 核心 Provider 验证：约 65%。MySQL/PG 真执行通过，SQL Server 当前失败，四核心 Provider 尚未统一获得可发布证据。
- 证据与报告：约 55%。可信写入链存在，但运行元数据、脏工作树身份、统一报告和全 Provider 聚合尚未闭环。
- 性能门禁：约 30%。Benchmark 类型较多，但没有本任务 FormalHost 结果和可比较报告。
- RC 总体状态：`NOT READY`。直接阻塞为 SQL Server lane 失败、能力缺口、Release Metadata 不完整、Formal Benchmark 未完成。

## 3. 架构、性能、维护性与开发体验判断

### 3.1 架构与复杂度

- 复用现有 `ProviderContractRunner` / Evidence 状态体系，禁止再建第二套 Provider Contract 或报告模型。
- SQL Server `Startup` 同时承担专属 Provider lane 与本地全局兼容模式，容易受进程环境变量污染。应先隔离测试环境变量与配置决策；只有重复逻辑明显时才抽取 internal 配置决策对象。
- `ProviderReleaseEvidence.cs` 同时包含 request、validator、writer 和 catalog，职责偏多；本轮只在元数据/聚合改动使文件继续显著膨胀时按“验证、目录、写入”职责拆分，不以行数机械拆分。
- `SqlLambdaQuery`、`SqlBuilderBase`、`SqlQueryBase` 等大类仅在本轮修改触及真实职责边界时局部拆分；禁止借 RC 硬化进行无关重构。

### 3.2 性能与资源

- 当前没有证据证明存在性能回归，也没有证据证明无回归。正式结论必须来自相同 SDK、Runtime、机器、Benchmark key 和配置的 FormalHost 制品。
- 优先测量 QueryPlan snapshot、cache hit/miss、参数 10/100/1000/2100、Join 1/3/5/10、Batch 10/100/1000/10000、SQLite streaming、Scope/Transaction、Diagnostics OFF/ON。
- 只有明确 Benchmark 热点才允许对象池、ArrayPool、Span、ValueTask 或 unsafe 优化；否则本任务只记录结果，不做复杂性能优化。

### 3.3 开发体验

- 本地 `.local` 文件可被项目自动加载，Provider runner 则显式 `CI=true` 并依赖进程专属变量。需要一个可重复、安全且不打印秘密的本地执行入口或明确 PowerShell 加载流程，避免开发者手工复制环境变量。
- Skip/Blocked 报告必须区分 gate disabled、connection missing、unsafe database、reset denied、unreachable、unsupported 和 implementation gap。
- 诊断应输出是否配置、数据库安全名称、可达性和执行状态，不输出密码、完整连接串或用户名。

## 4. 实施阶段、任务与依赖

## Phase A：事实基线与工作树治理

### T01（P0）固定当前工作树、SDK 与 RC 输入

- 依赖：无。
- 目标：避免把上一任务报告、历史 TRX 或脏工作树二进制误当作本任务证据。
- 已确认文件：`global.json`、`common.props`、`Bing.All.sln`、当前 Git Diff、上一任务 `execution.md` / `review.md`。
- 步骤：
  1. 记录 `git status --short`、`git diff --stat`、`git diff --check`、HEAD、分支、SDK、Runtime、OS。
  2. 将当前未提交变更按“前置任务改动、本任务后续改动、用户本地忽略配置”分类；不得 reset、checkout、clean 或覆盖。
  3. 为 RC Release Evidence 定义 clean-source 规则：受保护 CI 必须使用不可变 commit identity；本地脏工作树运行只能标记 `TestGenerated` 或 `NOT_RELEASE_ELIGIBLE`，不得产生可发布结论。
- 产物：`artifacts/reports/rs0026-inventory.md` 的环境前言，以及最终报告的 source identity 段。
- 验收：每项证据能追溯到 HEAD/工作树状态；不读取或输出 `.local` 中的秘密。

### T02（P0）执行七项目、全部支持 TFM 的 RS0026 Inventory

- 依赖：T01。
- 目标：以真实 Analyzer build 证明当前 `RS0026=0`，而不是沿用上一任务结论。
- 修改范围：原则上只生成 `artifacts/reports/rs0026-inventory.md`；发现诊断后再进入 T03/T04 修改对应源码和 PublicAPI。
- 命令基线：

```powershell
dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.Core\Bing.Dapper.Core.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.MySql\Bing.Dapper.MySql.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.PostgreSql\Bing.Dapper.PostgreSql.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.SqlServer\Bing.Dapper.SqlServer.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.Sqlite\Bing.Dapper.Sqlite.csproj -c Release --nologo
dotnet build .\framework\src\Bing.Dapper.Oracle\Bing.Dapper.Oracle.csproj -c Release --nologo
```

- 步骤：保存每个 Project/TFM 的完整日志；提取 Project、Namespace、Type、Member、File、Line、Overload Family、Analyzer Message；对 Shipped/Unshipped 分开分类。
- 验收：inventory 覆盖所有支持 TFM；`RS0026` 诊断数、处置和复核命令明确；不得 suppress。

## Phase B：Public API 最终收敛

### T03（P0）冻结 Query/Terminal/Transaction 公共语义

- 依赖：T02。
- 目标：在修 Analyzer 前先明确 API 语义，防止机械删除 optional 参数或继续扩展 overload。
- 必须决策：
  - `ToEntity<T>`：基于现有 `0→default / 1→entity / >1→exception` 行为，二选一：正式冻结 `ToEntity`，或主版本迁移为 `SingleOrDefault`；禁止长期保留同义 alias。
  - `ISqlTransactionScopeFactory.Begin/BeginAsync`：评估 Shipped optional overload family，按显式短重载/完整重载或 Options 收敛。
  - `DbKey` / `DataSourceKey`：公共新 API 统一使用 `DataSourceKey`；既有 Shipped 名称仅在收益足以覆盖迁移成本时主版本调整，内部无关变量不做批量重命名。
  - Query 根入口固定为 `Query/Sql/SqlInterpolated/Procedure/From/FromSubquery`，不新增同义入口。
- 修改范围：`framework/src/Bing.Data.Sql/**`、`Bing.Dapper.Core/**`、对应 `PublicAPI.*.txt`、XML、API contract tests、Release Notes/迁移文档。
- 验收：每个 Breaking Change 有替代 API、示例、反射/动态调用影响和 PublicAPI diff；没有 bool 控制参数或冗余 alias 扩张。

### T04（P0）实施 RS0026/API 修复并冻结 PublicAPI

- 依赖：T03。
- 目标：`RS0026=0`、Public API Analyzer 无阻塞、相邻重载一致。
- 步骤：
  1. 对 T02 每条诊断按“保留、显式 overload、Options/Descriptor、internal、删除/迁移”逐项实施。
  2. 同步 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` 和 XML 注释。
  3. 扩展 `SqlQueryApiContractTest` / `TransactionApiContractTest`，直接断言方法集合、参数顺序、optional metadata、CancellationToken 命名和 alias 决策。
  4. 复跑七项目 build 与 `Bing.Data.Sql.Tests` 双 TFM。
- 禁止：`NoWarn`、pragma、SuppressMessage、降低 Analyzer severity、为消 warning 增加更多重复重载。
- 验收：新 inventory 明确 `RS0026=0`；PublicAPI Analyzer 为 0 error；迁移文档与反射结果一致。

## Phase C：Integration 执行链与安全诊断

### T05（P0）统一本机 `.local`、CI Secret 与 testhost 加载链

- 依赖：T01。
- 目标：证明配置确实进入 testhost，同时保持 Git 无密和 protected lane 隔离。
- 已确认文件：三个 Provider integration `.csproj`、`.gitignore`、`eng/ci/Invoke-ProviderIntegrationTests.ps1`、`appveyor.yml`、`docs/testing/database-integration-tests.md`。
- 候选文件：`eng/ci/Invoke-LocalProviderIntegrationTests.ps1`，仅当现有 runner 无法安全复用 `.local` 时新增薄封装；不得复制 runner 验证逻辑。
- 步骤：
  1. 保持受跟踪 `integration.runsettings` 删除；验证三个 `.local` 被 Git 忽略。
  2. 增加安全的本地入口或文档化加载流程，将 `.local` 中仅目标 Provider gate、连接、reset 和可选 MySQL cross-db 配置注入进程，再调用统一 runner。
  3. protected runner 继续显式 `CI=true`，禁止加载 `.local`；common lane 清除所有 Provider 变量。
  4. 增加配置链测试：local 文件存在/缺失、CI=true、专属 gate、global gate、空值、`0/1/yes/true/TrUe`。
- 验收：本机和 CI 使用同一核心 runner；Git tracked 文件无秘密；testhost 可观察到正确 gate，但报告不含连接串。

### T06（P0）增加无密 Startup Diagnostic 与精确 Skip/Blocked 原因

- 依赖：T05。
- 目标：每次 Provider run 可审计地记录 RunSettings 来源、gate、connection configured、安全数据库名、reset、reachability 和 execution 状态。
- 修改范围：优先复用 `Bing.Test.Shared` 的 gate/safety helper、三个 Provider fixture、runner summary；不为每个 Provider复制诊断模型。
- 输出字段：Provider、ConfigurationSource、GlobalGate、ProviderGate、ConnectionConfigured、DatabaseName、ResetAllowed、DatabaseReachable、ExecutionStatus、BlockedReason。
- 安全规则：只记录布尔值和已通过安全校验的数据库名；不记录用户名、密码、主机、端口或完整连接字符串。
- 验收：每种 skip/block 原因有直接测试；unreachable 不被归类为 gate disabled；runner JSON 与 Integration report 可消费该诊断。

### T07（P0）修复 SQL Server 当前 lane 失败并隔离全局兼容测试

- 依赖：T05。
- 根因：测试未隔离 `RUN_SQLSERVER_INTEGRATION_TESTS`，与 runner 专属 gate 冲突。
- 修改范围：`SqlServerStartupConnectionStringTest.cs`、必要时 `Startup.cs` 的 internal 决策边界；不得放宽 protected lane 隔离。
- 步骤：
  1. 全局多 Provider 兼容测试显式保存、清除并恢复 SQL Server 专属 gate。
  2. 专属 lane 测试继续断言只注册 SQL Server。
  3. 将环境变量测试维持同 collection 串行，避免跨测试污染。
  4. 重跑 SQL Server lane，要求 Failed=0、CoreSkipped=0；`MultiProviderQueryTest` 要么移出核心 lane并明确 optional/non-release，要么提供独立安全多 Provider lane，不能静默 skip 核心合同。
- 验收：SQL Server runner 生成摘要和可信矩阵；无修改生产安全规则来迎合错误测试。

## Phase D：共享 Provider Behavioral Contract

### T08（P0）扩展现有 Capability Catalog 与共享合同词汇

- 依赖：T04、T06。
- 目标：统一核心 Provider 的能力口径，不创建第二套测试框架。
- 能力维度：Scalar、First/FirstOrDefault/Single/SingleOrDefault、List、Raw、Anonymous/Dynamic/Null/Interpolated、Lambda/Join/Subquery/Multi-source、Aggregate、Paging、Data Types、Streaming、Cancellation、CRUD、Batch、Transaction、Procedure/Output、Multiple Result。
- 步骤：
  1. 将现有 57-entry catalog 扩展为稳定枚举/目录；保留现有六态语义，报告层映射为 PASS/FAILED/NOT VERIFIED/BLOCKED/UNSUPPORTED。
  2. 每个 `RealIntegrationProven` 条目必须绑定真实测试方法；不支持项给出 DatabaseUnsupported，缺实现给出 ImplementationGap。
  3. 共享层只编排委托、验证元数据和序列化；数据库 SQL 留在 Provider fixture。
- 验收：唯一键、状态转换、重复、失败、取消、序列化、敏感字段均有共享单测；四核心 Provider 无空白能力单元。

### T09（P0）MySQL 缺口收敛

- 依赖：T08。
- 重点：Batch Mutation、Batch Transaction、Partial Failure、Large Batch、QueryMultiple/Multiple Result、early dispose/resource lifecycle。
- 修改范围：MySQL integration fixture/script、`SqlExecutor`/`SqlQuery` 测试、必要的 Provider 实现与直接单测。
- 策略：
  - 先确认现有公开 Multiple Query API 与 MySQL driver 行为，再写真实多结果 fixture；不伪造 Returning。
  - Returning 若数据库不支持，标记 `Unsupported/DatabaseUnsupported`；框架路径缺失才标记 `ImplementationGap`。
  - Batch 覆盖成功、事务提交/回滚、单批失败、分片边界和资源可复用。
- 验收：MySQL lane `Executed>0, Failed=0, CoreSkipped=0`；Batch 与 Multiple Result 不再无依据地为 ImplementationGap。

### T10（P0）PostgreSQL Procedure/Function 缺口收敛

- 依赖：T08。
- 重点：Function/Procedure、返回/输出语义、async、pre-cancel、稳定时的 during-command cancellation、large batch、streaming exception cleanup。
- 策略：按 PostgreSQL 原生语义建模，不强行模拟 SQL Server/MySQL `OUT/INOUT`；若公开 API 无法表达，记录明确 ImplementationGap 和后续 API 决策，不伪造支持。
- 验收：现有 RETURNING/UpdateFrom/DeleteUsing 不回退；新增过程/函数能力由真实 TRX 证明，或以有依据的 Unsupported/ImplementationGap 收口。

### T11（P0）SQL Server 核心合同补强

- 依赖：T07、T08。
- 子范围：
  - CRUD/Batch：Insert、Update、Delete、Batch Insert/Update/Delete、Transaction Batch、Partial Failure、OUTPUT。
  - Procedure：Stored Procedure、Output、InputOutput、ReturnValue、async、pre-cancel。
  - Multiple Result：QueryMultiple、多结果顺序、early dispose、reader/connection ownership。
  - Transaction：同步/异步 begin/commit/rollback、dispose without commit、Query+Executor shared transaction、NativeAsync/SynchronousFallback 实际模式。
  - Streaming/Cancellation：early break、during enumeration cancel、exception cleanup、cancel 后恢复执行。
  - Paging/Data Type：空页、边界页、Unicode、Guid、enum/nullable enum。
- Fixture：只创建 `BingSql*Integration` 固定前缀对象，初始化/reset/cleanup 全部经过 safety gate。
- 验收：每个新增测试断言真实 DB 状态、返回值或资源后续可用性；SQL Server lane零失败、零核心 skip，并生成 Release Evidence。

### T12（P0）SQL Server 2100 参数边界

- 依赖：T11 CRUD/Batch fixture。
- 场景：IN 与 Batch 的 2099、2100、2101；ParameterLimitManager、Batch Planner、Command Split。
- 要求：明确失败发生在 framework validation 或 SQL Server；2101 不允许生成语义错误或部分提交；Batch 分片保持事务和顺序。
- 测试：Provider unit tests 验证 profile/规划；真实 integration 验证数据库边界。SQL 输出测试断言完整 SQL 或完整命令集合。
- 验收：边界行为与 Provider Profile 的 2100 一致，失败不污染 Builder/参数/事务状态。

### T13（P1）SQLite、Oracle、Doris 定位收口

- 依赖：T08。
- SQLite：只补共享合同缺失的参考行为，不机械增加大量独有测试；继续运行受控 net8.0/net6.0 合同。
- Oracle：build、unit、注册和 capability 状态必须通过；无授权数据库时记录 NotExecuted/ImplementationGap，`ReleaseBlocking=No`。
- Doris：按 ReadOnly/MySQL protocol 定位，仅验证 query/read-only fail-fast；Mutation/Transaction/Procedure 不要求对齐核心 Provider。
- 验收：Oracle/Doris 不伪报 RealIntegrationProven，也不阻塞核心 RC。

## Phase E：Release Evidence 与报告

### T14（P0）补齐真实运行元数据和 Source Identity 完整性

- 依赖：T07、T09-T13。
- 当前缺口：`ProviderValidatedReleaseRun.DatabaseVersion` / `DriverVersion` 固定为 `not-recorded`；ProviderVersion 仅为 Provider 名；本地脏工作树运行仍使用 `git=<HEAD>`。
- 修改范围：Provider runner summary、Evidence request/validator/token、CLI、共享测试、Provider fixture 版本查询。
- 元数据：Provider package version、Database Engine version、ADO.NET driver version、.NET Runtime、TFM、OS、Commit、RunId、clean/dirty source state、必要的 binary hash。
- 规则：
  - 数据库版本使用无密标量查询，如 MySQL `VERSION()`、PostgreSQL `server_version`、SQL Server `SERVERPROPERTY`、SQLite `sqlite_version()`。
  - ReleaseEvidence 必须来自 clean CI commit；dirty workspace 只能 TestGenerated，或 source identity 必须包含可验证 diff hash 且不具备 release eligibility。
  - `not-recorded` 不允许核心 Provider 通过 RC Evidence gate。
- 验收：伪造版本、source identity 不符、dirty release、陈旧 binary/TRX 均被拒绝；四核心 Provider 成功矩阵包含真实版本。

### T15（P0）生成统一 Capability Matrix、Unit/Integration 报告

- 依赖：T08、T14。
- 产物：
  - `artifacts/test-results/provider-capability-matrix.json`
  - `artifacts/test-results/provider-capability-matrix.md`
  - `artifacts/test-results/unit-test-report.md`
  - `artifacts/test-results/integration-test-report.md`
- 实施：优先扩展 `Bing.ProviderEvidence.Cli` 聚合现有 TRX/摘要/矩阵；使用 XML/JSON 解析，不用文本拼接猜测计数。
- Unit report：Commit、SDK、Runtime、TFM、Project、Discovered、Executed、Passed、Failed、Skipped、Duration。
- Integration report：Provider、Assembly、配置来源、gate、connection configured、数据库/驱动版本、计数、Core/Optional skip、状态和阻塞原因。
- 验收：每个结论可定位 TRX/RunId/Test Method；失败或缺失制品不能合并成 PASS；输出无密。

## Phase F：职责级 Unit Test 与内部边界

### T16（P0）按真实改动补职责级单测

- 依赖：T04、T08、T14。
- 优先对象：Capability validation、Transaction async adapter/mode、ParameterLimitManager、Batch Planner、Provider runtime bridge、resource ownership、Provider/DataSource resolver、DatabaseScope、Release Evidence validator。
- 规则：
  - 只 mock 时间、IO、DB、连接、日志等外部边界，不 mock 被测内部实现。
  - 每个测试使用英文 `Method_State_Expected`、中文 XML 测试目的、AAA。
  - SQL 输出变更断言完整字符串；缓存变更覆盖命中、未命中和隔离；资源测试验证 Dispose/AsyncDispose、异常和取消后状态。
- 验收：本轮修改的每个核心生产符号均进入“生产符号 → 测试项目 → 方法”追溯；不以测试数量作为完成条件。

### T17（P1）InternalsVisibleTo 与内部复杂度复核

- 依赖：T16。
- 目标：friend assembly 只允许 Tests、Integration、Benchmarks 和必要测试辅助；生产 Provider 不通过 friend assembly 获取 internal。
- 复杂度：只对本轮实际改动造成的职责混杂进行拆分；优先 validation、lifecycle、transaction、cleanup、capability、execution 边界；单实现 helper 使用 internal class，不创建无意义 interface/base/strategy。
- 验收：保留并扩展现有 IVT contract test；无新增生产 friend；无第二套 resolver/evidence/transaction abstraction。

## Phase G：Formal Benchmark 与性能报告

### T18（P0）补齐缺失 Benchmark 场景

- 依赖：T04、T09-T12、T16。
- 复用项目：`Bing.Data.Sql.Benchmarks` 和现有 `Program.Main/BenchmarkSwitcher`。
- 先做覆盖差距分析，再最小补充：
  - Shape Cache hit/miss、Query clone、QueryPlan snapshot；
  - Parameters 10/100/1000/2100；
  - Join 1/3/5/10；Batch 10/100/1000/10000；
  - SQLite Streaming 100/10000/100000；Database Scope 1/10/100；Parallel Scope；
  - Transaction NativeAsync/SynchronousFallback；Diagnostics OFF/ON。
- 控制组合爆炸：不同维度拆成独立 benchmark class，禁止全笛卡尔积；100000 streaming 和 10000 batch 只在适用类中运行。
- 验收：每个新增场景有明确热点假设；FormalHost 含 MemoryDiagnoser；测试替身只用于纯构建热路径，E2E 使用 SQLite。

### T19（P0）执行 Formal Benchmark 并生成正式报告

- 依赖：T18。
- 执行：Release、无调试器、固定 SDK/Runtime、机器空闲；保留 BenchmarkDotNet raw artifacts。必要时分组运行，不能以 DryJob/CI smoke 替代 FormalHost。
- 产物：更新 `artifacts/benchmarks/benchmark-report.md`，记录 Commit、clean state、CPU、OS、Runtime、BenchmarkDotNet Version、Method、Mean、Median、StdDev、Ratio、Allocated、Gen0/1/2、原始制品路径。
- 比较：只有同 key、同参数、同 Job、同机器/Runtime 的历史结果才能计算 Baseline/Current/Delta；否则标记 `NOT COMPARABLE`，但当前 FormalHost 结果仍必须完整。
- 门禁：定义并在报告中说明 critical regression 阈值；任何超过阈值的结果先复跑确认，再标记 Confirmed/Potential Regression。禁止仅为 0GC 进行复杂优化。
- 验收：报告不再是 `BLOCKED`；所有计划核心场景有当前数据，无确认的 critical regression，或有明确阻塞/回退决策。

## Phase H：文档、最终验证与 RC Gate

### T20（P1）同步用户文档与迁移指南

- 依赖：T04、T09-T15。
- 当前文档基础：`docs/sqlquery-usage.md`、`docs/sqlquery-lambda-usage.md`、Mutation/Batch/Transaction/Multi-database 专题、Release Notes、integration testing 文档。
- 策略：不为满足目录名机械复制 13 份薄文档。先建立 `docs/sql/` 导航和 RC 必需页面，再复用/迁移现有成熟专题：getting-started、query/raw/interpolated、lambda、mutation/batch、transaction、streaming、stored procedure、multi-database/read-write、provider-capabilities、migration-guide。
- 要求：示例必须编译或由 contract test 校验；API 名称、optional 参数、Provider 限制和 `.local`/CI 配置与最终实现一致；无凭据。
- 验收：新增/修改 public API 有 summary/typeparam/param/returns/exception；override/interface 优先 `<inheritdoc />`；Release Notes 明确 Breaking Change。

### T21（P0）执行完整 RC 验证序列

- 依赖：T04-T20。
- 顺序：
  1. 七生产项目 Release build 和 `Bing.All.sln` 聚合 build；RS0026/PublicAPI Analyzer gate。
  2. `Bing.Data.Sql.Tests`、`Bing.Data.Sql.CustomProvider.Tests`、`Bing.Dapper.Core.Tests`、MySQL/PG/SQL Server/SQLite/Oracle unit tests，按支持 TFM 执行。
  3. SQLite 受控合同 net8.0/net6.0。
  4. MySQL、PostgreSQL、SQL Server protected runner；每个 Provider/TFM 使用唯一 RunId，不复用历史 TRX。
  5. Evidence aggregation 和三份测试/能力报告。
  6. Formal Benchmark 和报告。
  7. 文档/链接/API 示例检查、`git diff --check`、最终 Diff Review。
- 外部阻塞：某 Provider 不可达时记录 BLOCKED 并继续其他阶段；不得把 Executed=0 判 PASS，也不得因 Oracle/Doris 阻塞核心 RC。

### T22（P0）生成最终报告并判定 RC

- 依赖：T21。
- 产物：`artifacts/reports/BING-SQL-RC-HARDENING-20260904-001-final-report.md`。
- 报告章节：总体、Build、RS0026 before/after、Breaking/API、RunSettings/Gate、MySQL、PostgreSQL、SQL Server、SQLite、Oracle/Doris、Capability Matrix、Release Evidence、Unit/Integration、Benchmark/GC、IVT、PublicAPI、文档、剩余问题、RC Gate、Release Recommendation、文件清单。
- 文件清单必须区分新增、修改、删除、重命名，并说明每个核心文件原因。
- 只有以下条件同时成立才写 `RC READY`：
  - `RS0026=0` 且 PublicAPI Analyzer/build 通过；
  - 核心 Unit 项目通过；
  - SQLite/MySQL/PostgreSQL/SQL Server 各 `Executed>0, Failed=0, CoreSkipped=0`；
  - 四核心 Provider Release Evidence 有效、版本元数据完整、source identity 可发布；
  - Capability Gap 已按真实支持/不支持/实现缺口收口，未将缺口伪装为通过；
  - Formal Benchmark 完成且无确认的 critical regression；
  - PublicAPI baseline、迁移文档和最终报告一致。
- 任一核心条件不满足时写 `RC NOT READY` 或 `BLOCKED`，列出可执行后续项，不降低门禁。

## 5. 用例矩阵

| Given | When | Then | 层级 |
| --- | --- | --- | --- |
| 当前七生产项目和全部 TFM | 执行 Release Analyzer build | RS0026=0；无 suppress；日志进入 inventory | Build/API |
| Shipped optional overload family | 反射检查 API | 方法集合、参数顺序和 optional metadata 与批准策略一致 | Unit |
| `ToEntity<T>` 0/1/>1 行 | 执行 terminal | 行为与唯一冻结名称一致，无 alias 漂移 | Unit + Integration |
| `.local` 存在且非 CI | 运行本机入口 | 只注入目标 Provider，配置来源为 LocalIgnored | Integration harness |
| `CI=true` 且存在 `.local` | 运行 protected runner | 不加载 `.local`，只接受 Job Secret | Runner self-test |
| gate missing/false/empty/0/1/yes | 发现 IntegrationFact | 明确 skip reason，不连接数据库 | Unit |
| 专属 gate=true、连接安全、reset=true | preflight | 通过并记录无密诊断 | Runner |
| global/default/其他 Provider 变量存在 | protected preflight | 连接前失败 | Runner |
| 数据库不可达 | startup diagnostic | BLOCKED/DatabaseUnreachable，不输出秘密 | Integration |
| SQL Server runner 注入专属 gate | 全局兼容配置测试执行 | 测试自隔离环境，不受 runner 污染 | Unit/Integration startup |
| MySQL batch 成功/失败/大批量 | 执行 mutation | 状态、事务、分片和资源符合预期 | Integration |
| MySQL multiple result early dispose | 读取首结果后释放 | Reader/Connection 可继续使用 | Integration |
| PostgreSQL function/procedure | sync/async/cancel | 返回和输出语义按原生能力验证 | Integration |
| SQL Server CRUD/Batch/Procedure/Multiple | 执行真实命令 | 返回、输出参数、DB 状态和资源均正确 | Integration |
| SQL Server IN/Batch 2099/2100/2101 | 构建并执行 | framework/DB 边界明确，2101 不部分提交 | Unit + Integration |
| Streaming early break/cancel/exception | 结束枚举后再次查询 | Reader、Connection、Transaction、Lease 释放 | Integration |
| Transaction native/fallback | async begin/commit/rollback | mode 和可见性与 Provider profile 一致 | Unit + Integration |
| TRX failed/core skip/zero executed | 写 Release Evidence | 拒绝且不生成成功矩阵 | Unit + Runner self-test |
| dirty workspace / stale binary | 创建 Release Evidence | 拒绝 release eligibility | Unit |
| 核心 Provider 成功运行 | 聚合报告 | TRX、RunId、版本、方法和能力状态一致 | CLI/Artifact |
| Formal Benchmark 同 key 基线存在 | 生成报告 | 输出 Delta 和 regression 分类 | Benchmark |
| 无可比较历史基线 | 生成报告 | 输出当前完整数据并标记 NOT COMPARABLE，不伪造 Delta | Benchmark |

## 6. Mock 与真实边界

- 可 Mock：时钟、文件时间、环境变量快照、无网络的 connection factory、异常/取消委托、日志 sink、Evidence 测试的临时 TRX/JSON。
- 不 Mock：Provider fixture 的真实 SQL 行为、参数上限数据库响应、事务可见性、Output/Procedure/Multiple Result、Streaming 资源释放。
- Shared Contract 单测使用内存委托验证状态机；Provider integration 使用真实受控数据库。
- Benchmark 的 Builder/QueryPlan 纯 CPU 场景可用 no-op executor；数据库 E2E 只用 SQLite 或明确授权 Provider，不将 mock 数据库结果当吞吐证据。

## 7. 已确认文件与候选文件

### 7.1 已确认会修改或生成

- `common.props`（仅当 gate 条件需纠正；不得降低 RS0026）
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- T02 inventory 命中的 SQL/Dapper API 源文件
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/TransactionApiContractTest.cs`
- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
- `eng/Bing.ProviderEvidence.Cli/Program.cs`
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- `appveyor.yml`
- MySQL/PostgreSQL/SQL Server integration fixture 与合同测试
- `framework/tests/Bing.Data.Sql.Benchmarks/*.cs`
- `docs/testing/database-integration-tests.md`
- `docs/ReleaseNotes.md`
- `ai_docs/sql-metadata-test-traceability.md`
- 本计划第 2、15、19、22 节列出的 inventory/report/matrix 制品

### 7.2 候选文件，仅在证据要求时修改/新增

- `eng/ci/Invoke-LocalProviderIntegrationTests.ps1`
- Provider 安全诊断 DTO/helper（优先放 `Bing.Test.Shared`）
- `ProviderReleaseEvidence.cs` 的职责拆分文件
- SQL Server fixture 的 procedure/multiple/batch SQL 文件
- `framework/src/Bing.Dapper.Core/**` 的 Batch/Multiple/Transaction 实现
- `framework/src/Bing.Dapper.{MySql,PostgreSql,SqlServer}/**` 的真实实现缺口
- `docs/sql/**` 的导航和最终专题页
- API sample/compile contract tests

### 7.3 明确禁止创建或恢复

- 含凭据的跟踪 `integration.runsettings`
- 第二套 Provider Contract/Evidence/Benchmark 框架
- 为兼容同时保留的同义 terminal/root API
- 无单独职责的 interface/base/strategy 层级

## 8. 数据、配置、兼容性和安全影响

- 数据库：只修改专用测试库中的固定测试对象；不迁移生产 schema。新增 procedure/table/type 必须有 idempotent setup/reset/cleanup。
- 配置：本机 `.local` 保持忽略；CI Secret 不写入 YAML；报告只记录 configured/reachable 和安全数据库名。
- Public API：允许主版本 Breaking Change，但必须有迁移说明、PublicAPI diff 和契约测试。删除/重命名不能只靠 Analyzer 驱动。
- Evidence：核心 Provider release 证据必须来自 clean、受保护运行；本地运行默认不具备 release eligibility。
- 性能：未测量前不做复杂优化；发现 regression 时先重复测量并定位到具体 hot path。

## 9. 分阶段提交与验收建议

为降低单个 Diff 风险，Executor 应按以下独立可 Review 批次实施，但不自动 Git commit：

1. Batch A：T01-T04，RS0026 inventory、API 决策、PublicAPI 和 Analyzer gate。
2. Batch B：T05-T07，配置诊断和 SQL Server 当前失败修复。
3. Batch C：T08-T10，Shared Contract、MySQL、PostgreSQL。
4. Batch D：T11-T13，SQL Server 深化、参数边界、非阻塞 Provider。
5. Batch E：T14-T17，Evidence metadata、报告、Unit/IVT。
6. Batch F：T18-T20，Benchmark 和文档。
7. Batch G：T21-T22，完整 RC 验证与最终报告。

每批完成后先跑最小测试，再跑相关回归；不得等到最后才发现 Provider fixture 或 API baseline 断裂。

## 10. 最终验收 Checklist

- [ ] 当前工作树、SDK、Runtime、OS 和 source identity 已记录，Release Evidence 不接受脏工作树伪装为 commit。
- [ ] 七生产项目全部支持 TFM 的 RS0026 inventory 已生成，最终 `RS0026=0`。
- [ ] 未使用 NoWarn/pragma/SuppressMessage/降级 severity。
- [ ] Query root、Terminal、Transaction、DataSourceKey 决策完成，PublicAPI 与迁移文档一致。
- [ ] `.local` / CI Secret / testhost 加载链和无密诊断已验证。
- [ ] SQL Server 当前 startup test 失败已按环境隔离根因修复，未放宽 protected lane。
- [ ] SQLite/MySQL/PostgreSQL/SQL Server 各 `Executed>0, Failed=0, CoreSkipped=0`。
- [ ] MySQL Batch/Multiple Result 缺口已真实证明、明确 Unsupported 或保留可解释 ImplementationGap。
- [ ] PostgreSQL Procedure/Function 缺口已按原生语义收口。
- [ ] SQL Server CRUD、Batch、Procedure、Multiple Result、Transaction、Streaming/Cancellation、Paging 和 2100 边界明显补强。
- [ ] ProviderContractRunner/Evidence 继续作为唯一证据框架。
- [ ] 四核心 Provider Evidence 含真实 Database/Driver/Package/Runtime/OS/Commit/RunId 元数据。
- [ ] unit-test-report、integration-test-report、provider-capability-matrix JSON/Markdown 已生成并可追溯。
- [ ] 本轮核心生产符号到测试方法的映射已更新。
- [ ] Formal Benchmark 已执行，benchmark-report 含统计、分配和可比较时的 Delta。
- [ ] 无确认的 critical performance regression，或报告明确阻塞 RC。
- [ ] InternalsVisibleTo 仅允许测试/基准必要程序集，无生产 friend 回归。
- [ ] Public API baseline、XML、Release Notes、迁移和用户文档同步。
- [ ] Oracle/Doris 状态诚实且不阻塞核心 RC。
- [ ] 最终报告列出新增/修改/删除/重命名文件及原因。
- [ ] 仅在所有核心门禁满足时报告 `RC READY`；否则报告 `RC NOT READY/BLOCKED`。
- [ ] 未执行 git add、commit、push、PR 或自动 merge。

## 11. 实施 Handoff

批准后执行：

```text
/execute-plan BING-SQL-RC-HARDENING-20260904-001
```

Executor 必须从 T01 开始，以本文件为唯一计划契约，持续实施、测试、生成 `execution.md` 和制品；外部 Provider 单点阻塞时继续其他可执行任务，但不得跳过最终 RC Gate 或伪造 PASS。
