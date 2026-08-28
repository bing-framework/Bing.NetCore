# Bing.Data.Sql RC 验证、Provider 门控修复与性能基线固化实施计划

- Task ID: `BING-SQL-RC-HARDENING-20260827-003`
- 计划日期: `2026-08-28`
- 状态: `pending`
- 范围: `Bing.Data.Sql`、`Bing.Dapper.Core`、MySQL/PostgreSQL/SQL Server 集成测试、共享门控、AppVeyor/Travis、Benchmark、SQL 文档与发布验收材料。
- 当前分支证据: `.git/HEAD` 指向 `dev_v6.0-refactor-sqlquery`；当前 ref 文件记录的提交为 `3059a5971e0d3b52705a8d63eb077763e61d3a9d`。执行器必须在 Phase 0 用 `git rev-parse HEAD`、`git status --short --untracked-files=all` 和 diff hash 重新确认，不能把本计划的静态读取当作最终提交证据。
- 执行边界: 不自动执行 `git add`、commit、push、PR、Tag、发布、凭据传递、破坏性清理或历史重写。保留现有工作树改动；外部 Provider 仅能操作安全校验通过的专用测试数据库。
- Planner 写入边界: 本轮仅创建本文件。执行器在 Phase 0 创建并维护 `execution.md`、`progress.md`、`verification-report.md`、`benchmark-baseline.md` 和 `review.md` 所需的执行记录；`review.md` 保持 Reviewer 独立证据，执行器不篡改其结论。

## 1. 输入、约束与冲突裁决

### 1.1 已读取的约束和资料

- 仓库规则: `AGENTS.md`、`.github/copilot-instructions.md`、`.github/prompts/create-plan.prompt.md`。
- 上一轮 RC 证据: `ai_docs/tasks/BING-SQL-RC-HARDENING-20260826-002/{plan.md,execution.md,verification-report.md,review.md}`。
- SQL 治理和使用文档: `ai_docs/sql-public-api-governance.md`、`ai_docs/sql-metadata-test-traceability.md`、`ai_docs/stage-08-multidatabase-completion.md`、`docs/integration-testing.md`、`docs/testing/database-integration-tests.md`、`docs/sqlquery-usage.md`。
- 当前实现/构建证据: `Bing.All.sln`、`appveyor.yml`、`.travis.yml`、`framework.props`、`framework.tests.props`、`common.tests.props`、`.gitignore`、Provider 项目与共享测试代码。

用户引用的 `Bing.Data.Sql-全面审查报告-20260827.md` 未在当前工作区中找到。因此“89%/95%/93%/88%/74%/78%”只能作为需求输入的历史静态评分，不能代替当前提交的 build、测试、CI 或 Benchmark 证据。

### 1.2 冲突与处理原则

| 冲突或偏差 | 当前证据 | 本计划裁决 |
| --- | --- | --- |
| 输入快照称三个 `integration.runsettings` 缺失 | 三个 Provider 项目目录当前均存在同名文件，项目也通过 `RunSettingsFilePath` 引用它们 | 以当前源码为准。它们不是“缺文件”问题，而是包含敏感配置、全局启用开关和不可复现优先级的问题。 |
| 输入快照称已有 `global.json` | 根目录当前不存在该文件，且 `.gitignore` 包含 `/global.json` | SDK 固定尚未闭环。必须先解除忽略规则，再按 Phase 0 实测 SDK 选择受支持的 .NET 8 SDK 固定版本。 |
| 前序任务声称 AppVeyor 已现代化和 SDK 已 pin | 当前 `appveyor.yml` 确为 VS2022，但仍只有 common lane；当前 `.travis.yml` 仍为 Xenial + .NET 2.2；根目录没有已跟踪 `global.json` | 只承认 VS2022、TRX/coverage/benchmark artifact 配置已存在；Provider 专用 CI、Linux/net8 和 SDK 文件仍未完成。 |
| 前序任务的外部 Provider/Benchmark 阻断 | `review.md` 明确要求外部安全环境、远程 CI 和可审计 before 身份；当前代码仍无 Provider job 或 TRX 执行数门禁 | 继续视为阻断，不使用全 Skip、历史 partial Benchmark 或本地等价命令宣称 RC 通过。 |
| 旧文档指导 `tests/runsettings/*.example` | 该目录及示例不存在；当前真实文件在 Provider 项目目录 | 统一为无密本地显式 `.local.runsettings` 或受保护环境变量；移除不存在路径。 |

### 1.3 不变量和安全边界

1. Provider 可运行条件为：`RUN_INTEGRATION_TESTS=true` **或**对应规范 Provider 变量为 `true`。`RUN_INTEGRATION_TESTS=false` 不得覆盖 Provider 专用变量的 `true`。
2. 规范 Provider 变量固定为 `RUN_MYSQL_INTEGRATION_TESTS`、`RUN_POSTGRESQL_INTEGRATION_TESTS`、`RUN_SQLSERVER_INTEGRATION_TESTS`。不新增 `RUN_PGSQL_INTEGRATION_TESTS`、`RUN_POSTGRES`、`RUN_NPGSQL` 等长期别名；当前未实现该别名，保持其无效。
3. Provider CI 必须使用 Provider 专属连接字符串，不接受 `ConnectionStrings__DefaultConnection` 回退；本地兼容回退是否保留必须有直接测试和文档边界。
4. 所有会初始化、清理或重置数据库的路径必须经 `IntegrationDatabaseSafetyValidator`，数据库名必须具有允许后缀且不含系统库、`prod`、`production`、`development` 等危险环境 token，并显式设置 `ALLOW_DATABASE_RESET_FOR_TESTS=true`。
5. 凭据不能写入 YAML、csproj、runsettings、测试输出、TRX、coverage、Benchmark、诊断、任务报告或命令行。任何已暴露的密码、连接信息和访问令牌必须由其所有者在外部系统中轮换；本任务不得尝试把历史重写或密钥轮换自动化。
6. 现有已完成的 public API 收敛不得回退：高层 `FromTable`、高层 `ClearSelect`、旧多字符串 Join、泛型 facade 和 `.As<TResult>()` 继续不存在；Raw 表来源使用 `Query().From(...)`，`Select` 替换、`AppendSelect` 追加，复杂 Join 使用值类型 `SqlJoinOptions`。
7. SQL 输出测试继续断言完整 SQL 文本和参数顺序/值；不得将 `Contains` 用作 SQL 行为验收。不得新增生产程序集 `InternalsVisibleTo`。

## 2. 当前实现、完成度与风险评估

### 2.1 已有真实实现，应保留并重新验证

| 能力 | 当前代码/测试证据 | 判断 |
| --- | --- | --- |
| Fluent mutation 缓存失效 | `SqlQueryOperationAccessor.Mutate/MutateBuilder` 在成功后调用 `MarkChanged`；`SqlQueryLifecycleTest` 覆盖 Where、Union、CTE、AddParam、ClearParams、no-op、clone/参数失败 | 已有生产实现和直接 Unit，不是占位。仍需对完整 mutation family 和真实 SQLite 执行回归。 |
| Select/Join API 收敛 | `SqlLambdaQuery` 提供 `rightAlias` 与 `SqlJoinOptions` 两类 Join；`SqlJoinOptions` 为值类型；API contract/Analyzer 断言旧入口不存在 | 已完成，保持 API baseline，不安排恢复兼容层。 |
| 多结果集释放 | `SqlMultipleQueryResult.DisposeAsync` 使用 callback 断开和 reader/lease 生命周期；`SqlMultipleQueryResultLifecycleTest` 覆盖 callback 清理、exactly-once、异常顺序、弱引用 | 已有直接 Unit 和 SQLite 集成测试。外部 Provider 多结果集仍无真实执行矩阵。 |
| SQLite 无外部依赖链路 | `Bing.Dapper.Sqlite.Tests.Integration` 不设 Provider gate，且含流式、取消、分页、Select replacement、多个结果集和真实文件数据库用例 | 已具备可重复的 common-lane 核心证据。 |
| Provider 安全守卫 | `IntegrationTestGate` 实现 global OR provider；`IntegrationDatabaseSafetyValidator` 校验库名、gate 和 reset；三个 fixture 启动时调用验证器 | 基础实现真实存在，但真值矩阵、CI 专属连接限制和实际运行证据未闭环。 |
| Provider 项目入 Solution | `Bing.All.sln` 包含 MySQL、PostgreSQL、SQL Server 三个集成项目，并有 Debug/Release `Build.0` | 已完成，不能把 Provider 未执行归因于 solution 漏项。 |
| Public API baseline | Data.Sql、Dapper Core 及各 Provider 都已有 `PublicAPI.Shipped.txt`/`Unshipped.txt`，相关生产项目引用 PublicApiAnalyzers | 基线文件和 analyzer 已存在；需要验证每个 common/Provider 构建实际执行 analyzer，而不是新建重复基线。 |
| Benchmark 基础 | Root/Join/IN/Raw、Mutation、SQLite/Dapper E2E、CI Dry smoke 均已有 BenchmarkDotNet 类；Root/Join 参数已是 `1/2/5/10`，IN 为 `0/1/10/100/500/1000/2100` | 有可运行骨架和局部 smoke，但当前正式 before/after、Raw 纯度、诊断订阅边界、mutation closure 场景和统一 provenance 尚未达到 RC 证据要求。 |

### 2.2 当前缺口和影响

| 维度 | 证据 | 状态与风险 |
| --- | --- | --- |
| 明文机密 | 三个 Provider `integration.runsettings` 目前存在，且包含连接配置、全局 gate 与重置授权；文件名未被 `.gitignore` 排除 | **P0 安全风险。** 必须先轮换对应凭据，随后移除所有明文值和启用开关。执行器不得在日志、计划外报告或聊天中复述其内容。 |
| common lane 覆盖环境 | `appveyor.yml` 以同一命令将 global 和所有 Provider gate 设为 `false`，且没有 Provider lane | **P0 CI 缺口。** 该命令覆盖安全环境值；结合已存在 runsettings 的实际优先级未被当前提交证明，不能认定 common lane 行为安全。 |
| Provider 实际执行判定 | 当前没有 preflight、TRX parser、Provider 单独命令或 machine-readable 执行数摘要 | **P0 发布风险。** 仅 Skip 的 `dotnet test` 可返回 0，绿色不能代表 Provider 测试运行。 |
| 连接字符串边界 | `IntegrationTestConnectionStringResolver` 支持 Provider 专属值后回退 `ConnectionStrings__DefaultConnection`；CI 没有拒绝此回退 | **P0 配置隔离风险。** Provider lane 可串库，必须在 CI preflight 强制专属变量。 |
| gate 测试 | `IntegrationTestGateTest` 有 global/provider/大小写/安全库名/部分 resolver 测试，但没有 `global=false + provider=true`、`false`、`1/yes/on/blank`、SQL Server connection 变量保存恢复或 PG 别名拒绝全矩阵 | **P0 直接测试缺口。** 测试修改进程环境，必须保持测试集合不并行且恢复所有变量。 |
| runsettings/文档 | csproj 直接引用当前 runsettings；resolver 异常和文档引用不存在 `tests/runsettings/*.example` | **P0 可复现性和开发体验缺口。** 无密 checkout 与本地/CI 行为目前不能由文档重现。 |
| Provider 覆盖深度 | 静态属性计数约 MySQL 48、PostgreSQL 38、SQL Server 4；SQL Server 仅常量、聚合/OUTPUT、CTE 等少数用例 | **P1 RC 功能缺口。** 三个 Provider 尚未覆盖要求的来源/Join、多映射、流式、事务和异常矩阵。 |
| CI 平台 | `.travis.yml` 仍为 Xenial/.NET 2.2；当前根目录没有 `global.json`，且 `.gitignore` 忽略它 | **P1 发布工程缺口。** Windows/现代 Linux/net8/固定 SDK 没有同一版本的成功证据。 |
| 性能结论 | 前序审查已判定旧 Root/Join before provenance 无效；当前 Benchmark 未记录统一 commit/dirty/source hash；Raw benchmark 在被测方法内构造 raw 字符串，E2E 每次 `QueryWithDiagnosticListener` 订阅 | **P1 性能证据不足。** 不得宣称低 GC、无回归、0 GC 或 Near-Zero Allocation。 |
| 目录和复杂度 | `SqlLambdaQuery`、`SqlBuilderBase`、`SqlQueryBase`、`SqlMultipleQueryResult`、大型 SQLite 集成和 E2E Benchmark 都承担多类职责；`SqliteDapperE2EBenchmarks` 已同时承载 14 条工作流和 setup 验证 | **P2 可维护性风险。** 必须在正确性契约锁定后局部拆分，不能用更多 public interface/factory 加层。 |

### 2.3 针对必须回答问题的结论

1. **当前已经实现了什么**：第 2.1 节列出的 API 收敛、mutation cache、multiple-result 生命周期、SQLite 执行、基础 gate/safety、Public API baseline 和 Benchmark harness 都有源码与直接测试证据。
2. **已完成/部分完成/未完成**：API 收敛和核心 SQLite 正确性已完成；CI/Provider gate、跨 Provider 执行、SDK/Linux 和性能 RC 证据部分完成或未完成；具体以第 2.2 节和 Phase 状态为准。
3. **是否只是骨架**：核心 API、cache 和 multiple-result 不是骨架；Provider integration 测试也不是空项目，但当前未经受保护环境实际执行，不能表述为 Provider RC 已通过。
4. **整体完成度**：静态实现可评为“功能基础较完整”，但 RC 完成度为 **partial**。历史评分不可复核；当前 RC 的 P0 机密处置、Provider non-skip gate 和真实 Provider run 缺失，故不接受“发布就绪”。
5. **缺少工作**：凭据处置、真实门控/CI、TRX fail-fast、Provider 矩阵、SDK/Linux、有效 benchmark baseline、文档与最终制品。
6. **性能/资源问题**：暂无有效前后对比；Raw benchmark 包含字符串构造，诊断 case 包含订阅成本，E2E 指标跨不同 RowCount/行为混合，不能直接比较；当前实现也没有 `Allocated=0` 证据。
7. **复杂度/耦合**：共享 gate 与 fixture 已提供合理边界，但 CI 配置与测试配置相互覆盖；大类/大测试文件和 Benchmark 混合职责会抬高维护成本。
8. **使用/开发体验**：规范变量已较清晰，但现有 runsettings、错误消息和文档路径冲突，且 common/provider lane 的期望 Skip 语义没有明确面向开发者的实现。
9. **API 合理性**：SQL 公共 API 的推荐路径、结果终结和 `SqlJoinOptions` 已基本合理；本任务不新增业务 API。配置 API 需收敛到唯一规范变量和安全启动方式。
10. **冗余/兼容**：Public API 的历史兼容入口已删除；唯一需要治理的是文档中残余旧配置路径、CI 中覆盖 gate 的重复策略以及 SQL Server Startup 的跨 Provider 注册是否仍有真实消费者。
11. **可合并/废弃/非公开项**：不恢复已删除 API；Runtime SPI 仅按真实跨程序集 consumer 保持公开。`SqlQueryOperationAccessor` 等 internal 实现可移动目录但不改变可见性。配置上不增加 PG 别名。
12. **大杂烩问题**：候选大类和大测试文件见 Phase 4；只有锁定现有行为后才用 partial/内部协作者拆分。
13. **测试质量**：SQL/SQLite/multiple-result 有直接证据；gate 真值、CI parser 和外部 Provider 的真实执行/异常边界仍不足。Provider Skip 不能算通过。
14. **文档**：必须同步集成测试、CI/Provider 表、迁移/ReleaseNotes、README SDK/CI 表述和追溯矩阵；所有示例不得包含实际 secret。

## 3. 目标状态

### 3.1 Provider 运行矩阵

| Lane | 运行范围 | Gate | 连接配置 | 必须结果 |
| --- | --- | --- | --- | --- |
| common Windows | Unit、Analyzer compile-contract、SQLite Integration、Public API analyzer、CI Dry/E2E smoke | 不继承/不设置外部 Provider gate | 无外部连接字符串 | 无外部连接；SQLite 不得意外 Skip。 |
| MySQL protected | 仅 `Bing.Dapper.MySql.Tests.Integration.csproj` | `RUN_MYSQL_INTEGRATION_TESTS=true` | 仅 `ConnectionStrings__MySqlConnection` + reset=true | core Provider 用例至少执行 1 个，gate Skip 为 0；跨库专项可按独立开关 Skip 并单列。 |
| PostgreSQL protected | 仅 `Bing.Dapper.PostgreSql.Tests.Integration.csproj` | `RUN_POSTGRESQL_INTEGRATION_TESTS=true` | 仅 `ConnectionStrings__PostgreSqlConnection` + reset=true | core Provider 用例至少执行 1 个，gate Skip 为 0。 |
| SQL Server protected | 仅 `Bing.Dapper.SqlServer.Tests.Integration.csproj` | `RUN_SQLSERVER_INTEGRATION_TESTS=true` | 仅 `ConnectionStrings__SqlServerConnection` + reset=true | core Provider 用例至少执行 1 个，gate Skip 为 0。 |
| modern Linux/net8 | common matrix，按受控条件可运行同一 Provider runner | 同上 | secret 仅在 protected lane 注入 | 成功记录绑定提交、SDK、TFM、OS 和 artifacts。 |

Provider lane 不得设置 `RUN_INTEGRATION_TESTS=true`，不得将其它 Provider 设置为 `true`，不得使用 `DefaultConnection` 兜底。MySQL 跨库能力继续由 `BING_INTEGRATION_MYSQL_CROSS_DATABASE=true` 和安全的第二测试库单独启用；它不是 core gate 的替代品。

### 3.2 无密 runsettings 方案

选择“已提交安全默认 runsettings + 显式本地覆盖”方案，不依赖不存在的目录：

- 保留三个项目已有的 `integration.runsettings` 路径，但内容只保留不含环境变量、不含连接信息的有效 `<RunSettings>` 默认结构；它们不得启用任何 gate。
- 本地开发者在项目目录复制/新建 `integration.local.runsettings`，该文件由现有 `*.local.runsettings` 忽略；使用 `dotnet test --settings <explicit-local-path>` 显式传入。受保护 CI 使用环境变量，不使用 local 文件。
- 当接收 Provider CI 时，preflight 只读取“是否存在”及数据库名安全性，不输出连接字符串、用户、主机、端口或密码。
- `IntegrationTestConnectionStringResolver` 的提示改为说明规范环境变量和文档入口，不得指向不存在的示例文件。是否保留 `DefaultConnection` 仅作为本地兼容回退，须在文档中明确“CI 禁止”并有单元测试。

### 3.3 Provider 执行门禁协议

新增一个单职责、可在 Windows/Linux PowerShell 运行的 CI runner（路径在 Phase 2 确定），输入为 Provider、csproj、TFM、TRX 输出目录。它必须：

1. 校验 Provider 映射、规范 gate 解析为 `true`、Provider 专属连接变量存在、reset 开关为 `true`、项目存在；从连接字符串只提取数据库名称供 `IntegrationDatabaseSafetyValidator` 等价规则判断。
2. 在 build 已完成后用 `dotnet test <provider-project> -c Release --no-build -f <tfm>` 运行，生成每 Provider/TFM 唯一 TRX。
3. 解析 TRX 并输出不含 secret 的 JSON/Markdown 摘要：Provider、TFM、discovered、passed、failed、skipped、executed、duration、commit、lane。
4. 当总测试数为零、`passed + failed` 为零、任何 core Provider test 因 `RUN_*_INTEGRATION_TESTS` gate Skip，或 preflight 失败时非零退出。MySQL 跨库专项的已知独立 Skip 只能按明确原因另计，不能掩盖 core skip。
5. 不把 Skip 当作 pass；common lane 的外部 Provider Skip 只允许作为“未选中项目”的结果，Provider lane 中则为失败。

## 4. 修改范围

### 4.1 已确认文件

配置、CI 与安全：

- `appveyor.yml`
- `.travis.yml`
- `.gitignore`
- `framework/tests/compose/sql-providers.compose.yml`
- `framework/tests/Bing.Dapper.MySql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/integration.runsettings`
- 三个对应 `*.Tests.Integration.csproj`

共享 gate/安全与测试：

- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestGate.cs`
- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestConnectionStringResolver.cs`
- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationDatabaseSafetyValidator.cs`
- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationFactAttribute.cs`
- `framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestGateTest.cs`

Provider fixture/测试：

- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Infrastructure/MySqlIntegrationDatabaseFixture.cs`
- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Infrastructure/MySqlCrossDatabaseFactAttribute.cs`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Infrastructure/PostgreSqlIntegrationDatabaseFixture.cs`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Startup.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerIntegrationDatabaseFixture.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/DatabaseScript.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Startup.cs`
- 现有 MySQL/PostgreSQL/SQL Server `SqlQuery`、`SqlExecutor` 集成测试及其 collection 定义。

正确性、API 与 Benchmark：

- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryResultLifecycleTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteMultipleQueryIntegrationTest.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqliteDapperE2EBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMutationBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj`
- `framework/src/Bing.Data.Sql/{PublicAPI.Shipped.txt,PublicAPI.Unshipped.txt}`
- `framework/src/Bing.Dapper.Core/{PublicAPI.Shipped.txt,PublicAPI.Unshipped.txt}`

文档：

- `docs/integration-testing.md`
- `docs/testing/database-integration-tests.md`
- `docs/sqlquery-usage.md`
- `docs/ReleaseNotes.md`
- `README.md`
- `ai_docs/sql-public-api-governance.md`
- `ai_docs/sql-metadata-test-traceability.md`

### 4.2 条件候选文件

- 新增 `global.json`，且从 `.gitignore` 的 `/global.json` 规则中移除；具体版本只在 Phase 0 的本地与 CI 可用 SDK 确认后确定。
- 新增最小 CI runner/TRX parser，例如 `eng/ci/Invoke-ProviderIntegrationTests.ps1` 和同目录无密 test fixture；不引入 Pester 或新的 CI 平台依赖，除非当前平台已明确提供。
- `framework/tests/Bing.Dapper.{MySql,PostgreSql,SqlServer}.Tests.Integration/integration.runsettings.example`，仅在无密示例确能帮助本地显式运行时添加；不得复制真实 settings 值。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery*.cs`、`Builders/Core/SqlBuilderBase*.cs`、`framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase*.cs`、`SqlMultipleQueryResult*.cs`、大型测试 partial 文件。仅在 Phase 4 的职责审计证明拆分能降低复杂度时修改。
- AppVeyor 项目级 protected variables、计划/手动触发器、远程 artifact 保留策略属于外部配置，不可由本地 YAML 单独完成；需由具备权限的维护者配置并留下无密作业记录。

### 4.3 明确不改动

- 不恢复已删除的 `FromTable`、高层 `ClearSelect`、旧 Join overload、泛型 facade 或 `.As<TResult>()`。
- 不新增 production `InternalsVisibleTo`、不把 runtime SPI internal 化到破坏官方 Provider 编译、不给 `RUN_PGSQL_*` 增加兼容别名。
- 不连接生产/开发/系统数据库；不在测试中使用 sleep、公网依赖或随机等待。
- 不因 CI 绿色而删除测试、弱化断言、吞异常或扩大 Skip。

## 5. 用例矩阵与 Mock 边界

| 范围 | Given | When | Then | 测试类型 |
| --- | --- | --- | --- | --- |
| Gate 真值 | global/provider 均 unset | 读取 MySQL、PostgreSQL、SQL Server | 均返回含规范变量名的 skip reason | Unit |
| Gate OR | global=true/provider unset；global=false/provider=true | 判定指定 Provider | 两种均启用 | Unit |
| Gate 非真值 | `false`、`1`、`yes`、`on`、空白、非法字符串 | 判定 Provider | 均不启用，原因不泄密 | Unit |
| PostgreSQL 命名 | 仅 `RUN_PGSQL_INTEGRATION_TESTS=true` | 判定 PostgreSql | 仍 Skip，提示仅规范变量 | Unit |
| 环境隔离 | 各 gate、reset、default 和三项 Provider connection env 已有初始值 | 测试修改后 Dispose | 包括 SQL Server connection 在内的全部变量恢复 | Unit，禁并行集合 |
| 连接解析 | Provider 专属与 default 同时存在；仅 default；均缺失 | Resolve | 专属优先；仅本地兼容路径回退；缺失异常只含变量/文档提示 | Unit |
| CI preflight | 专属 gate/connection/reset/安全库名完整或缺任一项 | runner validate-only | 合法值通过；缺失/危险库/仅 default 均失败且不输出敏感值 | 脚本自检 |
| TRX 门禁 | passed Provider TRX；全 skip TRX；零测试 TRX；MySQL cross-db 可选 Skip TRX | parser | core 执行数为零或 core skip 失败；可选 Skip 单列，不掩盖结果 | 脚本自检 |
| 安全库 | `_test/_tests/_integration/_integration_test` 与 system/prod/development/空库名 | 验证 reset | 仅允许安全专用库；异常不含密码 | Unit |
| Mutation | 已 `ToSql()` 的 Raw/Lambda query | 每个 mutation family、no-op、后续失败 | 成功 SQL/参数/snapshot/shape 更新一次；no-op 与失败完全不变 | Unit + SQLite |
| Select/Multiple | 替换/追加 projection；callback、reader、lease 可控失败 | Select、AppendSelect、Read/Dispose/DisposeAsync、预取消 | 旧 API 不存在；SQL 完整；资源 exactly-once、异常顺序稳定 | Unit + SQLite |
| Provider core | 1/2/5/10 sources；2/5/10 joins；映射、流、事务、取消/异常 | provider-specific gate 已启用 | 完整 SQL/结果/资源释放按 Provider 方言成立 | 真实 Integration |
| Public API | baseline 与 consumer source | build/analyzer/compile contract | 未声明 API 变化失败；删除入口不可编译；推荐路径可编译 | Analyzer + build |
| Benchmark | 固定源码/环境/params | formal before/after | 每 case 可匹配，报告含 Mean/Median/Allocated/Gen 和 provenance | BenchmarkDotNet |

Mock 仅用于时钟、连接、reader、lease、事务、日志和外部资源等系统边界。不得 mock `SqlQuery`、Builder 或 clause 的内部调用来替代 SQL、参数、状态、执行快照和真实数据库结果。外部 Provider 的功能断言必须在其真实安全数据库上运行。

## 6. 分阶段实施计划

### Phase 0 - 取证、机密处置与可重放基线

**状态: `pending`**  
**依赖: 无**

#### RC27-P0-01 [P0] 建立任务和工作树证据

- 目标: 在任何行为改动前绑定当前源码、dirty 状态、SDK/Runtime、环境和现有测试发现结果。
- 修改范围: 本任务执行记录，不改生产行为。
- 步骤:
  1. 创建 `execution.md`、`progress.md`、`verification-report.md`、`benchmark-baseline.md`，记录 HEAD、branch、status、diff stat/diff check、未跟踪文件、OS、CPU、内存、GC、PowerShell、`dotnet --info`、`dotnet --list-sdks`、SDK/runtime、TFM、BenchmarkDotNet 版本。
  2. 以只输出变量“存在/长度/规范布尔解析/数据库名是否安全”的方式记录 all gate、reset 与 Provider connection 配置；绝不打印连接字符串、用户名、主机、端口或密码。
  3. 对三个 Provider 项目分别先进行 test discovery/当前 gate 运行，保存独立 TRX，统计 discovered/passed/failed/skipped，并记录 Skip 原因分类。不得把缺 gate 的 Skip 记为通过。
  4. 验证当前 runsettings 的实际 MSBuild/VSTest 优先级：在隔离环境中仅观察 gate 是否被设置，不能连接任何外部服务。记录 common `set ...=false` 与 runsettings/environment 的最终优先级，作为 Phase 1 回归条件。
  5. 确认 `integration.runsettings` 是否已被 Git 跟踪、是否出现在历史提交、`global.json` 是否被 ignore、AppVeyor 默认分支/权限和 Travis 是否仍是 required check。无法访问远程服务时记录为外部阻断。
- 测试: 不运行有凭据的 Provider DDL/DML；可运行 test discovery、无外部 Unit/SQLite、`git diff --check`。
- 风险: 环境或 TRX 可能泄密。所有输出先脱敏，发现 secret 则立即停止上传该 artifact 并记录安全事件编号而非值。
- 验收: 每个 Provider 都能回答“项目是否被选中、发现数、执行数、Skip 数、Skip 原因、有效 gate、是否安全可运行”；当前 evidence 绑定具体 HEAD/dirty hash。

#### RC27-P0-02 [P0] 处置已暴露的测试凭据并建立无密默认设置

- 目标: 清除工作树中的明文连接信息和默认全局启用，消除干净 checkout 的隐藏外部执行依赖。
- 依赖: RC27-P0-01；凭据所有者确认轮换或撤销已开始。
- 修改范围: 三个 `integration.runsettings`、`.gitignore`、三个 Integration csproj、文档；必要时无密 `.example` 文件。
- 步骤:
  1. 由凭据/数据库所有者在外部系统轮换所有已暴露的 MySQL、PostgreSQL、SQL Server 账号和密码；评估仓库历史暴露范围。历史重写、force push 或删除远程记录均需单独书面授权，不能由执行器自行进行。
  2. 将三个已提交 runsettings 改成不设置 gate、reset 或连接字符串的安全有效默认值，或在确认调用方不依赖路径后移除该属性/文件。优先保留无密、路径存在的安全默认，以避免 clean checkout 的缺文件差异；不得提交 placeholder password。
  3. 保留 `*.local.runsettings` 忽略规则，按需要新增无密 `integration.runsettings.example`；本地运行必须显式 `--settings`，CI 使用 protected environment variables。
  4. 改正 resolver 和文档中不存在的 `tests/runsettings/*.example` 指引，明确无密设置路径、provider-specific env 和 CI 禁止 default fallback。
  5. 复查 `appsettings.json`、runsettings、YAML、任务文档和 VSTest 输出模板中是否还有硬编码连接或密码模式；不得把扫描命中的值写入报告。
- 测试: 加载安全 runsettings 后执行 Provider discovery，确认没有自动启用；运行 `IntegrationTestGateTest`；检查 Git diff 与受影响配置不含连接字段/密码字段。
- 风险: 仅删除文件而不轮换已暴露凭据不能完成安全处置；误设 `false` 到 runsettings 会再次覆盖 protected CI 的 `true`。
- 验收: 任何普通 checkout/test 不拥有外部连接信息且不会启动 Provider；CI/local 仍可通过明确 provider 环境或忽略的 local settings 启用；无新 secret 进入 diff/artifact。

#### RC27-P0-03 [P0] 建立当前 Benchmark 可比较身份

- 目标: 把当前已审计的 HEAD/dirty 状态建立为后续性能改动的 before，而不复用前序无 provenance 的历史结果。
- 依赖: RC27-P0-01。
- 步骤:
  1. 对 Benchmark 源、csproj、配置、产物命令、`global.json`（创建后）计算 SHA-256，并记录 branch、commit、dirty diff hash、OS/CPU/电源、SDK/runtime/JIT/GC、Job、TFM 和开始/结束时间。
  2. 旧 Round 3/Root/Join artifacts 仅列为历史参考，明确不可用于当前矩阵 delta；不从 CSV 反推旧源码。
  3. 为每个 case 定义 key: `Benchmark type + method + all Params + Job + runtime + TFM`；只比较完全相同 key。`Error` 不可替代 P95；如要 P95，必须有原始样本或独立采样来源。
  4. 先运行少量 Dry smoke 确认 current source 可执行，再在不混用 Dry/ColdStart/NA/中断结果的独立 artifacts 目录运行 FormalHost baseline。
- 验收: `benchmark-baseline.md` 可区分 current valid baseline、historical invalid 和 blocked；后续优化能在同一环境/Job/key 下获得 before/after。

### Phase 1 - Provider gate、runsettings 与安全预检正确性

**状态: `pending`**  
**依赖: Phase 0 完成；RC27-P0-02 必须先完成**

#### RC27-P1-01 [P0] 补齐 gate/connection/safety 直接单元测试

- 目标: 将 gate 语义、规范变量、环境恢复、连接优先级和 reset 安全性变成可重复的直接测试合同。
- 修改范围: `IntegrationTestGate*`、`IntegrationTestConnectionStringResolver`、`IntegrationDatabaseSafetyValidator` 与 `IntegrationTestGateTest`。
- 步骤:
  1. 使用表驱动测试覆盖 global/provider unset、global true、global false + provider true、provider false、大小写 `TrUe`、`1/yes/on/blank/invalid`；确保仅值为 `true` 才开启。
  2. 添加 PostgreSQL 规范名合同：`PostgreSql` 仅生成 `RUN_POSTGRESQL_INTEGRATION_TESTS`；设置 PGSQL 非规范变量不能启用，Skip reason 只指向规范名。
  3. 将 MySQL、PostgreSQL、SQL Server、Doris、Oracle gate、reset、default 和**所有** Provider-specific connection variables纳入 fixture 保存/恢复；修复当前 SQL Server connection 环境变量未加入隔离列表的缺口。
  4. 维持环境变量测试集合禁并行，审计同程序集是否有其它修改环境的测试；只在必要时扩大为程序集级串行，避免无关性能损失。
  5. 覆盖 Provider connection 优先、default 本地回退、Provider CI preflight 拒绝 default、缺值异常脱敏、安全库名和 reset 开关。异常断言只能检查变量名/安全原因，禁止断言实际连接文本。
- 测试: `Bing.Test.Shared` net8.0/net6.0；受影响 Data.Sql Unit。
- 风险: 进程级环境变量导致并发污染；必须由 finally/Dispose 恢复，不依赖测试执行顺序。
- 验收: 推荐 gate 语义和安全边界由直接 Unit 覆盖；所有修改 SQL/安全文本的断言具体且不泄密。

#### RC27-P1-02 [P0] 固化无密 runsettings 与 Provider Startup 边界

- 目标: 确保项目配置不会擅自启用 Provider，且 SQL Server/PG Startup 不为其它 Provider 注册或回退配置制造隐性依赖。
- 依赖: RC27-P1-01。
- 步骤:
  1. 应用第 3.2 节的无密 runsettings 决策；验证 `RunSettingsFilePath` 存在时的默认行为和 `--settings` 本地覆盖行为。
  2. 对 MySQL、PostgreSQL、SQL Server fixture 统一在使用连接前解析 provider-specific 配置、调用安全 validator，并清理 connection pool；gate 未开时 fixture 不连接、不建 service provider。
  3. 审计 SQL Server `Startup` 当前注册 MySQL/PostgreSQL provider 与三个 data source 的真实 consumer。若不是必需的 compile/integration contract，收缩为仅 SQL Server 注册；若必须保留，则明确其原因、禁止它读取其它 Provider 的连接配置，并添加启动级测试。
  4. PostgreSQL/SQL Server Startup 文档/注释改为规范 Provider gate，不再声称仅全局开关；所有入口在 CI preflight 前不得使用 default fallback。
- 测试: 对每个 fixture 测 gate off、gate on+安全 fake config、非法数据库名、缺专属配置；实际 Provider 只在安全环境中运行。
- 验收: 干净 checkout 的 Provider 项目默认全部 Skip 且不含连接尝试；专属启用时只有对应 Provider 被选择；无跨 Provider 隐式注册或连接回退。

#### RC27-P1-03 [P0] 保持核心 mutation/Select/multiple-result 行为

- 目标: 在修复运行环境前后确认前序 RC 正确性没有退化，并补齐仍缺的直接边界。
- 依赖: RC27-P1-01。
- 步骤:
  1. 以 mutation family 清单复审所有会改变 Raw/Fluent 描述的操作：Select/AppendSelect、From/Join、Where/Having/Group/Order、paging、Union/CTE、subquery、dynamic filter、AddParam/ClearParams。每类至少有成功、no-op、失败原子性和缓存/执行 snapshot 断言。
  2. 对 `Select<TEntity>(bool)` replacement、`AppendSelect` append、失败 projection 原子性保持完整 SQL/参数/shape version 断言。
  3. 增补 SQLite 多结果集对读取失败、预取消、提前 `Dispose`/`DisposeAsync`、重复释放和后续 executor 可用性的真实验证；维持 Dapper Core 中 callback/lease exception ordering 的直接 Unit。
  4. 不为本阶段重写公共 API。若发现 `SqlQueryOperationAccessor` 对某一 no-op 操作仍无条件 `MarkChanged`，先写失败测试，再进行最小修复并基准测量其闭包/分配影响。
- 验收: Data.Sql、Dapper Core、SQLite Unit/Integration 的 SQL、参数、异常与资源生命周期回归通过；发现的 MUST_FIX 在进入 Phase 2 前清零。

### Phase 2 - 可执行 Provider CI、TRX 非零门禁与平台矩阵

**状态: `pending`**  
**依赖: Phase 1 完成**

#### RC27-P2-01 [P0] 实现安全 Provider runner 和 TRX 结果门禁

- 目标: 让 Provider lane 能从显式安全环境运行、脱敏汇总并拒绝 all-skip/zero-test 假绿。
- 修改范围: 新增最小 `eng/ci` PowerShell runner/fixture，必要的 AppVeyor 调用；不引入新的测试框架或 secret 文件。
- 步骤:
  1. 实现第 3.3 节 runner，使用 UTF-8 PowerShell console/output 编码，参数限定为三个官方 Provider 和对应项目；禁止从调用方传入任意项目/Provider 映射绕过白名单。
  2. preflight 仅接受 provider-specific env；验证 gate、reset、项目路径和数据库名称。连接解析/错误输出不得显示完整字符串；拒绝 `DefaultConnection` 作为 Provider CI 输入。
  3. 对 `net8.0` 与 `net6.0` 分别生成唯一 TRX，例如 `<provider>-net8.trx`；解析 XML 得到 discovered/passed/failed/skipped/executed/duration。解析逻辑必须先以已提交的脱敏 fixture TRX 自检通过。
  4. 将 MySQL cross-database 作为单独 optional capability 统计，只允许明确的跨库 gate/配置缺失原因 Skip；其余 `IntegrationFact("MySql")` Skip、所有 PostgreSQL/SQL Server core Skip 和零测试均失败。
  5. runner 输出可上传的无密摘要，并在失败时给出“gate/专属连接/reset/数据库安全/TRX 执行数”分类，不输出凭据。
- 测试: runner `-SelfTest`/validate-only 覆盖第 5 节 TRX/预检矩阵；真实 Provider 运行由 protected lane 完成。
- 风险: TRX schema、adapter 输出或 optional skip 文案变化导致 parser 漏判。解析按 outcome 和测试身份/明确 allow-list 双重校验，测试 fixture 覆盖未知 outcome 为失败。
- 验收: Provider runner 对 all-skip、missing secret、default fallback、unsafe DB、zero test 和 core skip 都明确非零退出；合法 Provider run 输出独立可读摘要。

#### RC27-P2-02 [P0] 重构 AppVeyor 为 common 与 protected Provider lanes

- 目标: common lane 不覆盖/泄露 protected 配置，三个 Provider 有各自实际 test command 与 artifact。
- 依赖: RC27-P2-01、已轮换凭据。
- 步骤:
  1. 将 common 逻辑拆为独立安全命令/脚本：只运行 Unit、Analyzer、SQLite、Public API build 和 Dry/E2E smoke；显式清除外部 Provider 环境，而不是与 Provider job 共用将变量设 `false` 的命令。
  2. 在 AppVeyor 配置/受保护项目中建立 MySQL、PostgreSQL、SQL Server 专属 lane。每 lane build 一次，runner test 均使用 `--no-build`，只运行对应 csproj 和对应 TFM；每 lane只注入自己的 connection、gate、reset，不注入其它 Provider 配置。
  3. protected lane 的变量由 AppVeyor secure environment 管理。普通 PR/fork 只运行 common lane，不调度 Provider lane，也不以“成功 Skip”代表 Provider 已验证；受信分支、手动或定时触发 Provider lanes。
  4. 将现在的 `master` only/`skip_branch_with_pr` 与仓库实际默认分支、required check 和发布分支策略核实后更新。不得猜测默认分支；PR common lane 需要真实触发，protected lane 需要明确拒绝非受信来源。
  5. 上传每 TFM 的 TRX、脱敏摘要、coverage、PublicAPI 文本、common Benchmark artifacts；Provider artifact 路径独立，避免覆盖。上传前扫描/脱敏 TRX 和日志中可能的连接字段。
- 验收: `appveyor.yml` 中 common 和 Provider 工作流不共享 gate 覆盖命令；三个 provider lanes 都有可读调用路径和不可伪绿的 runner；远程作业由有权限维护者实际运行并保留链接/制品 hash。

#### RC27-P2-03 [P1] 固定 SDK 并替换过时 Linux CI

- 目标: 建立与当前目标框架一致的固定 SDK、Windows + Linux/net8 代表性证据。
- 依赖: RC27-P0-01，需确认 CI 中真实可安装版本。
- 步骤:
  1. 从 `.gitignore` 移除 `/global.json`，新增跟踪的 `global.json`，固定一个经本机与远程 CI 验证的 .NET 8 SDK 精确版本和明确 `rollForward` 策略。不得复用缺失的历史 10.0.300 声明或臆造版本。
  2. 核实 `.travis.yml` 是否仍为 required CI。若仍使用，则把 Xenial/.NET 2.2 替换为现代 Linux/net8 common lane；若已弃用，先取得维护者批准后移除配置并在分支保护中删除 required check。不得静默保留不会运行的 legacy CI。
  3. Linux common lane 运行 net8 Unit/Analyzer/SQLite 和 benchmark smoke；Provider 容器可复用现有 `framework/tests/compose/sql-providers.compose.yml`，但只有满足安全数据库、healthcheck、secret policy 和 runner 预检时才接入。使用 compose 中示例密码前必须评估其是否仅为本地 disposable 容器且不与真实环境混淆。
  4. Windows/Provider 与 Linux/common 至少分别记录 net8；net6 作为受影响项目的代表性回归按矩阵执行，不做无意义全排列。
- 验收: clean environment 用已跟踪 SDK 配置完成 Release build；现代 Linux/net8 有远程成功记录；过时 Travis 不再作为唯一或误导性 CI 证据。

### Phase 3 - 跨 Provider 功能矩阵和真实执行证据

**状态: `pending`**  
**依赖: Phase 2 的 runner 与至少一个安全 Provider 环境就绪**

#### RC27-P3-01 [P1] 扩展三个主 Provider 的最小 RC 矩阵

- 目标: 将“测试项目存在”提升为 Provider-specific 的真实 SQL/执行/资源证据，优先补齐 SQL Server 的明显缺口。
- 修改范围: 三个 Provider Integration 项目的 fixture、脚本、测试类；必要时对应 Provider Unit 和 SQLite shared contract。
- 步骤:
  1. 为 MySQL、PostgreSQL、SQL Server 分别建立同一能力表：1/2/5/10 source，2/5/10 Join，schema/quoted dotted table/alias，同步/异步/流式，2/5/7 mapping，multiple results，transaction，异常、预取消和资源释放，IN 上限/大集合。
  2. 先以现有 MySQL/PostgreSQL 较丰富用例为基线补缺，随后为 SQL Server 增加受控固定前缀表和最少的独立类，不把单个 `Select 1`、聚合或 CTE 当作完整覆盖。
  3. 所有 fixture collection 维持 `DisableParallelization=true`；每测试或类前 reset 仅清理固定测试表/schema，连接池在 fixture dispose 后清理。共享 DB 无法隔离时，Provider lane 必须串行。
  4. Provider 不支持的能力用明确的 capability/`NotSupportedException` 合同验证，不以 Skip 表示。Doris 保持只读单独范围，Oracle 按安全 schema/reset 契约另列，不阻断本任务的三个主 Provider。
  5. SQL Server 的 Startup/fixture 路径必须使用 SQL Server 专属数据源；跨 Provider route 用专门测试而不是无关默认注册。
- 测试: 每新增/更新 SQL 字符串断言完整文本；成功路径真实执行；异常/取消/Dispose 同时有 Unit 或 SQLite 基础路径兜底。
- 风险: 容器/共享库 reset 导致 flaky。先从 compose healthcheck、专用数据库和 collection 串行化排除时序问题，禁止 sleep。
- 验收: 每主 Provider 在当前 commit 产生独立非零 executed TRX；能力矩阵中的未支持项具有非 Skip 的明确证据；缺外部环境只能标记 blocked，不能写 completed。

#### RC27-P3-02 [P1] 完成公共 API 与 Runtime SPI 发布审计

- 目标: 在 CI/Provider 变动后确保不会恢复旧 API，也不会通过 friend assembly 扩大运行时耦合。
- 依赖: RC27-P1-03。
- 步骤:
  1. 运行 Data.Sql、Dapper Core 与 Provider 项目的 PublicApiAnalyzers，核实 Shipped/Unshipped 基线实际参与构建；新增/删除 public 成员必须进入正确 baseline 并有 compile contract。
  2. 保持删除 API 的 Roslyn/reflection negative contract，以及 `SqlJoinOptions`、terminal result、Raw `Query().From(...)` 的正向 consumer contract。
  3. 搜索生产程序集 `InternalsVisibleTo` 和 Runtime SPI 的实际消费者。测试/Benchmark IVT 可保留；生产 consumer 必须继续使用最小 public + `EditorBrowsable(Never)` contract，不能为重构临时增加 IVT。
  4. 只有 API baseline 或文档审计发现 drift 时修改 `PublicAPI.*`；本任务不设计新的 Breaking Change。若配置文件路径变更影响本地用户，按第 3.2 节提供迁移说明。
- 验收: Public API build 有可追溯成功记录，旧入口仍不可编译，生产 assemblies 无新增 IVT，Runtime SPI 未泄露 Builder/connection/transaction。

### Phase 4 - 职责拆分与测试组织收口

**状态: `pending`**  
**依赖: Phase 1 正确性和 Phase 3 Provider contract 已锁定**

#### RC27-P4-01 [P2] 基于证据拆分生产实现

- 目标: 降低查询、Builder 与多结果集生命周期的维护复杂度，不改变 public API、SQL、参数、异常、并发或资源所有权。
- 步骤:
  1. 记录候选文件行数、职责和引用方向。优先候选为 `SqlLambdaQuery`（Sources/Select/Where/Joins/Grouping/Terminals partial）、`SqlQueryBase`（Preparation/Terminals/Streaming/Transaction/Diagnostics partial）、`SqlBuilderBase`（渲染/clone/operation 协作者）和 `SqlMultipleQueryResult`（读取状态/completion/cleanup）。
  2. `SqlQueryOperationAccessor` 若仍与 query public facade 混放，可移入 `Queries/Internal` 或等价目录；仅移动 internal 文件和 namespace 内部实现，不创造新的 public abstraction 链。
  3. `JoinClause` 优先使用 private/internal 协作者拆解候选/提交/渲染逻辑；不新增 interface -> abstract -> provider -> strategy 的层级。
  4. 多结果集保留单一 reader lease 所有权和 `TryBeginDispose` 状态门。拆分后必须通过同一 direct lifecycle suite 与 SQLite result tests。
  5. 每次移动/partial 拆分后立即运行 Public API、受影响 Unit 和 SQLite；禁止夹带格式化或命名空间全仓迁移。
- 风险: partial 拆分复制私有状态、破坏访问修饰或导致 reader 双重释放。通过小批次提交前验证和 diff review 控制。
- 验收: 关键文件职责更集中，测试行为不变，未新增 public API/IVT/重复 helper。

#### RC27-P4-02 [P2] 拆分大型测试与 Benchmark 支撑代码

- 目标: 让失败定位到 Query/Join/Streaming/MultiMapping/Transaction/Diagnostics，而不是堆积在巨型测试类。
- 步骤:
  1. 将 `SqliteExecutionIntegrationTest` 按真实领域拆 partial 或独立文件，共享同一安全 fixture；不复制 seed/reset helper。
  2. 将 Provider 测试按 Query/Join/Streaming/MultiMapping/Transaction/Diagnostics 命名组织；跨 Provider 共性只在真正消除重复时抽取基础测试，不创建第二套 test infrastructure。
  3. 将 SQLite E2E benchmark base、FormalHost 类型、Dry smoke 类型和诊断 observer 分离，使 setup、订阅和待测方法边界可读。
- 验收: 测试方法名称继续采用 `Method_State_Expected`，中文测试目的和 AAA 保留；每个文件职责单一且工程编译无重复 source/include。

### Phase 5 - Benchmark 纯度、性能基线与数据驱动优化

**状态: `pending`**  
**依赖: RC27-P0-03；任何生产热路径优化前必须拥有有效 current baseline**

#### RC27-P5-01 [P1] 校正 Benchmark 场景边界

- 目标: 让 Benchmark 测量目标路径而非字符串构造、订阅 setup、数据库初始化或无关参数组合。
- 步骤:
  1. 保持 Root/Join `1/2/5/10` 语义；验证 `SourceCount=1` 不再错误标识为一个 Join。Raw 20/50 场景预构造 raw SQL，仅测 append/render；字符串/LINQ 生成另设 harness baseline，并用结构化循环 `.From(table, alias)` 比较。
  2. 保持 IN `0/1/10/100/500/1000/2100` 的 values-create、预构造 values bind/render、完整 build/render 分离；不把它与 root 数量交叉。
  3. 将 mutation benchmark 拆为一次/十次 mutation、cache hit/no-op、capturing lambda 与 `Mutate<TState> + static lambda` 候选对比；先确保 no-op/原子性 correctness，再测 delegate/closure 增量。
  4. 将 `DiagnosticListener` 常驻订阅移至 GlobalSetup，`QueryWithDiagnosticListener` 只测已订阅 query；新增独立 subscribe+query case。Activity/Trace/none 各自独立，Trace provider 不输出实际日志。
  5. 按 ToList/ToEntity/同步流/异步流/多映射/多结果集/诊断拆分 SQLite E2E 组。RowCount 是场景参数而非跨组性能结论；提前释放、取消、异常路径单独报告。
  6. Dry smoke 仅保留最小可执行性检查；FormalHost 才执行完整矩阵。MemoryDiagnoser 必须输出 Mean/Median、Allocated、Gen0/1/2；需要 P95 时采集真实样本或明确另行测量。
- 验收: 每个 Params 维度影响被测行为；CI Dry 不混入 FormalHost；报告不存在 NA/中断/Dry 与 formal 混合比较。

#### RC27-P5-02 [P1] 形成可复核 before/after 并仅优化已证实热点

- 目标: 以当前有效基线为 before，获取后续改动的真实性能和分配证据。
- 步骤:
  1. 在相同机器、SDK、Runtime、GC、Job、case key 下完整运行 current baseline：Root/Join、IN、Raw、mutation、no-op cache、SQLite E2E、diagnostics、streaming、2/5/7 mapping、multiple results。
  2. 先运行 correctness，再对单一候选优化跑 after。优先评估 `Mutate<TState> + static lambda` 和 changed/no-op internal 返回协议；不引入 Builder/Clause object pool，除非隔离、租户、并发、残留参数与异常路径都有直接测试且 profiler 显示池化必要。
  3. 将 before/after 的 source hash、命令、CSV/Markdown/HTML/raw log hash、环境和逐 case delta 写入基线报告。大于约 10% 且误差区间不能解释的 Mean 或 Allocated 回归必须修复、撤回或标记 blocked。
  4. 未提供完整 current FormalHost 前，不做任何“低 GC/无回归/性能提升”声明。即使某 case `Allocated=0`，也只能描述该 exact case。
- 验收: 所有保留优化都有同 key before/after、相关 correctness 和 allocation 证据；没有基于历史无效 artifact 的比较。

### Phase 6 - 文档、发布验证、Review 与交付

**状态: `pending`**  
**依赖: 前述适用 Phase 完成；外部阻断必须如实保留**

#### RC27-P6-01 [P1] 同步调用方、Provider 作者和维护者文档

- 目标: 文档只描述实际唯一配置/API 路径，并可以在无 secret checkout 中复现。
- 步骤:
  1. 更新 `docs/integration-testing.md` 和 `docs/testing/database-integration-tests.md`：规范变量、provider-specific connection、reset 开关、安全库名、common expected Skip 与 protected forbidden Skip、显式 local runsettings、独立 TRX、compose 本地启动方式和脱敏要求。
  2. 显著说明 PostgreSQL 用 `RUN_POSTGRESQL_INTEGRATION_TESTS`，不是 `RUN_PGSQL_INTEGRATION_TESTS`；不得保留不存在的 runsettings path。
  3. 更新 `README.md`/CI 文档，移除 .NET 2.2/Xenial 和无效 SDK 表述；只写 Phase 2 真实固定版本和 Linux provider。
  4. 更新 `docs/ReleaseNotes.md`、`ai_docs/sql-public-api-governance.md` 和 `ai_docs/sql-metadata-test-traceability.md`：保留 previous Breaking API 迁移说明、最终 Provider 能力/验证表、生产符号 -> 测试方法映射和 Benchmark 证据边界。
  5. XML 文档仅为本任务改动的 public API/contract 补 `summary`、`param`、`returns`、`typeparam`、关键 exception；interface/override 优先 `inheritdoc`。
- 验收: 文档命令/变量对应真实项目路径，所有示例无 secret 且可编译或可执行；没有把 blocked Provider/Benchmark 说成完成。

#### RC27-P6-02 [P0] 分阶段 Review、最终验证与报告

- 目标: 以真实运行数据给出 RC 结论，不掩盖外部环境、CI 或性能阻断。
- 步骤:
  1. 每个 Phase 结束执行 Inspect -> Reproduce -> Implement -> Build -> Unit -> SQLite/Provider Integration -> Review -> Fix MUST_FIX/SHOULD_FIX -> Regression -> Record 循环。分类失败为产品、测试、环境/Provider、安全拒绝、CI、flaky 或性能回退；禁止仅重跑到绿色。
  2. Review 结果使用 `MUST_FIX`、`SHOULD_FIX`、`COULD_FIX`。同阶段 MUST_FIX 必须修复并复验；SHOULD_FIX 在不扩大范围时修复；COULD_FIX 记录后续清单。`review.md` 由独立 reviewer 维护。
  3. 运行第 7 节的真实命令矩阵，执行 `git diff --check`，复核无 secret、无新增 production IVT、无误恢复 API。生成脱敏 TRX/coverage/PublicAPI/Benchmark artifact manifest。
  4. 编写最终 `verification-report.md`/`execution.md`：Task ID/HEAD/dirty、修改文件、Provider 根因和最终矩阵、Phase 状态、命令及 pass/fail/skip、三个 Provider TRX 摘要、blocked 原因、API 迁移、Benchmark before/after、Review 分类、发布风险以及未执行 Git/发布操作的声明。
- 验收: 只有满足第 8 节的完整条件才写 `completed`。缺 CI 权限、Provider secret/safe DB 或 formal benchmark 的情形必须为 `blocked`/`partial` 并给出精确解除路径。

## 7. 验证命令矩阵

以下命令均基于现有 solution/csproj/Benchmark 入口；Provider 命令仅在 runner preflight 通过、凭据由安全环境注入且数据库已验证安全时执行。执行器必须记录实际命令、SDK、TFM、退出码和测试统计。

```powershell
# 构建与无外部依赖回归
dotnet build .\Bing.All.sln -c Release -nologo -v minimal
dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net8.0 --no-restore --nologo
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 --no-restore --nologo
```

```powershell
# 当前已存在的 Benchmark smoke 入口；变更后须保持仅 Dry Job。
dotnet build .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore --nologo
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --ci-smoke --filter "*SqlCiSmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\ci"
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\ci-e2e"
```

```powershell
# Phase 2 新 runner 的预期调用形式。连接信息只能由受保护环境提供，不能写入脚本。
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider PostgreSql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider SqlServer -Framework net8.0 -Configuration Release
```

Provider runner 在正式 build 后使用对应现有项目：

- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Bing.Dapper.PostgreSql.Tests.Integration.csproj`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Bing.Dapper.SqlServer.Tests.Integration.csproj`

每个 Provider 还需按 Phase 2 矩阵运行 `net6.0` 或有记录的代表性 TFM。Final FormalHost 命令由 Phase 5 固化后写入 `benchmark-baseline.md`；它必须包含 `--filter` 和独立 `--artifacts`，不可用 Dry smoke 代替。

## 8. 最终验收与交付判定

### 必须满足

- [ ] 已完成凭据轮换/撤销协调，工作树、CI 配置、文档、TRX 和 Benchmark 制品均无 secret；历史处置已由维护者接受或作为独立风险记录。
- [ ] 三个 runsettings 不再隐式启用 Provider 或包含连接数据；clean checkout 行为可重现。
- [ ] `IntegrationTestGate` 的 OR/规范名/非法值/环境恢复、connection resolver、reset safety 有直接 Unit 覆盖。
- [ ] common lane 不运行外部 Provider；MySQL、PostgreSQL、SQL Server protected lane 各有独立 runner、专属 connection、reset 校验和 TRX。
- [ ] Provider lane 对 zero test、all skip、core gate skip 和 unsafe/default connection 都失败；MySQL cross-db optional skip 单独统计。
- [ ] 当前提交下三个主 Provider 各有至少一次真实、非零执行的受保护成功记录，且数据库只为安全测试库。
- [ ] Data.Sql、Analyzer、Dapper Core、SQLite Unit/Integration 和 Public API build 全绿，SQL 断言完整，未新增 production IVT。
- [ ] `global.json` 已跟踪且为经验证的 SDK；Windows 与现代 Linux/net8 各有代表性成功记录；Xenial/.NET 2.2 不再作为有效 CI。
- [ ] Provider 1/2/5/10 source、2/5/10 join、mapping、streaming、transaction、multiple results、异常/取消/Dispose 缺口均有 pass 或明确 `NotSupported`/blocked 证据。
- [ ] 当前源码 FormalHost baseline 与任何保留性能优化均有可匹配 before/after、环境和 artifact hash；无 NA/中断/Dry 混入正式比较。
- [ ] 文档、traceability、ReleaseNotes 和 Provider 能力表与实际门控/API/CI一致；未声称 0 GC 或未经证实的性能收益。
- [ ] `git diff --check` 通过；未执行 commit/push/PR/Tag/Release。

### 允许的阻断结论

下列条件缺失时，任务只能是 `partial` 或 `blocked`，不能标记完成：

- 维护者未提供或未授权受保护 CI job、secret 注入、专用测试数据库与 reset 许可；
- 无法轮换已暴露的凭据或无法确认其影响范围；
- 无可审计 current FormalHost artifact 或无法在固定环境运行；
- 无法确认/替换远程 legacy CI 的 required-check 策略。

每个阻断必须记录缺少的权限/变量类别/环境条件、已完成的本地证据和恢复后应运行的唯一命令，绝不以 Skip、历史报告或推测替代。

## 9. 执行顺序与下一步

依赖顺序为：

`RC27-P0-01 -> RC27-P0-02 -> RC27-P1-01 -> RC27-P1-02 -> RC27-P1-03 -> RC27-P2-01 -> RC27-P2-02 -> RC27-P2-03 -> RC27-P3-01 -> RC27-P3-02 -> RC27-P4-* / RC27-P5-* -> RC27-P6-*`

Phase 4 的职责拆分与 Phase 5 的基准治理可在 Phase 3 的核心 contract 稳定后并行，但最终报告必须等待二者的适用验证。执行完成计划后停止；下一严格角色入口为 `$execute-plan` 或 `/execute-plan`，不得在 Planner 阶段自动实施。