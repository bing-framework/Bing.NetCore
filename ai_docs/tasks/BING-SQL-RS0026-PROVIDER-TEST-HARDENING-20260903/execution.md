<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903
AI_EXECUTION_FINISHED_AT: 2026-09-04T10:02:38+08:00

# 实施执行报告

## 执行结论

原实施轮以 `PARTIAL` 终态结束；Round 2 已完成当前 `recommended` Review Fix 范围，仍不将缺少授权外部 Provider 真执行的总体发布结论表述为通过。

代码、测试、API 治理、Provider runner 安全边界、CI lane 配置、SQL Server 最小合同、共享证据模型和文档同步已完成。七个受影响生产项目的 post-fix Release build 均为 `0 warning / 0 error`；`Bing.Data.Sql` 与共享测试、SQLite 本地回归均通过。

发布级 Provider 真执行证据尚未闭环：当前环境没有经授权的 MySQL、PostgreSQL 或 SQL Server 专属安全测试数据库和连接变量，因此三个受保护 Provider lane 未执行。默认 gate TRX 仅证明测试在缺少授权配置时安全跳过，不是 Provider 真执行证明。SQLite 受控合同已在 Round 2 通过唯一目录生成两个 TFM 的当前 `TestGenerated` 制品；它们不属于 `ReleaseEvidence`，也不改变全 Provider 发布未就绪结论。

## 任务信息

- 任务：`BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903`
- 批准计划：`artifacts/plans/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903-plan.md`
- 计划入口：`ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/plan.md`
- 执行器：`codex`
- SDK：`8.0.424`
- source identity：`git=ea006172207927bae0ebc9da2ea9c634b4045e68`
- 开始时间：`2026-09-03T12:01:00.396Z`
- 结束时间：`2026-09-03T13:47:40.3577983Z`
- 原实施终态：`PARTIAL`；Round 2 `recommended` Review Fix 终态：`COMPLETED`

## 计划执行情况

| 计划范围 | 状态 | 说明 |
| --- | --- | --- |
| T01-T04：RS0026 清单、API 重载治理、PublicAPI 和统一 error gate | 已完成 | 修复显式 optional overload 家族，更新 PublicAPI/XML，并在 `common.props` 条件性提升 `RS0026`。 |
| T05-T06：runsettings、Provider gate、连接优先级和 SQL Server 启动隔离 | 已完成 | 移除提交配置中的真实连接信息和自动启用路径；保护 lane 拒绝全局 gate/default connection。 |
| T07-T09：MySQL、PostgreSQL、SQL Server 合同 | 部分完成 | MySQL/PostgreSQL 既有合同保持；SQL Server 增加参数、流式/取消、事务最小合同；本轮没有授权外部数据库，真实 lane 未执行。 |
| T10：Oracle/Doris 诚实状态 | 已完成 | 报告保持 `NotExecuted`，不生成虚假 `RealIntegrationProven`。 |
| T11-T13：runner、CI lane、共享证据模型 | 已完成 | runner 自检通过；证据状态和可信元数据边界已由共享测试覆盖。 |
| T14-T16：能力矩阵、最终报告、追踪和文档 | 部分完成 | 无密矩阵、报告和追踪已生成；矩阵保留外部 Provider `NotExecuted`，不满足发布放行。 |

## 已完成事项

- 处理 `Bing.Data.Sql` 查询、扩展和多映射 API 的 `RS0026/RS0027` optional overload 治理，保持同步/异步、取消、多映射和 Join 家族的显式入口。
- 同步 `PublicAPI.Shipped.txt`、`PublicAPI.Unshipped.txt`、XML 注释和 API contract tests。
- 在受影响 SQL/Dapper 项目启用条件性 `RS0026` error gate，未使用 `NoWarn` 绕过。
- 清理 MySQL、PostgreSQL、SQL Server 集成项目配置中的自动加载和真实连接风险，统一使用 Provider 专属 gate/连接变量与 reset 授权。
- 加固 `Invoke-ProviderIntegrationTests.ps1`：fail-fast preflight、安全数据库名校验、专属变量校验、唯一结果目录、TRX 计数校验、无密摘要和 self-test。
- 更新 AppVeyor common/protected lane 隔离，common lane 清除外部 Provider 变量，protected lane 使用同一 runner。
- 增加 SQL Server 受保护 lane 的启动连接隔离、固定合同表和最小参数/null、流式/预取消、事务提交/回滚测试。
- 扩展 `ProviderCapabilityEvidence`、`ProviderContractRunner` 和共享测试，阻止静态伪造 `RealIntegrationProven`，区分 `TestGenerated` 与 `ReleaseEvidence`。
- 修复 SQLite 左连接 DTO 派生表回归中的固定自增 ID 断言，使期望值从当前 seeded rows 的实际 ID 推导；根因是 fixture 使用 `DELETE` 后 SQLite `AUTOINCREMENT` 不重置。
- 补充 SQL Server 合同测试到 `ai_docs/sql-metadata-test-traceability.md`。

## 部分/未完成事项

- MySQL、PostgreSQL、SQL Server protected lane：未执行。原因是没有授权的专属安全数据库、连接变量和 reset 授权；不得用默认 gate 的安全 skip 冒充真执行。
- Oracle：未执行；没有授权 Oracle 安全 fixture/数据库，本轮只完成构建和状态记录。
- Doris：未执行；本任务没有可用的专属 Doris 合同 lane，保持外部执行状态为 `NotExecuted`。
- SQLite 受控合同测试：初始普通全量测试在缺少 `BING_SQLITE_CONTRACT_RESULTS_DIRECTORY`、`BING_SQLITE_CONTRACT_TRX_FILE_NAME`、`BING_SQLITE_CONTRACT_ARTIFACT_FILE_NAME` 时安全跳过；Round 2 已通过受控脚本分别生成 net8.0/net6.0 的 `1/1` `TestGenerated` Matrix，保持 `ReleaseReady=false`。
- 旧证据 `evidence/t01-release-build-output.txt`、`evidence/t03-sqltextquery-build.txt` 含修复前的 `RS0026/RS0016` 诊断，只能作为历史基线，不能作为 post-fix 通过证据。

## 修改文件

生产/API/配置/CI：

- `.gitignore`
- `appveyor.yml`
- `common.props`
- `docs/testing/database-integration-tests.md`
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Extensions/DialectExtensions.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IFrom.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.IJoin.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions/Extensions.ISqlBuilder.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlFluentQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Data.Sql/PublicAPI.Unshipped.txt`
- `ai_docs/sql-public-api-governance.md`
- `ai_docs/sql-metadata-test-traceability.md`

测试/fixture：

- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`
- `framework/tests/Bing.Dapper.MySql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Bing.Dapper.PostgreSql.Tests.Integration.csproj`
- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/DatabaseScript.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerStartupConnectionStringTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Startup.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/SqlQuery/SqlServerExecutionContractTest.cs`
- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/integration.runsettings`
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/TransactionApiContractTest.cs`
- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`

任务证据和报告：

- `ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/evidence/final/`
- `ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/execution.md`
- `artifacts/provider-test-results/final-rs0026-provider-hardening/`

## API/数据/配置变化

- 公共 API：optional parameter overload 改为显式 overload 家族；删除/保留的公共符号与 PublicAPI 基线同步，属于批准的主版本 Breaking Change 范围。
- 数据库：未执行生产数据库、未执行 destructive schema 操作；SQL Server 合同对象使用固定测试前缀并继续受 reset gate 保护。
- 配置：外部 Provider 仅接受专属 gate、专属连接变量和 `ALLOW_DATABASE_RESET_FOR_TESTS=true`；protected lane 不接受 `RUN_INTEGRATION_TESTS` 或 `ConnectionStrings__DefaultConnection` 回退。
- 证据：六态模型继续区分 `Declared`、`UnitProven`、`RealIntegrationProven`、`Unsupported`、`ImplementationGap`、`NotExecuted`。没有完整可信执行元数据时不升级为 `RealIntegrationProven`；`TestGenerated` 不等于 `ReleaseEvidence`。

## 测试结果

### 最终 post-fix 生产构建

七个受影响项目均 Release build 成功，最终日志位于任务 `evidence/final/`：

- `Bing.Data.Sql`：0 warning / 0 error
- `Bing.Dapper.Core`：0 warning / 0 error
- `Bing.Dapper.MySql`：0 warning / 0 error
- `Bing.Dapper.PostgreSql`：0 warning / 0 error
- `Bing.Dapper.SqlServer`：0 warning / 0 error
- `Bing.Dapper.Sqlite`：0 warning / 0 error
- `Bing.Dapper.Oracle`：0 warning / 0 error

### 测试与 TRX

| 范围 | Total | Passed | Failed | NotExecuted/Skip | 结论 |
| --- | ---: | ---: | ---: | ---: | --- |
| `Bing.Data.Sql.Tests` final rerun | 1278 | 1278 | 0 | 0 | 通过；net8.0/net6.0 均为 1278/1278。 |
| `Bing.Test.Shared` | 61 | 61 | 0 | 0 | 通过。 |
| SQLite integration net8.0 | 152 | 151 | 0 | 1 | 本地回归通过；1 个受控合同证据测试安全跳过。 |
| SQLite integration net6.0 | 152 | 151 | 0 | 1 | 本地回归通过；1 个受控合同证据测试安全跳过。 |
| SQLite 受控合同 net8.0（Round 2） | 1 | 1 | 0 | 0 | 通过；生成两个 `TestGenerated` Matrix entry，`ReleaseReady=false`。 |
| SQLite 受控合同 net6.0（Round 2） | 1 | 1 | 0 | 0 | 通过；生成两个 `TestGenerated` Matrix entry，`ReleaseReady=false`。 |
| SQLite 固定左连接单测 | 1 | 1 | 0 | 0 | 通过。 |
| MySQL default gate | 55 | 1 | 0 | 54 | 安全 skip；不是外部真执行。 |
| PostgreSQL default gate | 41 | 3 | 0 | 38 | 安全 skip；不是外部真执行。另有一份历史同类命名 TRX，均不升级证据状态。 |
| SQL Server default gate | 11 | 3 | 0 | 8 | 安全 skip；不是外部真执行。 |

TRX 路径、状态和无密矩阵见 `artifacts/provider-test-results/final-rs0026-provider-hardening/`；SQLite TFM TRX 分别为：

- `artifacts/provider-test-results/sqlite-integration-final-net8/sqlite-integration-final-net8.trx`
- `artifacts/provider-test-results/sqlite-integration-final-net6/sqlite-integration-final-net6.trx`

Round 2 SQLite 受控合同的当前绑定制品为：

- `artifacts/test-results/rs0026-review-fix-round2-net8/rs0026-review-fix-round2-net8.0.trx`
- `artifacts/test-results/rs0026-review-fix-round2-net8/rs0026-review-fix-round2-net8.0.json`
- `artifacts/test-results/rs0026-review-fix-round2-net6/rs0026-review-fix-round2-net6.0.trx`
- `artifacts/test-results/rs0026-review-fix-round2-net6/rs0026-review-fix-round2-net6.0.json`

### Runner 与受控边界

- `Invoke-ProviderIntegrationTests.ps1 -SelfTest`：通过。
- Provider runner self-test 结果：`Provider=PostgreSql; Framework=net8.0; Discovered=1; Passed=1; Failed=0; Skipped=0; OptionalSkipped=0; Executed=1`。
- 自检使用受控内存/模拟场景，不证明真实 PostgreSQL 连接能力。

## Build/Typecheck/Lint/Format

- Build：七个受影响生产项目 post-fix Release build 全部成功，0 warning/0 error。
- Typecheck：C# 编译由上述 `dotnet build` 覆盖。
- Lint/Analyzer：`RS0026` error gate 生效；最终受影响项目无 analyzer warning/error。
- Format：执行 `git diff --check`，未发现空白错误。
- Benchmark：本轮未修改映射缓存、格式化热路径、Builder clone 或延迟渲染实现；按计划 benchmark 对比不适用。

## 计划偏差

- 计划要求在授权环境对 MySQL、PostgreSQL、SQL Server 各 Provider lane 进行真实执行。本机没有授权安全数据库和专属连接变量，故按计划的诚实状态规则保留 `NotExecuted`，未尝试使用默认连接、全局 gate 或公网服务替代。
- 初始普通 SQLite net8.0/net6.0 回归未注入合同环境变量，故仅记录 skip；Round 2 已用受控脚本生成绑定当前 TRX 的 Matrix，未伪造或升级为发布证据。
- 由于 SQLite fixture 使用 `DELETE` 不重置自增序列，原测试的固定 ID 断言不稳定；已采取最小测试修复，改为使用当前 seeded rows 的实际 ID，不修改生产逻辑。

## 基线问题

- `evidence/t01-release-build-output.txt` 和 `evidence/t03-sqltextquery-build.txt` 保留修复前构建输出，其中的 RS0026/RS0027/RS0016 诊断是历史基线。
- `artifacts/provider-test-results/default-gate/` 中的 TRX 是默认禁用路径的安全跳过结果，不能推导任何外部 Provider 已通过。
- 工作树在本轮开始时已有大量与任务相关的未提交修改；本轮未 reset、clean、checkout 或覆盖无关改动。

## 已知问题

- 外部 Provider 的发布级真实执行证据缺失，当前不能给出全 Provider `ReleaseReady` 结论。
- SQLite 受控合同 net8.0/net6.0 已形成 `TestGenerated` 制品，但该证据不等于可信 `ReleaseEvidence`，也不使全 Provider 矩阵 `ReleaseReady`。
- SQL Server 新增合同覆盖的是最小合同，过程、批量、多结果等更宽能力仍需要在授权 lane 或明确 `Unsupported` 证据中继续补齐。
- Oracle/Doris 没有本轮外部执行制品；其未执行状态不能当作通过。

## 风险与回归关注点

- 发布前必须在受保护环境注入专属 Provider gate、专属安全连接、reset 授权和完整可信元数据，并分别生成每个 Provider/TFM 的 TRX 与 Matrix。
- 重新运行 SQLite 受控合同时必须使用新的结果目录和唯一文件名，且验证 TRX 计数、测试方法、源码身份和 Matrix 路径绑定。
- API Breaking Change 发布前需由 Reviewer 核查 PublicAPI、XML、迁移说明和外部消费者影响；本轮未宣称外部仓库消费者不存在。
- 继续关注 SQL Server fixture 的 reset 失败、事务资源释放、流式租约和连接归属；任何失败都不能通过静态证据条目掩盖。

## Reviewer 注意事项

- 请以 `artifacts/provider-test-results/final-rs0026-provider-hardening/provider-capability-matrix.md` 和 `.json` 为本轮矩阵入口；其中 `NotExecuted`、`UnitProven`、`RealIntegrationProven` 的边界按证据模型解释。
- 不要引用旧的 T01/T03 日志作为最终构建通过证据；应查看 `evidence/final/` 下的七个 post-fix build 日志。
- 默认 gate TRX 的少量 Passed 项来自门控/启动路径，不代表对应数据库执行过核心合同。
- 本轮没有自动执行 `git add`、`git commit`、`git push` 或创建 PR。

## Git 状态

- source identity：`git=ea006172207927bae0ebc9da2ea9c634b4045e68`
- 本轮保留未提交工作树状态；未执行 reset、clean、checkout、add、commit、push 或 PR 操作。
- 最终 diff 审计：`git diff --check` 通过；完整 `git status --short` 和 `git diff --stat` 保留在执行会话中，报告不覆盖其他用户修改。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/review.md`
- 本轮仅处理 `MUST_FIX`：`FIX-001`、`FIX-002`；`FIX-003`、`FIX-004`、`FIX-005` 为 `SHOULD_FIX`，按用户指定的 `must` 范围未处理。

#### FIX-001

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderCapabilityEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunner.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs`
	- `eng/Bing.ProviderEvidence.Cli/Bing.ProviderEvidence.Cli.csproj`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
	- `Bing.All.sln`
- 根因：原 `CreateReleaseEvidence` 接受任意字符串、时间和内部调用者，未独立复核 TRX/摘要/路径/运行身份；runner 只写摘要，未走共享矩阵链。
- 修复：
	- 移除宽松的 `CreateReleaseEvidence` 路径；新增 `ProviderReleaseEvidenceWriter.Write` 为唯一发布级写入入口。
	- 入口验证当前 `artifacts/provider-test-results/<run-id>/` 内的 TRX、摘要及固定矩阵路径，绑定 Provider、TFM、run id、source identity、UTC 时间窗口、TRX/摘要计数和无敏感字段约束。
	- 只有验证器登记的运行令牌才能创建 `ReleaseEvidence`；手工构造的同程序集令牌被拒绝，普通元数据构造仍保持 `TestGenerated`。
	- Provider runner 成功写入摘要后调用内部 CLI；CLI 通过唯一写入入口生成同目录 `provider-capability-matrix.json` 与 `.md`。失败、历史、越界、身份不符或核心跳过的制品不会生成发布矩阵。
- 验证：
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore --nologo`：`70/70 PASS`。
	- `dotnet test .\framework\tests\Bing.Test.Shared\Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore --nologo`：`70/70 PASS`。
	- 覆盖伪造路径、历史 TRX、source identity 不匹配、Provider/TFM 不匹配、failed、核心 skip、手工伪造运行令牌和成功可信写入。
	- `dotnet build .\eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj -c Release --no-restore --nologo -v minimal`：`0 warning / 0 error`。
	- `.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest`：`PASS`，成功自检生成绑定当前 run id 的 ReleaseEvidence 矩阵。

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
	- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
	- `eng/Bing.ProviderEvidence.Cli/Program.cs`
	- `artifacts/provider-test-results/final-rs0026-provider-hardening/provider-capability-matrix.json`
	- `artifacts/provider-test-results/final-rs0026-provider-hardening/provider-capability-matrix.md`
	- `ai_docs/sql-metadata-test-traceability.md`
- 根因：最终制品将 MySQL、PostgreSQL、SQL Server 各压缩为单一 Core 行，无法判断每项 T14 能力是未执行、实现缺口、原生不支持或已获真实证据。
- 修复：
	- 新增受控 `ProviderCapabilityCatalog`，将 `Provider + Capability + Scenario` 作为唯一键，覆盖 MySQL、PostgreSQL、SQL Server、SQLite、Oracle、Doris 的 query、parameter/null、type、stream/resource release、cancellation、DML/batch、transaction、procedure/output、multiple/returning/output。
	- 有现有测试代码但本轮没有授权运行的条目保留 `NotExecuted` 并记录精确测试方法；无 fixture/实现为 `ImplementationGap`；SQLite 过程输出为有依据的 `Unsupported`。
	- 当前最终基线由受限 CLI 从目录生成，包含 57 个唯一条目，`ReleaseReady=false`；没有将默认 gate skip 或本地基线提升为真实 Provider 执行。
	- 可信 protected lane 只有在当前 TRX 中找到对应通过方法时才提升匹配条目为 `RealIntegrationProven`，其余条目保持原状态。
	- 追溯文档增加本轮 RS0026 当前 error-gate 状态、显式重载 API 合同、发布证据链和 Provider 能力→测试方法映射；旧的“已知 warning”陈述明确为历史阶段说明。
- 验证：
	- `ProviderReleaseEvidenceValidatorTest.Catalog_WhenBaselineIsCreated_ShouldCoverEveryProviderCapabilityWithUniqueKeys`：PASS，验证完整能力词汇、57 个唯一键、六态合法性及非发布就绪状态。
	- `dotnet run --project .\eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj -c Release --no-build -- --workspace-root $PWD --baseline-output-directory artifacts/provider-test-results/final-rs0026-provider-hardening`：PASS，生成 `Entries=57; ReleaseReady=False`。
	- 最终 JSON、Markdown 与追溯文档均指向相同的测试方法和状态模型。

### Round 1 汇总

- MUST_FIX：2
- 已完成：`FIX-001`、`FIX-002`
- PARTIAL：无
- BLOCKED：无
- FAILED：无
- 未纳入范围：`FIX-003`、`FIX-004`、`FIX-005`（用户指定 `fixScope=must`）。
- 回归验证：共享测试 net8.0/net6.0 各 `70/70 PASS`；Provider runner self-test `PASS`；内部 CLI build `0 warning / 0 error`；`Bing.All.sln` Release build `91 warning / 0 error`；`git diff --check` `PASS`。
- 下一步：移交 `code-reviewer` 进行再次独立验收；本状态仅表示本轮 `MUST_FIX` 修复完成，不表示 `review.md` 已通过。

### Round 2

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/review.md`
- 本轮处理当前纳入范围的 `SHOULD_FIX`：`FIX-003`、`FIX-004`、`FIX-005`。Round 1 已解决的 `FIX-001`、`FIX-002` 未重写。

#### FIX-003

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `ai_docs/sql-metadata-test-traceability.md`
- 生成制品：
	- `artifacts/test-results/rs0026-review-fix-round2-net8/rs0026-review-fix-round2-net8.0.trx`
	- `artifacts/test-results/rs0026-review-fix-round2-net8/rs0026-review-fix-round2-net8.0.json`
	- `artifacts/test-results/rs0026-review-fix-round2-net6/rs0026-review-fix-round2-net6.0.trx`
	- `artifacts/test-results/rs0026-review-fix-round2-net6/rs0026-review-fix-round2-net6.0.json`
- 根因：上一轮仅执行普通 SQLite 回归，受控合同因未注入绑定目录、TRX 和 Matrix 文件名的环境变量而安全跳过，未形成可验证的当前运行制品。
- 修复：使用唯一目录通过 `Invoke-SqliteContractTests.ps1` 重新执行两个 TFM；追溯文档明确基线全 Provider 静态矩阵仍是非发布目录，而当前 SQLite 运行 Matrix 是独立的 `TestGenerated` 证据，不能提升为 `ReleaseEvidence` 或 `ReleaseReady`。
- 验证：
	- `Invoke-SqliteContractTests.ps1 -ResultsDirectory artifacts/test-results/rs0026-review-fix-round2-net8 -Framework net8.0 -Configuration Release -RunName rs0026-review-fix-round2`：`1/1 PASS`，TRX `total=1/passed=1/failed=0/notExecuted=0`，Matrix 两项 `RealIntegrationProven`、`ArtifactKind=TestGenerated`、`ReleaseReady=false`。
	- `Invoke-SqliteContractTests.ps1 -ResultsDirectory artifacts/test-results/rs0026-review-fix-round2-net6 -Framework net6.0 -Configuration Release -RunName rs0026-review-fix-round2`：`1/1 PASS`，TRX `total=1/passed=1/failed=0/notExecuted=0`，Matrix 两项 `RealIntegrationProven`、`ArtifactKind=TestGenerated`、`ReleaseReady=false`。
	- 两个 Matrix 均绑定各自 TRX/JSON 相对路径、相同的源标识 `1.0.0+ea006172207927bae0ebc9da2ea9c634b4045e68` 和当前 TRX 时间窗口。

#### FIX-004

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `docs/ReleaseNotes.md`
- 根因：已存在的 7.0.0 SQL API 收敛说明未明确 RS0026 将受影响 optional parameter metadata 拆分为短重载与完整重载后的消费者迁移边界。
- 修复：发行说明补充 `SqlTextQuery`、`SqlFluentQuery`、`SqlLambdaQuery` 和 Fluent 扩展的重载族说明：完整参数重载保留，源码调用按实参数量选择短重载；反射、动态调用和源生成器不再假定 `ParameterInfo.HasDefaultValue` 或默认参数元数据存在。
- 验证：
	- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-restore --nologo --filter "FullyQualifiedName~SqlQueryApiContractTest|FullyQualifiedName~TransactionApiContractTest"`：`26/26 PASS`。
	- `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -c Release --no-restore --nologo -v minimal`：`0 warning / 0 error`。
	- `PublicAPI.Unshipped.txt` 与 API 契约测试确认 Fluent `From`/`Join` 的 `2/3` 参数短/完整重载、Builder 的 `1/2/3` 参数重载及异步 `ToListAsync` 的无可选参数元数据。

#### FIX-005

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- 根因：runner 已读取 TRX 的 `failed` 计数，但 `Get-TrxSummary` 未在摘要层直接拒绝失败计数；self-test 也没有 failed TRX 回归场景。
- 修复：`Get-TrxSummary` 在任何成功摘要或发布矩阵写入前拒绝 `failed > 0`；self-test 构造 `total=1/passed=0/failed=1` 的 TRX，断言期望拒绝消息，并断言未出现成功摘要、JSON Matrix 或 Markdown Matrix。
- 验证：
	- `.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest`：`PASS`；保留已通过 TRX 的可信 `ReleaseEvidence` 正向链验证，同时 failed TRX 在 runner 层被直接拒绝。

### Round 2 汇总

- MUST_FIX：0
- SHOULD_FIX：3
- 已完成：`FIX-003`、`FIX-004`、`FIX-005`
- PARTIAL：无
- BLOCKED：无
- FAILED：无
- 回归验证：SQLite 受控合同 `net8.0 1/1 PASS`、`net6.0 1/1 PASS`；API 合同 `26/26 PASS`；`Bing.Data.Sql` Release build `0 warning / 0 error`；共享证据测试 net8.0/net6.0 各 `70/70 PASS`；Provider runner self-test `PASS`。
- 下一步：移交 `code-reviewer` 进行重新独立验收；本终态仅表示 Round 2 的 `recommended` 范围已处理，不表示 `review.md` 已通过。

### Round 3

- Review 状态：`NEEDS_FIX`
- Fix Scope：`must`
- Review 文件：`ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/review.md`
- 本轮处理 `MUST_FIX`：`FIX-006`。未修改 `review.md`。

#### FIX-006

- 严重程度：`BLOCKER`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 修改文件：
	- `framework/tests/Bing.Dapper.MySql.Tests.Integration/integration.runsettings`
	- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/integration.runsettings`
	- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/integration.runsettings`
	- `framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`
	- `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Bing.Dapper.PostgreSql.Tests.Integration.csproj`
	- `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Bing.Dapper.SqlServer.Tests.Integration.csproj`
	- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
	- `appveyor.yml`
	- `docs/testing/database-integration-tests.md`
- 根因：跟踪的 `integration.runsettings` 包含连接凭据、启用 gate 和 reset 授权；三个外部 Provider 项目还会在本机自动加载跟踪文件。
- 修复：将三个跟踪 settings 改为无密、默认关闭模板；项目改为仅在非 CI 且存在被 `.gitignore` 忽略的 `integration.runsettings.local` 时加载；Provider runner 和 AppVeyor common lane 显式传递 `CI=true`；同步文档说明本机与 CI 配置源。
- 验证：
	- 三个模板 XML 均可解析，`git grep` 未发现 `Password=`、`User Id=`、`Username=` 或启用的 gate/reset 值。
	- 三个项目在无 `.local` 文件时 `RunSettingsFilePath` 均为空；模拟 `CI=true` 与 `ContinuousIntegrationBuild=true` 时也均为空。
	- MySQL、PostgreSQL、SQL Server 集成项目 `net8.0 Release` build：均 `0 warning / 0 error`。
	- `git diff --check`：PASS。
	- 受控 runner self-test 未因本次配置变更执行；需由后续独立 Review 继续验收。

### Round 3 汇总

- MUST_FIX：1
- 已完成：`FIX-006`
- PARTIAL：无
- BLOCKED：无
- FAILED：无
- 回归验证：三个 Provider 集成测试项目 CI 条件 Release build 通过；跟踪模板无凭据且默认关闭；无本地 `.local` 文件时不自动加载。
- 下一步：移交 `code-reviewer` 进行再次独立验收；本终态仅表示本轮 `MUST_FIX` 已处理，不表示 `review.md` 已通过。
