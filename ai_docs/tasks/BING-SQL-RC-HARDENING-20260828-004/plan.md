# Bing.Data.Sql / Bing.Dapper RC Hardening 实施计划

<!-- AI_PLAN_STATUS: READY_FOR_EXECUTION -->
<!-- AI_TASK_ID: BING-SQL-RC-HARDENING-20260828-004 -->

## 1. 任务元数据与裁决

| 项 | 值 |
| --- | --- |
| Task ID | `BING-SQL-RC-HARDENING-20260828-004` |
| 类型 | implementation-and-verification |
| 优先级 | P0 |
| 当前规划结论 | `READY_FOR_EXECUTION`，开始实施后的总状态初始为 `PARTIAL` |
| 代码基线 | 当前分支 `dev_v6.0-refactor-sqlquery`；规划时已知 HEAD 为 `3059a5971e0d3b52705a8d63eb077763e61d3a9d`，实施 Phase 0 必须重新记录实际 HEAD 与工作树状态 |
| SDK | `global.json` 固定 `8.0.419`，允许 `latestPatch` |
| 主构建入口 | `Bing.All.sln`，.NET 项目，xUnit，BenchmarkDotNet `0.14.0` |
| 自动 Git 操作 | 禁止 `git add`、commit、push、PR、tag、release |

### 1.1 指令冲突

用户需求要求从 Phase 0 持续执行至 Phase 6，并在任务目录生成报告；但本会话当前处于 `plan-writer` 模式，且 `.github/prompts/create-plan.prompt.md` 明确限制本阶段只能写入实际 `plan.md`，禁止实施、测试、CI、数据库操作及创建执行报告。

本计划因此只记录实施步骤，不执行它们。后续必须通过 `/execute-plan BING-SQL-RC-HARDENING-20260828-004` 或等价 `execute-plan` 入口开始执行。执行器在 Phase 0 创建本任务要求的所有报告；本规划阶段不得预创建它们。

### 1.2 不可违反的保护边界

下列用户配置是绝对保护对象：不得读取、解析、显示、写入、清空、删除、复制或以日志/报告回显其内容。只允许用 `git diff --quiet -- <path>` 验证路径未变更。

- `framework/tests/Bing.Dapper.MySql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.MySql.Tests.Integration/appsettings.json`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/appsettings.json`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/appsettings.json`
- `modules/admin/src/Bing.Admin/appsettings.json`
- `modules/admin/src/Bing.Admin.FreeSQL/appsettings.json`

其他全程约束：

- Provider 凭据只通过受保护 CI 变量或维护者显式注入；所有终端输出、TRX、JSON、Markdown 仅记录变量名称、数据库安全校验结论和脱敏标识，绝不输出连接字符串或密码。
- 受保护 Provider lane 只允许自身的 `RUN_<PROVIDER>_INTEGRATION_TESTS=true`、自身 `ConnectionStrings__<Provider>Connection` 与 `ALLOW_DATABASE_RESET_FOR_TESTS=true`。必须拒绝 `RUN_INTEGRATION_TESTS=true` 和非空 `ConnectionStrings__DefaultConnection`。
- 真实重置只针对通过安全命名校验的专用测试数据库。不得使用生产、开发或系统数据库。
- 不将默认 gate Skip、纯 DI/Startup 测试、runner `-ValidateOnly`、runner `-SelfTest`、BenchmarkDotNet `DryJob` 或静态 YAML 路由称为真实 Provider/远端 CI/FormalHost 成功。
- 与本任务无直接调用链关系的 `NotImplementedException`、其他框架模块的 `Helper`、无关格式化和无关重构均不纳入范围。

## 2. 当前证据与完成度判断

### 2.1 已确认实现

| 领域 | 已有实现与直接证据 | 当前判定 |
| --- | --- | --- |
| Provider 默认隔离 | 三个外部 Provider 集成项目均不再声明 `RunSettingsFilePath`；本地 runsettings 仅能由用户显式选择。 | 已实现；不等于真实 Provider 已执行。 |
| 门控与安全 | `IntegrationTestGate` 使用全局或 Provider 专属 gate；`Invoke-ProviderIntegrationTests.ps1` 白名单 MySQL/PostgreSQL/SQL Server，校验 gate、专属连接、reset、测试库名，拒绝 global/default 回退，分析 TRX 并输出 JSON。 | 本地合同已实现；真实数据库与远端证据缺失。 |
| CI 路由 | `appveyor.yml` 可按 `PROVIDER_TEST_LANE=common\|mysql\|postgresql\|sqlserver` 选择 common 或三个 runner 调用。 | 部分实现；静态文件没有 matrix/job 为后三个 lane 赋值，日常调度只会落到 `common`。 |
| 本地兼容 | PostgreSQL/SQL Server Startup 优先专属连接，缺失时可回退本地 `DefaultConnection`；SQL Server 在显式 global 本地模式注册三种具名数据源。 | 已实现，且受保护 runner 首先拒绝 global 模式。 |
| 环境变量测试隔离 | 共享项目有禁并行 `EnvironmentVariableTestCollection`；SQL Server Startup 测试自行 `try/finally` 恢复 global gate。 | 部分实现；SQL Server 测试未加入其程序集内的禁并行 collection，可能与同进程测试并发干扰。 |
| 查询 mutation 生命周期 | `SqlQuery` 以 `_shapeVersion`、`_cachedVersion`、`_cachedSql` 控制缓存；现有测试覆盖追加 SQL、清空参数、Union/CTE 等多种成功/失败/no-op。 | 部分实现；`SqlQueryOperationAccessor.Mutate` 和 `MutateBuilder` 成功返回后无条件 `MarkChanged`。 |
| `WhereIfNotEmpty` | `WhereClause.WhereIfNotEmpty` 对 null/空白直接 return，但 public fluent 扩展仍先进入 mutation gateway。 | 缺陷候选，需以直接回归测试证明；很可能使空输入无效化缓存。 |
| `Helper` 公共边界 | `Builders.Internal.Helper` 是 public，`JoinItem.SetDependency(Helper)` 和 `Clone(Helper)` 也是 public；17 个相关记录已在 `PublicAPI.Shipped.txt`。仓库源码的已定位生产调用均在 Bing.Data.Sql 内部。 | 不合理且高耦合；不能未经消费者审计直接 internalize。 |
| Benchmark | 正式类用 `FormalHost`（3 launch、6 warmup、15 iteration）；`--ci-smoke`/`--e2e-smoke` 用 DryJob。 | Smoke 可运行边界已存在；不等价正式基线。 |
| SQLite E2E benchmark | `SqliteDapperE2EBenchmarkBase.Setup()` 无条件创建 `NoOpDiagnosticObserver`，`QueryWithDiagnosticListener()` 只调用基线查询，`SubscribeDiagnosticListenerAndQuery()` 再创建 observer。 | 基线受污染，listener-off、steady-on、subscribe-on 不可比较；`RowCount` 也被不依赖规模的异常/取消/诊断场景继承。 |
| 文档与追溯 | 集成测试文档已描述 gate/runner 安全模型；ReleaseNotes 仍含未绑定当前 artifact 的历史测试计数；Public API 治理与 traceability 未覆盖本任务的最终 Helper/mutation/benchmark 决策。 | 部分完成，需以实际产物更新。 |

### 2.2 上一任务的可复用但非最终证据

`BING-SQL-RC-HARDENING-20260827-003` 的 execution/review 记录了本地 build、shared gate、Data.Sql、Dapper Core、SQLite 集成、默认 Skip、runner self-test 和 Dry benchmark 结果。它们只能用于确定现有入口和回归基线，不能充当本任务的 current Provider non-skip、远端 CI 或 FormalHost 证据。

上一 review 尚有 `FIX-001`（SHOULD_FIX：真实 Provider、远端 CI、FormalHost、发布追溯）和 `FIX-002`（OPTIONAL：局部格式）。本任务吸收前者为 P0/P1 验收目标；后者仅在触及相同文件时最小处理，不能挤占 P0。

### 2.3 14 项审计回答

| 问题 | 基于当前源码的结论 | 本计划处理 |
| --- | --- | --- |
| 1. 已实现什么 | 上表的 gate、runner、默认隔离、SQLite 路径、缓存与 API analyzer 均有实体实现。 | Phase 0 建立当前证据清单，Phase 1-5 按真实调用链复验。 |
| 2. 完成/部分/未实现 | 默认安全隔离已完成；protected CI、Provider non-skip、FormalHost、Helper 收敛、mutation no-op、benchmark 隔离均部分或未完成。 | 分拆验收，不允许互相替代。 |
| 3. 是否仅骨架 | runner/self-test 和静态 lane 是真实本地合同，但尚没有外部数据库或远端执行证据。 | Phase 1/4/5 要求制品与 provenance。 |
| 4. 总体完成度 | RC 本地安全基础较完整，发布级证据未闭环，当前总体只能是 `PARTIAL`。 | 只有全部强制报告和证据满足时才可 `COMPLETED`。 |
| 5. 缺口 | CI job materialization、真实 Provider 矩阵、环境变量并发隔离、no-op 正确性、Helper 决策、benchmark 比较、文档追溯。 | Phase 1-6。 |
| 6. 性能/资源风险 | 无条件缓存失效导致重复渲染；observer 常驻造成监听器基线污染；`RowCount` 使固定场景重复运行。 | Phase 2、Phase 3 先正确性再性能。 |
| 7. 复杂度/耦合 | `Helper` 聚合上下文、方言、映射、参数等职责；公开 `JoinItem` 泄漏内部依赖；runner 与 gate 各维护安全模型。 | Phase 1 建立 runner/gate drift 测试；Phase 2 进行消费者审计和边界收敛。 |
| 8. 开发体验 | local DefaultConnection 兼容可用，但 protected lane 与普通 lane 的分界要依赖远端配置；当前文档没有可审计 job 证据。 | Phase 1/5 给出明确、无密的 job 与报告入口。 |
| 9. API 合理性 | non-generic query + terminal result 的主路径明确；`Builders.Internal.Helper` 命名和可见性不合理。 | Phase 2 以外部消费者事实决定内部化或最小命名 SPI。 |
| 10. 重复/冗余 | `Helper` 将多责任集中；当前 diagnostic benchmark 三场景不独立；Dry 与 FormalHost 表面同名但证据等级不同。 | Phase 2/3 拆分，不增加兼容 facade。 |
| 11. 可合并/删除/内部化 | 若没有外部 Provider/包契约，`Helper` 与 `JoinItem` 的两个依赖注入方法应 internalize；否则只能保留最小经证实 SPI。 | Phase 2 的决策闸门。 |
| 12. 大文件/结构 | `Helper.cs` 约 500 行且多责任；SQLite E2E benchmark 基类混合数据库、诊断、trace、多映射和固定场景。 | Phase 2 仅按边界拆出必要协作；Phase 3 按测量条件拆 benchmark 类型，避免无关业务重构。 |
| 13. 测试覆盖 | 现有覆盖广，但未直接覆盖 public `WhereIfNotEmpty` 空值缓存版本，未隔离 SQL Server process env，并且外部 Provider 默认是 Skip。 | Phase 1、2、4 增加直接、可运行和真正执行的测试。 |
| 14. 文档 | 集成文档较接近源码；README、ReleaseNotes、governance、traceability 与 current RC artifacts 未闭环。 | Phase 5 基于真实结果更新，禁止伪造计数。 |

## 3. 目标、非目标与依赖顺序

### 3.1 任务目标

1. 让 `common`、MySQL、PostgreSQL、SQL Server 成为真实可调度、可审计且隔离的 CI 路径，或准确记录平台权限阻塞。
2. 在不接触受保护配置的前提下，保存每个真实 Provider 的 non-skip TRX/JSON 证据，并与 SQLite 本地真实集成测试区分。
3. 修复并证明查询描述的 no-op 不改变 ShapeVersion/cache，真实变更恰好 Touch 一次，异常不 Touch。
4. 以消费者事实处理 public `Helper` 和公开 `JoinItem` 内部依赖泄漏，形成唯一推荐扩展边界。
5. 让 BenchmarkDotNet 的 listener-off、steady-on、subscribe-plus-query 分别可比较，且 FormalHost 结果有可复核 before/after provenance。
6. 交付用户指定的全部报告、可追溯矩阵与独立 review/fix 闭环；没有外部授权时明确 `BLOCKED`，而非虚报完成。

### 3.2 明确非目标

- 不恢复 `.As<TResult>()`、generic query facade、`CreateAdvanced*`、`FromTable(string)`、高元数查询类型或重复 terminal API。
- 不创建为兼容旧 `Helper` 而存在的宽泛 facade；不为 benchmark 增加生产专用/池化路径。
- 不修改受保护配置文件内容，不猜测任何密钥、数据库地址或外部 CI URL。
- 不将无关模块的 `Bing.Data.Queries.Internal.Helper` 或其他命名相同类型改名。
- 不处理与 SQL/Dapper 链路无关的旧 warning、样式或 `NotImplementedException`。

### 3.3 依赖图

```text
Phase 0 证据与报告协议
  -> Phase 1 CI/Provider 安全边界
      -> Phase 4 真实集成与 CI 制品
  -> Phase 2 查询 mutation 与 public Helper 决策
      -> Phase 3 Benchmark 隔离与 FormalHost
      -> Phase 4 回归矩阵
  -> Phase 5 文档、追溯、发布证据
      -> Phase 6 独立 review 与修复闭环
```

## 4. 实施前文件清单

### 4.1 已确认将修改或创建的文件

执行中根据实际决策选择最小集合；以下是已确认的计划目标，不代表每个文件必然在同一提交中改动。

- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/progress.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/execution.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/unit-test-report.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/integration-test-report.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/benchmark-report.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/verification-report.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/review.md`
- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260828-004/artifact-index.md`
- `appveyor.yml`
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerStartupConnectionStringTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/` 下新增或复用本程序集的 environment-variable collection 定义
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/WhereClauseExtensions.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Internal/Helper.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/JoinItem.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`，仅在最终留下新的 public API 时
- `framework/tests/Bing.Data.Sql.Analyzers.Tests/SqlOperationCompileContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Core/JoinItemTest.cs` 和/或现有直接契约测试文件
- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`，仅在需要增加明确 benchmark host 选择入口时
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMutationBenchmarks.cs`
- `docs/integration-testing.md`
- `docs/testing/database-integration-tests.md`
- `README.md`，仅补充已被远端或仓库证据支持的 RC/SDK/CI 入口
- `docs/ReleaseNotes.md`
- `ai_docs/sql-public-api-governance.md`
- `ai_docs/sql-metadata-test-traceability.md`

### 4.2 候选文件，必须先证实需要再修改

- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestGateTest.cs`：仅当 runner/gate 规则确有重复或 drift，新增直接契约；不为格式化改动。
- `framework/tests/Bing.Dapper.{MySql,PostgreSql,SqlServer}.Tests.Integration/**`：仅为真实发现的功能矩阵缺口添加 gated 集成测试；每个新增测试必须有可重置、可清理的 fixture 支持。
- `.travis.yml`：只有确认 Travis 仍是实际 required CI 且存在可复现差异时修改；否则只记录远端状态未知。
- `framework/src/Bing.Data.Sql/AssemblyInfo.cs`：默认不修改。不得增加 production `InternalsVisibleTo` 来绕过 public API 边界。
- Provider 项目 PublicAPI 基线：仅在 Phase 2 消费者审计证明其导出面受影响时修改。

## 5. Phase 0 - 基线、报告协议与安全取证

### RC28-P0-01 [P0] 建立当前任务身份和报告骨架

**依赖：** 无。  
**目标：** 创建用户要求的八份执行期 Markdown 和 artifact 索引，确保每个结论可以定位到当前源版本、命令、环境和制品，而不写入敏感值。

**步骤：**

1. 记录实际 HEAD、分支、dirty file path 列表、`global.json` SDK、`dotnet --info`/`dotnet --list-sdks` 摘要、操作系统、处理器、内存、runtime、GC 配置和 UTC/本地时间。
2. 创建 `progress.md`，每个 Phase 使用 `PENDING/IN_PROGRESS/COMPLETED/PARTIAL/BLOCKED/FAILED`；不得将外部阻塞阻断其他无依赖 Phase。
3. 创建 `execution.md`，只记录实现动作、实际修改文件、偏差和未执行原因。
4. 创建 `unit-test-report.md`、`integration-test-report.md`、`benchmark-report.md`、`verification-report.md`、`artifact-index.md`、`review.md`。报告状态先设为 `PENDING`；没有运行也必须保留报告并写解除条件。
5. `artifact-index.md` 每条 artifact 至少记录相对路径、SHA-256、生成命令、源 HEAD、TFM/Provider、run id/job id（若有）、是否 non-skip/FormalHost、脱敏检查结论和报告反向链接。
6. 对八个受保护路径仅运行 `git diff --quiet -- <path>`；报告只记录“路径未变更/变更导致停止”，不读取内容。
7. 检查上一任务报告是否在本次开始后被用户改变；仅作为历史输入，不覆盖、不重写。

**验收：** 八份报告均存在；所有内容 UTF-8；没有受保护文件内容或连接字符串；所有证据可关联到当前 HEAD。

### RC28-P0-02 [P0] 建立完成判定与外部解锁清单

**目标：** 使外部数据库、CI 和 FormalHost 的缺失可被精确表示，避免“接口存在即完成”。

**步骤：**

1. 在 `verification-report.md` 固定三层证据：仓库静态/本地合同、SQLite 真执行、受保护 Provider/远端 CI/FormalHost。
2. 定义 Provider 完成最低证据：每个 Provider 至少一份本任务 current non-skip TRX 和 runner JSON，至少 `net8.0`；若项目仍支持 `net6.0`，每 Provider 还须有代表性 net6.0 real run 或报告准确的支持范围与阻塞原因。
3. 定义 FormalHost 比较键：benchmark type、method、参数、job id、SDK、runtime、OS、CPU、GC、commit、命令、artifact hash。不同键不得比较或宣称回归/改善。
4. 列出维护者需提供的非仓库前置条件：每 Provider 独立安全测试库、对应 CI secret、仅 trusted/manual/scheduled/approved 的 job 权限、artifact 留存权限、以及同机 FormalHost 时间窗口。

**验收：** 当前总状态为 `PARTIAL`，而非 `COMPLETED`；阻塞项均有精确解除条件。

## 6. Phase 1 - P0 CI、Provider 边界与并发安全

### RC28-P1-01 [P0] 将四条 CI lane 从静态分支变为可调度作业

**依赖：** RC28-P0-01。  
**目标：** materialize `common`、`mysql`、`postgresql`、`sqlserver` 四条可见 job/lane，同时不把 Provider secret 暴露给 common/untrusted 作业。

**步骤：**

1. 检查 AppVeyor 当前实际支持的 job/matrix 和 job-scoped secure-variable 能力；不得从本地 YAML 推断远端机密、分支保护或成功状态。
2. 在 `appveyor.yml` 增加明确的四 job/matrix 路由，令每个 job 显式赋予 `PROVIDER_TEST_LANE`。common job 继续清除 global/provider/reset/default/provider connection 变量。
3. MySQL/PostgreSQL/SQL Server job 只能调用对应 runner；不得在其 job 定义中导出实际连接值，且不得用 common 变量回退。
4. 如 AppVeyor 无法提供 job-scoped secret，选择受支持且可审计的替代拓扑，例如三个受保护的独立 CI project/job；在文档和报告中将“YAML 已修改”与“远端受保护配置已完成”分开记录。不得用全局变量暴露全部 Provider secret 作为捷径。
5. 将外部 job 限制到受信分支、受批准 PR、手动或定时触发中的维护者确认策略；该策略若只能在 Web UI 设置，列入 `BLOCKED`，记录无密截图/运行标识或等价导出证据。
6. 确保 artifacts 包含 common test TRX/coverage、provider TRX/JSON 和 benchmark artifact 文件夹；artifact 名称含 Provider、TFM、job/run identity，避免覆盖。

**测试与验收：**

- YAML 结构可被 CI 解析，四 lane 均能从仓库配置定位到不同入口。
- common lane 未拥有 Provider gate、reset、default 或 provider connection 值。
- 外部 Provider job 成功前，报告状态仍是 `PARTIAL/BLOCKED`，不能以 matrix 语法存在宣布完成。

### RC28-P1-02 [P0] 强化 runner 可审计合同，防止 gate/runner 漂移

**依赖：** RC28-P1-01。  
**目标：** runner 的预检、TRX 审计和 JSON 摘要能区分执行与 Skip，并提供足够 provenance。

**步骤：**

1. 将 runner 与 `IntegrationTestGate`/`IntegrationDatabaseSafetyValidator` 的 Provider 名称、gate 名、connection variable、reset 安全规则逐项对照；若存在重复常量漂移，优先增加同输入的直接测试，而不是通过改变安全阈值“统一”。
2. 保留现有先拒绝 global gate、再验证 provider gate/reset/default/connection/safe DB 的顺序。新增或完善 self-test，以覆盖 literal `true`、非规范 PG alias、缺少项目、零发现、全 Skip、core Skip、允许的 optional Skip、失败结果和 JSON 字段。
3. JSON 只输出 Provider、TFM、计数、duration、相对 TRX path、source/run identity 和安全检查结果；不得序列化 connection string、环境变量值或数据库地址。
4. 需要时为 runner 增加可选无密 provenance 参数或从 CI 标准变量读取的 run identity，但首先实现 allow-list，避免将任意环境变量写入 artifact。
5. 对三个项目继续静态验证 `RunSettingsFilePath` 缺席，且不读取对应 runsettings。

**验收：** `-SelfTest` 通过；安全 fake 的 `-ValidateOnly` 只能证明预检且报告不得写为数据库执行；TRX 校验拒绝零执行和 core Skip；JSON 不含敏感值。

### RC28-P1-03 [P0] 隔离 SQL Server Startup 的进程环境变量测试

**依赖：** RC28-P0-01。  
**目标：** 防止 `ConfigureServices_WhenGlobalMultiProviderRunEnabled_ShouldRegisterAllProviderDataSources` 在设置 `RUN_INTEGRATION_TESTS` 的时间窗口与同程序集测试并发。

**步骤：**

1. 在 SQL Server integration test 程序集建立专用的 `[CollectionDefinition(..., DisableParallelization = true)]`，或在确认现有程序集 collection 可复用后复用其名称；不能依赖 `Bing.Test.Shared` 内 collection definition 跨程序集生效。
2. 将所有本程序集会写环境变量的测试归入该 collection；保留 `try/finally` 恢复原值。
3. 不把此 Startup/DI 测试归入真实 Provider 执行计数，runner optional Skip 也不得掩盖真实 core Skip。

**验收：** 两个 TFM 的相关项目测试不出现并发环境污染；原有三数据源 DI 断言仍通过；没有连接外部数据库。

## 7. Phase 2 - P0/P1 查询 mutation 与 Public API 收敛

### RC28-P2-01 [P0] 先锁定 mutation/no-op 正确性，再最小修复

**依赖：** RC28-P0-01。  
**目标：** 查询描述的成功真实变更恰好 invalidates cache 一次；no-op 和异常保持 SQL、参数、ShapeVersion、cachedVersion 和 render count 不变。

**现有调用链：** public `WhereIfNotEmpty` -> `SqlQueryOperationAccessor.Mutate` -> `WhereClause.WhereIfNotEmpty` -> 空值 return -> gateway 无条件 `MarkChanged` -> `SqlQuery.Touch()`。

**步骤：**

1. 在修改生产代码前，于 `SqlQueryLifecycleTest` 增加 public fluent 回归用例：初次 `ToSql()` 建立缓存，分别调用 `WhereIfNotEmpty` 的 `null`、空字符串、空白字符串，断言完整 SQL、参数集合、`_shapeVersion`、`_cachedVersion` 和 `CountingTestSqlBuilder.ToSqlCallCount` 均不变。
2. 增加真实非空 `WhereIfNotEmpty` 用例，断言完整 SQL、参数值/顺序、ShapeVersion 恰加 1、缓存只重新渲染一次。
3. 复验一个 mutation delegate 抛异常的现有/新增直接路径，断言异常前后的字段与 render count 不变；不得将“最终 SQL 相等”替代版本/缓存断言。
4. 审计所有 `SqlQueryOperationAccessor.Mutate`/`MutateBuilder` 调用点，把入口分为“输入已保证会变更”“允许 no-op”“可能抛出”；避免为了一个 `WhereIfNotEmpty` 将所有 Clause API 强行改成复杂返回协议。
5. 优先在 public extension 边界短路已定义的 no-op 输入；只有审计显示多个 gateway 调用无法在边界判断且确有统一 changed 信号时，才设计最小内部 `bool changed` 或 state callback 协议。协议必须不改变 public API、不吞异常、不重复 Touch。
6. 保留异常后的原始语义。不得通过预先 Touch 后回滚版本实现修复。

**验收：** 新旧 lifecycle tests 证明 null/empty/whitespace no-op 为零 Touch，real mutation 为一次 Touch，throw 为零 Touch；完整 SQL 断言而非 `Contains`；Data.Sql net8/net6 均通过。

### RC28-P2-02 [P1] 以消费者审计处理 `Builders.Internal.Helper` 的公开泄漏

**依赖：** RC28-P0-01。  
**目标：** 形成一个可维护的内部协作边界或最小正式 SPI，不再将多职责 `Helper` 作为未证明的公共合同。

**步骤：**

1. 搜索所有 framework source、Provider 项目、Analyzer consumer snippets、解决方案项目引用和可用 package/API baseline，记录每个 `Helper`/`JoinItem.SetDependency`/`JoinItem.Clone` 消费者。明确排除 `Bing.Data.Queries.Internal.Helper` 等不同类型。
2. 评审 `Helper` 当前承担的列解析、表达式值、条件、范围、参数、方言 SQL、映射与数据库上下文职责；将生产内部调用者按职责列在 `execution.md`。
3. 作出并记录不可混用的决策：
   - **分支 A，无外部合同：** 将 `Helper` 改为 `internal sealed`，将 `JoinItem.SetDependency(Helper)` 和 `Clone(Helper)` 也改为 internal；仅保留 `JoinItem` 对外真正需要的创建、条件和渲染表面。更新 Shipped baseline 删除的符号，并在 ReleaseNotes 标注为已批准主版本 Breaking Change。
   - **分支 B，有真实外部 Provider 合同：** 不保留名为 `Helper` 的大而全 public 类。为已经被证实的单一职责建立最小、命名明确、稳定异常语义的 public abstraction；迁移 `JoinItem` 暴露面和该消费者，其他 Helper 能力保持 internal。新增成员先进入 Unshipped，Shipped 改动遵循发布策略。
4. 不得因为测试方便新增 production `InternalsVisibleTo`。测试继续使用现有 Data.Sql Tests/Benchmarks friend assembly，外部消费者验证必须经 public surface 编译。
5. 更新 Analyzer compile contract：分支 A 证明普通第三方 consumer 无法构造/调用删除的 Helper/JoinItem internal dependency，同时官方 public SQL API 仍可编译；分支 B 证明唯一 SPI 可编译、无关 Helper 能力不可编译。
6. 增加直接 `JoinItem`/JoinClause clone/render 测试，断言完整 SQL、条件与 clone 独立性，确保可见性收敛没有修改运行行为。

**风险与迁移：** `PublicAPI.Shipped.txt` 已包含 17 条相关成员，不能视为私有重构。仓库无法证明外部 NuGet 消费者不存在，因此 ReleaseNotes、governance 和迁移说明必须明确 Breaking Change/替代路径；不得宣称“无外部消费者”。

**验收：** PublicApiAnalyzers 无 `RS0016/RS0017/RS0018`；官方 Provider/Analyzer consumer 编译通过；没有暴露大而全 `Helper` facade；直接 clone/render/consumer contract tests 通过。

### RC28-P2-03 [P2] 仅在正确性锁定后评估 mutation gateway 分配优化

**依赖：** RC28-P2-01。  
**目标：** 判断 capturing lambda 是否是可测量热点，避免以推测换取复杂度。

**步骤：**

1. 在 `SqlMutationBenchmarks` 或职责更匹配的新 benchmark type 增加 no-op、true mutation、cached render hit/miss、capturing callback 与 static/state callback 的独立基准。
2. 任何内部 state overload 必须在 benchmark 显示分配/吞吐问题且不破坏 Phase 2 正确性后才加入；不新增 public API，不将 benchmark helper 进入生产正常路径。
3. 所有 benchmark 结果均先视为实验；只有同机同键 FormalHost before/after 才能做性能结论。

**验收：** benchmark 方法的行为被 unit tests 锁定；没有以 DryJob 输出声称分配改善。

## 8. Phase 3 - P1 Benchmark 隔离与可比较 FormalHost

### RC28-P3-01 [P1] 拆分 SQLite E2E diagnostics 测量条件

**依赖：** RC28-P2-01。  
**目标：** listener-off、steady listener-on、subscribe-plus-query 真正独立，避免 `DiagnosticListener.AllListeners` 的全局订阅污染基线。

**步骤：**

1. 将共享数据库 seed、query factory、trace factory 和 cleanup 提炼为不创建 observer 的基础设施；默认 listener-off type 不得构造 `NoOpDiagnosticObserver`。
2. 使用独立 benchmark type/process invocation 区分：listener-off、steady-on（在该 type 的 setup 创建并仅在 cleanup dispose observer）、subscribe-plus-query（每次测量在 query 前创建并立即 dispose observer）。
3. 将 diagnostics 场景和 trace 场景与无监听基线分离；每个 type 的 setup validation 只调用自身不会反向改变 measurement condition 的路径。
4. 将 RowCount 仅保留在真正与行数成比例的 E2E list/stream/multi-map/multiple-query 场景。cardinality exception、预取消、固定诊断和固定 trace 场景拆入固定输入 type，减少无意义参数交叉乘积。
5. 保留真实 SQLite 文件、Dapper query、stream/dispose、2/5/7 mapping 和 cancellation 路径；不改为纯 mock 或生产专用快捷路径。
6. 如 `SqlMetadataBenchmarks.cs` 的 `Program.Main` 需要新增显式 host selection，只新增可审计 flag/type allow-list；保留现有 `--ci-smoke` 与 `--e2e-smoke` 语义，不让 smoke 误跑 FormalHost。

**验收：** source review 能证明 listener-off 不订阅；每种测量条件在独立 artifact 目录运行；Smoke 仍可启动并验证代表路径；没有 observer lifetime 泄漏到其它 benchmark。

### RC28-P3-02 [P1] 建立 FormalHost before/after 运行协议

**依赖：** RC28-P3-01、RC28-P0-02。  
**步骤：**

1. 在修改前后（若修改前已不可重建，则明确 `before=UNAVAILABLE`，不能伪造）运行完全相同的 benchmark type/method/params/filter/job。
2. 每次运行保存 BenchmarkDotNet 原始结果、report markdown/csv/json、environment 信息、命令文本和 SHA-256，并在 `artifact-index.md` 登记。
3. FormalHost 使用现有 `SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")`。Dry smoke 仅用于可启动性，不参与性能比较。
4. 比较 Mean、Error/StdDev、Allocated、Gen0/1/2；没有统计可比性或 case key 不同则结论为 `NOT_COMPARABLE`。
5. 对任何超过维护者已定义阈值的回归，定位到方法/参数并回退本任务新增的优化性改动或记录阻塞；不要把正确性修复包装为性能收益。

**验收：** `benchmark-report.md` 逐 case 写清 `PASS/REGRESSION/NOT_COMPARABLE/BLOCKED`，并指向具体 artifact hash；无 FormalHost 则报告 `BLOCKED/PARTIAL`。

## 9. Phase 4 - P0/P1 测试与真实集成执行

### RC28-P4-01 [P0] 单元、Analyzer、SQLite 回归矩阵

**依赖：** RC28-P1-03、RC28-P2-01、RC28-P2-02、RC28-P3-01 的相应实现。  
**目标：** 覆盖内部状态、公开编译契约和不依赖外部服务的真实执行。

**必须覆盖：**

- Data.Sql lifecycle 的 no-op/real/throw cache-version contract。
- `Helper` 最终可见性与 `JoinItem` clone/render public/consumer contract。
- Provider gate、runner self-test、SQL Server 环境变量禁并行与 startup routing。
- SQLite 真执行：source/join、CRUD、transaction commit/rollback、async cancellation、sync/async stream dispose、Dapper 2/5/7 mapping、nullable、large `IN`；现有用例已覆盖时记录实际测试方法，不重复造轮子。
- SQL 文本测试一律断言完整 SQL；缓存键/映射/Provider 分支有直接职责测试，不以综合测试间接替代。

**真实命令：**

```powershell
dotnet build .\Bing.All.sln -c Release -nologo -v quiet -clp:ErrorsOnly
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 --no-restore --nologo
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest
```

**验收：** `unit-test-report.md` 写入逐项目/TFM的 actual total/passed/failed/skipped、命令、开始结束时间、artifact path；历史计数不可复制为 current 结果。

### RC28-P4-02 [P0] 在受保护环境运行每个真实 Provider

**依赖：** RC28-P1-01、RC28-P1-02；维护者提供外部前置条件。  
**目标：** 用统一 runner 获得三个 Provider 的 real non-skip TRX/JSON，并真实覆盖查询、CRUD、transaction、async/cancel、stream/dispose、映射、nullable/large-IN 等各自实际支持的范围。

**步骤：**

1. 每个 Provider 在独立 job/数据库中仅设置对应 gate、对应 connection variable 和 reset authorization；执行前 runner 必须通过 safety preflight。
2. 以 runner 运行至少 net8.0：

```powershell
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider PostgreSql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider SqlServer -Framework net8.0 -Configuration Release
```

1. 对仍声明 `net6.0;net8.0` 的 Provider 项目，执行受保护的 representative `net6.0` runner run，或在 `integration-test-report.md` 标注环境/Provider 阻塞与未覆盖 TFM；不能因 `TestTfmsInParallel=false` 推断已执行。
2. 只在真实 fixture 可安全 reset/cleanup 时补充缺失的 gated test。每个新增测试使用既有 MySql/PostgreSql/SqlServer collection/fixture、中文测试目的、AAA、Provider gate，且不得读取受保护 appsettings/runsettings。
3. 将 MySQL cross-database optional skip 与 SQL Server local `MultiProviderQueryTest` 单列；它们不能让 core Provider skip 合法化。
4. 保存本任务的 non-skip TRX 和 runner JSON 到 artifact 目录，哈希后引用到 report；报告中列测试名称类别和计数，禁止写连接值。

**Mock 边界：** 仅 mock 时间、连接/IO 等外部系统的 unit test；真实 Provider 路径不可 mock 数据库执行。SQLite 是本地真实 integration，不可替代 MySQL/PostgreSQL/SQL Server 的方言/driver 证据。

**验收与阻塞：** 每 Provider 有 current non-skip TRX/JSON、核心 test 无 Skip、runner 成功和无密 CI artifact，才标该 Provider `COMPLETED`。没有 Provider 安全数据库、secret 或 CI 权限时，该 Provider `BLOCKED`，继续其他 Phase，整体不得 `COMPLETED`。

### 9.3 Given/When/Then 用例矩阵

| 层级 | Given | When | Then | 实现位置 |
| --- | --- | --- | --- | --- |
| Unit | 已缓存的 fluent query | `WhereIfNotEmpty(null/empty/whitespace)` | 完整 SQL、参数、ShapeVersion、cachedVersion、render count 不变 | `SqlQueryLifecycleTest` |
| Unit | 已缓存的 fluent query | `WhereIfNotEmpty(non-empty)` | 完整 SQL/参数变化，Touch 恰好一次 | `SqlQueryLifecycleTest` |
| Unit | mutation callback 将抛异常 | 调用 gateway 入口 | 异常传播，版本/缓存不变 | `SqlQueryLifecycleTest` 或直接 gateway contract |
| Unit | SQL Server process environment | 设置 global gate 的 Startup test 与其它同程序集测试 | collection 禁并行且 finally 恢复原值 | SQL Server integration infrastructure |
| Unit | 合法/非法 gate、safe DB、TRX | runner self-test/preflight | 仅 literal true、拒绝 global/default/zero/core-skip，JSON 无敏感 | runner + shared tests |
| API compile | 外部 consumer source | 尝试使用最终 Helper/JoinItem 边界 | 分支 A 不可编译；分支 B 仅最小 SPI 可编译 | Analyzer contract tests |
| SQLite Integration | 临时 SQLite 文件 | CRUD、transaction、cancel、stream dispose、2/5/7 mapping、nullable/IN | 真正 IO、资源释放和结果正确 | 现有 SQLite integration tests |
| Provider Integration | 专属 gate/安全测试 DB | runner 调用对应项目 | core non-skip TRX/JSON 与真实结果 | 三个 gated test projects |
| Benchmark | 同一 machine/key | listener-off/steady-on/subscribe-on FormalHost | 仅可比 case 比较 Mean/Allocation/GC | BenchmarkDotNet artifacts |

## 10. Phase 5 - 文档、追溯与发布证据

### RC28-P5-01 [P1] 同步操作文档与发布说明

**依赖：** Phase 1-4 实际结果。  
**步骤：**

1. 更新 `docs/integration-testing.md` 和 `docs/testing/database-integration-tests.md`：准确说明四 lane 调度方式、trusted job 边界、Provider 专属变量、显式 runsettings、runner artifacts、global/default 禁止规则和实际可用命令。
2. 仅在 README 确有合适操作入口时添加简短 SDK/CI/RC 文档链接；不添加无法验证的 badge、远端 URL 或“所有 Provider 已通过”表述。
3. 更新 ReleaseNotes：将未绑定 current artifact 的绝对测试数字替换为有版本/commit/报告范围的陈述，或保留为历史记录并明确历史 provenance；记载 Helper 最终 Breaking Change 和唯一迁移路径。
4. 更新 `ai_docs/sql-public-api-governance.md`：记录 Helper 分支决策、PublicAPI Shipped/Unshipped 策略、禁止恢复 legacy API，以及 Provider SPI 的最小化理由。
5. 更新 `ai_docs/sql-metadata-test-traceability.md`：维护最终“生产符号 -> 测试方法”映射，至少包含 `SqlQueryOperationAccessor`、`WhereClauseExtensions.WhereIfNotEmpty`、最终 Helper/JoinItem 边界、runner/gate safety contract、benchmark 隔离理由和相应项目/方法。对 benchmark 写明是 smoke、FormalHost、not comparable 或 blocked。

**验收：** 文档中的变量名、命令、TFM、Public API 和 status 与当前源码/artifact-index 一致；没有虚构结果、链接、计数或敏感配置。

### RC28-P5-02 [P1] 形成强制报告闭环

**依赖：** Phase 4、Phase 3。  
**步骤：**

1. `unit-test-report.md`：列出 build、unit/analyzer、internal contracts、命令、TFM、结果与失败/skip 分类。
2. `integration-test-report.md`：分别列 SQLite 已执行结果与 MySQL/PostgreSQL/SQL Server 的 real/blocked 状态；默认 Skip/DI tests 单独标记，不计入真实 Provider。
3. `benchmark-report.md`：拆分 Smoke、FormalHost before、FormalHost after、GC/Allocation、比较结论；无 artifact 时明确 `BLOCKED/NOT_COMPARABLE`。
4. `verification-report.md`：逐 Phase 汇总 acceptance，引用报告和 artifact hash，列出仍剩余风险。
5. `artifact-index.md`：验证所有引用文件存在、哈希匹配、无敏感扫描命中；对远端 artifact 只记录已导入/可审计 ID，不捏造外链。

**验收：** 八份报告都存在且状态互相一致。任一强制报告缺失、Provider/FormalHost 被阻塞却未写解除条件时，任务只能 `PARTIAL/BLOCKED`。

## 11. Phase 6 - 独立 Review 与修复闭环

### RC28-P6-01 [P0] 独立审查

**依赖：** Phase 0-5。  
**步骤：**

1. 由独立 reviewer 对 plan、execution、全部报告、git diff、PublicAPI、真实源码、测试、CI 配置和 artifact-index 审查。
2. Review 必须优先验证：受保护配置未变更、CI secret boundary、no-op/cache correctness、Helper public surface、listener isolation、Dry/FormalHost 区别、Provider TRX non-skip 和文档声明一致性。
3. `review.md` 给出 machine metadata、严重度、`FIX-xxx`、对应计划项、可复现证据、验收命令和 `PASS/PASS_WITH_ISSUES/NEEDS_FIX`。

### RC28-P6-02 [P0] 按 review 修复并回归

**依赖：** RC28-P6-01。  
**步骤：**

1. 使用 `fix-review` 的 `recommended` 范围处理 MUST_FIX 与 SHOULD_FIX；未经用户要求不处理 OPTIONAL。
2. 不修改 `review.md` 作为修复手段；在 `execution.md` 记录每个 FIX 的根因、修改、测试和状态。
3. 对每个修复重跑最小直接测试，再按影响扩大到 Data.Sql/Dapper Core/SQLite/Provider/FormalHost；更新 reports/artifact index。
4. 直到 independent review 不再存在未解决 MUST_FIX/SHOULD_FIX，或外部阻塞有明确 `BLOCKED` 证据。

## 12. 最终验收门槛

### 12.1 可标记 `COMPLETED` 的全部条件

- [ ] 八份强制任务报告均存在、UTF-8、无敏感内容并在 artifact-index 互相可追溯。
- [ ] 受保护配置路径均未变更，且整个执行期未读取、输出或改写其内容。
- [ ] `common/mysql/postgresql/sqlserver` 有真实可调度 CI/job 配置，受保护 lane 的 remote policy/secret boundary 具有无密可审计证据。
- [ ] runner self-test、gate/环境并发 tests、Data.Sql、Analyzer、Dapper Core、SQLite net8 和代表性 net6 的当前结果全部记录并通过。
- [ ] 每个 MySQL/PostgreSQL/SQL Server 都有本任务 current non-skip TRX/JSON，core Provider test 无 Skip，且真实执行覆盖矩阵有可追溯说明。
- [ ] mutation no-op/real/throw 直接测试通过，完整 SQL/版本/cache 断言齐全。
- [ ] `Helper` 最终 public/internal 决策已完成，PublicAPI/analyzer/consumer/clone tests 通过，迁移说明已更新。
- [ ] diagnostics benchmark 条件隔离，Smoke 与 FormalHost 结论分开；before/after 满足同一 comparability key，或没有性能声明。
- [ ] 文档、ReleaseNotes、governance、traceability 与 current source/artifacts 一致。
- [ ] 最终 independent review 不含未处理的 MUST_FIX/SHOULD_FIX。

### 12.2 必须标记 `PARTIAL` 或 `BLOCKED` 的条件

出现任一项时，不得使用 `COMPLETED`：

- 任一要求报告未创建或没有解除条件。
- 任一 Provider 只有 default Skip、self-test、ValidateOnly 或静态 CI route，缺少 current non-skip TRX/JSON。
- 远端 CI secret/trusted-lane/required-check 状态不可验证。
- FormalHost before/after 的 source/environment/case key 不可比或制品不存在。
- Provider 安全数据库/reset 授权/CI 权限未由维护者提供。

阻塞报告必须写明：阻塞 Provider/Phase、未执行命令、所需变量名称或权限类别（不写值）、安全数据库规则、维护者操作、恢复后要执行的命令，以及继续完成的无依赖工作。

## 13. 实施后顺序

1. 通过 `/execute-plan BING-SQL-RC-HARDENING-20260828-004` 进入执行模式。
2. 执行器从 RC28-P0-01 开始，先建立报告与安全基线，再做任何代码或 CI 修改。
3. 不自动 commit、push、创建 PR、触发外部 CI 或连接数据库；外部 Provider 运行必须在维护者已授权的受保护环境中执行。
