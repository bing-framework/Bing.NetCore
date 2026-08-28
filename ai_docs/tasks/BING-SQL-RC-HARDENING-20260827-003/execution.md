<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260827-003
AI_EXECUTION_FINISHED_AT: 2026-08-28T11:27:17.1837195+08:00

# 实施执行报告

## 执行结论

本任务初始实施在当前本地权限和环境内完成了 P0/P1/P2/P5 可实施项，状态为 `PARTIAL`。后续独立 Review 的 MUST_FIX 已在 Round 1 完成，最终仍需独立 Reviewer 再次验收。

- 基线提交：`3059a5971e0d3b52705a8d63eb077763e61d3a9d`
- 最终工作树 diff hash：`31c1b56b19028b38117dd5e617d6ccec72e163bc`
- SDK：`8.0.419`，由新跟踪的 `global.json` 固定。
- 未执行自动 `git add`、commit、push、PR、Tag 或发布。

## 任务信息

- Task ID：`BING-SQL-RC-HARDENING-20260827-003`
- 分支：`dev_v6.0-refactor-sqlquery`
- 执行器：Copilot plan executor
- 执行开始：`2026-08-28T01:28:59.190Z`
- 执行结束：`2026-08-28T01:59:33.5227816Z`

## 计划执行情况

| Phase | 状态 | 结果 |
| --- | --- | --- |
| Phase 0 | PARTIAL | 不改写用户配置，改以移除项目对用户 runsettings 的自动加载实现配置隔离；外部凭据轮换确认、历史处置和 FormalHost before 基线待维护者完成。 |
| Phase 1 | COMPLETED（本地） | gate/resolver 直接测试、用户 runsettings 自动加载隔离、Provider Startup 兼容及核心/SQLite 回归已验证。 |
| Phase 2 | PARTIAL | 新增 Provider runner、TRX 非零/Skip 门禁、common lane 环境隔离、SDK 与 Linux 配置现代化；远端 protected lane 及 Linux CI 未获权限执行。 |
| Phase 3 | BLOCKED | 没有获授权的 MySQL/PostgreSQL/SQL Server 专用测试数据库、专属 secret 和 reset 许可，未伪造真实 Provider 执行或扩展结论。 |
| Phase 4 | NOT APPLICABLE | 未发现需要为本任务正确性而进行的生产职责拆分；避免范围外重构。 |
| Phase 5 | PARTIAL | 修正 Raw/诊断订阅 benchmark 边界并通过 Dry smoke；当前提交的 FormalHost before/after 仍缺失。 |
| Phase 6 | PARTIAL | 集成测试文档已更新，最终验证已完成；发布说明、追溯矩阵和独立 review 不在本次本地证据范围。 |

## 已完成事项

- 移除三个 Provider 集成项目对用户 `integration.runsettings` 的自动引用；本地用户可显式通过 `--settings` 或 VS Code 测试设置使用其原有文件。
- 将 `.gitignore` 调整为跟踪 `global.json`，新增 .NET SDK `8.0.419` 固定配置，并保留 local runsettings 忽略规则。
- 保留用户现有 Provider 与 admin 配置文件原样，不读取、不回显、不置空其内容；任何既有凭据的轮换仍应由其所有者在外部系统完成。
- 扩充 `IntegrationTestGateTest`：global/provider OR、严格 `true` 解析、PGSQL 非规范变量拒绝、SQL Server 专属连接解析和环境恢复。
- 更新 resolver 和集成测试文档：Provider 专属连接优先，`DefaultConnection` 仅本地兼容，受保护 CI 禁止回退。
- PostgreSQL/SQL Server Startup 优先读取 Provider 专属连接，未配置时兼容回退 `DefaultConnection`；SQL Server 仅在显式全局本地兼容模式下额外注册 MySQL/PostgreSQL 具名数据源。
- 为三个 Provider 集成项目增加 `Microsoft.Bcl.AsyncInterfaces`，修复实际观察到的 `IAsyncDisposable` 编译缺失。
- 新增 `eng/ci/Invoke-ProviderIntegrationTests.ps1`：白名单 Provider/项目、专属 gate/连接/reset/安全库名预检、TRX 执行数检查、core Skip 拒绝和无密 JSON 摘要。
- AppVeyor common lane 显式清除外部 Provider 环境而非设置 `false`，并通过 `PROVIDER_TEST_LANE` 为 MySQL、PostgreSQL、SQL Server 提供各自的 runner 调用路径；Travis 本地配置升级为 focal/.NET 8 common lane。
- Benchmark 将 Raw 字符串构造与 render 拆开，并将诊断订阅移至 setup，新增独立订阅加查询场景。

## 部分/未完成事项

- 未完成三个主 Provider 的真实受保护执行和 P3 功能矩阵。解除条件：维护者在受信 CI 或本地安全环境仅注入对应 Provider 的专属连接、`RUN_*_INTEGRATION_TESTS=true`、`ALLOW_DATABASE_RESET_FOR_TESTS=true`，然后运行 `eng/ci/Invoke-ProviderIntegrationTests.ps1`；不得设置 `RUN_INTEGRATION_TESTS=true` 或 `ConnectionStrings__DefaultConnection`。
- 未创建或验证远端 AppVeyor protected Provider lanes，无法确认分支保护、secure variables、手动/定时触发和 artifact 留存策略。
- 未确认 Travis 是否仍为 required check，也未取得现代 Linux 作业的远端成功记录。
- 未执行 FormalHost current before/after；Dry smoke 不能用于性能比较。需要相同机器/SDK/runtime/GC/Job 和完全相同 case key 的独立 artifacts。
- 未获得凭据轮换、仓库历史处置或本机已生成 `bin` 历史开发配置清理的授权。未读取或报告其敏感值。
- 未进行独立 reviewer 的 `review.md` 结论，因此不能宣称 Review 闭环。

## 修改文件

主要修改：`.gitignore`、`global.json`、`appveyor.yml`、`.travis.yml`、`eng/ci/Invoke-ProviderIntegrationTests.ps1`、三个 Provider 集成项目配置/Startup、共享 gate/resolver 测试、两份集成测试文档，以及两个 Benchmark 类。用户现有 runsettings 与 admin 示例配置未修改。

## API/数据/配置变化

- 未新增或恢复 SQL public API，未新增 production `InternalsVisibleTo`。
- Provider CI 现在要求唯一的 Provider 专属连接变量；runner 明确拒绝全局 gate 和 `ConnectionStrings__DefaultConnection`。
- 默认 checkout 不再自动加载项目目录的用户 runsettings，因此不会由该文件隐式启用外部 Provider。
- Provider Startup/Resolver 优先使用专属连接键，并保留已测试的 `DefaultConnection` 本地兼容回退；受保护 CI runner 明确拒绝默认连接。

## 测试结果

| 命令/范围 | 结果 |
| --- | --- |
| `Bing.Test.Shared` net8.0 / net6.0 | 各 51 通过。 |
| `Bing.Data.Sql.Tests` net8.0 / net6.0 | 各 1261 通过。 |
| `Bing.Dapper.Core.Tests` net8.0 / net6.0 | 退出码 0。 |
| `Bing.Data.Sql.Analyzers.Tests` | 退出码 0。 |
| `Bing.Dapper.Sqlite.Tests.Integration` net8.0 / net6.0 | 各 151 通过；现有 `NETSDK1206` RID 警告仍存在。 |
| SQL Server 默认 gate | 3 通过，5 Skip；包含三数据源启动级兼容测试，未连接外部数据库。 |
| MySQL 默认 gate | 1 通过，54 Skip；唯一通过用例仅验证服务注册且使用无效连接，不建立数据库连接。 |
| PostgreSQL 默认 gate | 3 通过，38 Skip；包含两项连接优先级启动测试，未连接外部数据库。 |
| Provider runner `-SelfTest` | 通过，覆盖安全库名、严格 bool、数据库名解析、成功/zero-test/all-skip/core-skip TRX 拒绝、MySQL optional skip 和 global gate 拒绝。 |
| Provider runner `-ValidateOnly` | 使用无敏感的模拟 SQL Server 环境通过；global gate 与 default fallback 均被拒绝。 |
| Benchmark `--ci-smoke` / `--e2e-smoke` | 均通过 Dry job；最小迭代时间警告符合 Dry smoke 特性。 |

## Build/Typecheck/Lint/Format

- `dotnet build .\Bing.All.sln -c Release -nologo -v quiet -clp:ErrorsOnly`：成功，83 个既有警告，0 个错误。
- `git diff --check`：通过。
- 修改文件的编辑器诊断：无错误。

## 计划偏差

- 原计划要求由执行器创建 `progress.md`、`verification-report.md` 和 `benchmark-baseline.md`。本次仅维护 execution skill 强制要求的 `execution.md`，避免在用户未要求时新增重复 Markdown；所有可复核摘要写入本报告。
- 原计划要求实际 protected AppVeyor lane。仓库配置可安全加入 runner 与 common lane 隔离，但 remote secure variables、触发策略和 CI 执行权不在本地 workspace 权限内。
- P3 Provider 扩展未实施：没有安全真实数据库时新增大量未执行集成测试不能形成 RC 证据，故保留为 blocked。

## 基线问题

- 当前 Benchmark 仅有 Dry smoke。前序 benchmark artifact 没有本提交可复核的源码/环境身份，不能作为 current before 或 after。
- MySQL/PostgreSQL 默认项目各包含一个纯服务注册测试；它们不属于外部 Provider 执行，也不应作为 protected Provider 通过证据。

## 已知问题

- 全量构建仍有 83 个仓库既有警告，包括 target framework 支持、nullable 和 SQLite RID 警告；本任务没有扩大范围处理无关警告。
- 受保护 Provider lane 尚无真实 TRX artifact，不能宣称 MySQL/PostgreSQL/SQL Server RC 通过。

## 风险与回归关注点

- 维护者必须尽快轮换此前已暴露的 Provider 与 admin 配置凭据，并评估仓库历史暴露范围；不能通过本地配置删除视为轮换完成。
- 执行真实 Provider lane 前必须使用专用安全测试库，并通过 runner 预检；不得设置 `RUN_INTEGRATION_TESTS=true`、不得使用 default connection。
- `appveyor.yml` 已提供三个显式 runner 调用路径；protected lane 的远程 secret、触发和分支保护策略仍必须由具备 CI 权限的维护者配置并验证。

## Reviewer 注意事项

- 检查 runner 的 TRX allow-list 是否与未来 adapter 的 skip 身份保持一致，尤其是 MySQL cross-database 和 SQL Server multi-provider local compatibility 测试。
- 检查 MySQL `MySqlConnection` 配置键的运行环境是否均通过专属环境变量注入；本地 fallback 只应保持 resolver 兼容，不能进入 protected CI。
- 对明文扫描只审计路径和类型，不将匹配值写入评论、TRX、日志或任务文档。

## Git 状态

工作树包含本任务所列的配置、测试、CI、Benchmark 和文档改动；`git diff --check` 通过。未自动执行 `git add`、`git commit`、`git push` 或创建 PR。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：must
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260827-003/review.md`
- 用户约束：不改写、置空、删除或回显三份 Provider `integration.runsettings`、三份 Provider `appsettings.json` 和两份 admin `appsettings.json`。

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`
  - `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Bing.Dapper.PostgreSql.Tests.Integration.csproj`
  - `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Bing.Dapper.SqlServer.Tests.Integration.csproj`
  - `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Startup.cs`
  - `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Startup.cs`
  - `framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Infrastructure/PostgreSqlStartupConnectionStringTest.cs`
  - `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerStartupConnectionStringTest.cs`
  - `docs/integration-testing.md`
  - `docs/testing/database-integration-tests.md`
- 根因：三个项目通过 `RunSettingsFilePath` 自动加载用户 runsettings，且 PostgreSQL/SQL Server Startup 没有保留 `DefaultConnection` 本地兼容回退。
- 修复：删除三个项目的自动 runsettings 引用，不触碰原文件；Provider 专属连接优先、`DefaultConnection` 仅作为本地回退；文档明确仅显式 `--settings` 或 VS Code 选择才会加载本地 runsettings。
- 验证：
  - 三个 Provider csproj 搜索 `RunSettingsFilePath`：无匹配。
  - MySQL net8 默认门控：1 passed、54 skipped；未连接外部数据库。
  - PostgreSQL net8/net6 默认门控：各 3 passed、38 skipped；未连接外部数据库。
  - SQL Server net8/net6 默认门控：各 3 passed、5 skipped；未连接外部数据库。
  - PostgreSQL/SQL Server 启动配置测试：验证专属连接优先和 `DefaultConnection` 回退。
  - 八个受保护配置文件：`git diff --quiet -- <paths>` 通过，未修改。

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Startup.cs`
  - `framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Infrastructure/SqlServerStartupConnectionStringTest.cs`
  - `eng/ci/Invoke-ProviderIntegrationTests.ps1`
  - `appveyor.yml`
  - `docs/integration-testing.md`
  - `docs/testing/database-integration-tests.md`
- 根因：SQL Server 的全局多 Provider 测试仍依赖三个具名数据源，但 Startup 已被收窄；runner 对 global gate 和 TRX 边界的直接自检不足，CI 未暴露三个 runner 调用路径。
- 修复：仅在显式 `RUN_INTEGRATION_TESTS=true` 本地兼容模式注册 MySQL/PostgreSQL Provider 和 `mysql`、`pgsql`、`sqlserver` 数据源；新增纯 DI 启动级测试。runner 优先拒绝 global gate，并覆盖 zero-test、all-skip、core-skip、optional skip、global-gate 合同。AppVeyor 以 `PROVIDER_TEST_LANE=mysql|postgresql|sqlserver` 路由到三个明确的 runner 调用。
- 验证：
  - `Invoke-ProviderIntegrationTests.ps1 -SelfTest`：PASS。
  - SQL Server net8/net6：各 3 passed、5 skipped；启动级测试解析三个具名数据源且不建立数据库连接。
  - `-ValidateOnly`：专属 gate/reset/安全占位连接通过；global gate 和 default connection 均按预期拒绝；环境变量已恢复。
  - `appveyor.yml`：存在 MySQL、PostgreSQL、SQL Server 三个可定位 runner 调用路径。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：DEFERRED
- 原因：用户明确限定 `fixScope=must`；真实 Provider 环境、FormalHost 基线和远端 CI 证据仍需要维护者授权与受保护环境。

#### FIX-004

- 严重程度：LOW
- 处理要求：OPTIONAL
- 执行状态：SKIPPED
- 原因：用户明确限定 `fixScope=must`。

### Round 1 汇总

- MUST_FIX：2
- 已完成：2（FIX-001、FIX-002）
- PARTIAL：0
- BLOCKED：0
- FAILED：0
- 回归验证：全量 Release build 成功（83 warnings、0 errors）；`Bing.Data.Sql.Tests` net6 1261 passed；`Bing.Dapper.Core.Tests` net6 134 passed；SQLite integration net6 151 passed；`git diff --check` 通过；编辑器诊断无错误。
- 配置保护：八个用户指定配置文件保持未修改，未读取或输出其内容。
- 下一步：重新进行独立 Review；本轮 `COMPLETED` 仅表示 MUST_FIX 已完成，不代表 `review.md` 已通过。

### Round 2

- Review 状态：NEEDS_FIX
- Fix Scope：must
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260827-003/review.md`
- 纳入范围的 FIX：无。
- 结论：当前 review.md 仅包含 `FIX-001`（SHOULD_FIX）和 `FIX-002`（OPTIONAL），不存在 MUST_FIX；根据用户限定范围未修改代码、测试、配置或 `review.md`。
- 验证：`git diff --check` 通过；已确认 review.md 的前三行机器元数据仍为 `NEEDS_FIX` 和当前任务标识。
- 下一步：如需继续处理，应以 `fixScope=recommended` 处理现有 SHOULD_FIX，或由维护者提供受保护 Provider/CI/Benchmark 证据。

### Round 3

- Review 状态：NEEDS_FIX
- Fix Scope：must
- Review 文件：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260827-003/review.md`
- 纳入范围的 FIX：无。
- 结论：当前 review.md 的开放项为 `FIX-001`（SHOULD_FIX）和 `FIX-002`（OPTIONAL），不存在 MUST_FIX；根据用户限定范围未修改代码、测试、配置或 review.md。
- 验证：完整读取当前 plan.md、execution.md、review.md 和 Git Diff；`git diff --check` 通过；review.md 的前三行仍为 `NEEDS_FIX` 及当前任务标识。
- 下一步：重新独立 Review；如需处理现有 SHOULD_FIX，必须由维护者提供受保护 Provider/CI/Benchmark 的无密可审计证据，或显式改用 `fixScope=recommended`。

### Round 3 汇总

- MUST_FIX：0
- 已完成：0
- PARTIAL：0
- BLOCKED：0
- FAILED：0
- 回归验证：`git diff --check` 通过；未运行无关构建、测试或外部数据库操作。
- 配置保护：未读取、修改、清空、删除或输出任何受保护用户配置内容。
- 下一步：重新进行独立 Review；本轮 `COMPLETED` 仅表示 MUST_FIX 范围为空且已按用户要求完成处理，不代表 `review.md` 已通过。
