<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260904-001
AI_REVIEWED_AT: 2026-09-04T14:31:09.6203937Z

# BING-SQL-RC-HARDENING-20260904-001 独立复审报告（Round 3）

## 1. 复审结论

**NEEDS_FIX / RC NOT READY**。

本轮优先复核上一轮 `FIX-003`、`FIX-004`、`FIX-005`。Review Fix 已显著补强 Provider ReleaseEvidence 的来源、哈希、session、archive matrix 和 SQLite producer 安全约束；共享 Evidence 单元测试双 TFM、CLI 构建、API 合同测试、runner self-test 和脚本语法检查均通过。

但完整发布工作流仍存在可由源码直接证明的正向链路阻断，不能仅归因于当前工作树 dirty 或缺少外部数据库凭据：SQLite ReleaseEvidence 未接入 aggregate、SQLite producer 与 loader 的受控根目录不一致、AppVeyor 未实际 materialize `release` lane、FormalHost 默认运行集合与 validator 的全 CSV `FormalHost` 约束冲突。因此 `FIX-003` 只能判为 `PARTIAL`，不能升级为 `RESOLVED`。

当前也不存在 clean source 下同一 session 的 8 个 Provider/TFM ReleaseEvidence、完整 FormalHost、RS0026/API gate artifact 和最终发布证据。禁止输出 `RC READY`。

## 2. 审查范围与证据

### 2.1 已核对

- 批准计划：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/plan.md`
- 执行记录：`ai_docs/tasks/BING-SQL-RC-HARDENING-20260904-001/execution.md`
- 上一轮审查：同任务目录原 `review.md`
- 当前 Git status、diff、diff stat 与 `git diff --check`
- Provider runner、SQLite runner、RC orchestrator、Evidence CLI、共享 Evidence/Readiness 实现与测试
- AppVeyor lane 配置、Benchmark 入口、FormalHost validator
- 当前 artifacts、最终报告、benchmark 报告、SQL 追溯和文档目录

### 2.2 本轮复跑

| 验证 | 结果 |
| --- | --- |
| `dotnet build eng/Bing.ProviderEvidence.Cli/Bing.ProviderEvidence.Cli.csproj -c Release --no-restore` | PASS，0 warning，0 error |
| `dotnet test framework/tests/Bing.Test.Shared/Bing.Test.Shared.csproj -c Release -f net8.0 --no-restore` | PASS，90/90 |
| `dotnet test framework/tests/Bing.Test.Shared/Bing.Test.Shared.csproj -c Release -f net6.0 --no-restore` | PASS，90/90 |
| `dotnet test framework/tests/Bing.Data.Sql.Analyzers.Tests/Bing.Data.Sql.Analyzers.Tests.csproj -c Release -f net8.0 --no-restore` | PASS，26/26 |
| SQLite Integration 项目 `net8.0` / `net6.0` build | PASS |
| `Invoke-ProviderIntegrationTests.ps1 -SelfTest` | PASS |
| Provider、SQLite、RC 三份 PowerShell 脚本 AST parse | PASS |
| aggregate fail-closed smoke（无受信输入） | PASS：`ReleaseReady=False`，`ProviderRunsValidated=0/8`，外部门禁均 false |
| `git diff --check` | PASS |

未复跑需要授权外部数据库的 MySQL、PostgreSQL、SQL Server clean ReleaseEvidence，也未伪造 FormalHost、RS0026 或 API gate artifact。

## 3. 上一轮 FIX 逐项复审

| FIX | 上一轮要求 | 本轮状态 | 结论 |
| --- | --- | --- | --- |
| `FIX-003` | 闭环同一 clean source/session 的 Provider、SQLite、FormalHost、RS0026、API 与 aggregate 发布链 | `PARTIAL` | ReleaseEvidence 安全基础已增强，但完整正向 workflow 仍不可达，继续 `MUST_FIX`。 |
| `FIX-004` | 移除 tracked 明文集成配置并防止敏感连接信息进入制品 | `RESOLVED` | tracked `integration.runsettings` 已删除；runner、报告与测试保持无密/fail-closed。 |
| `FIX-005` | 完成 inventory、追溯、API artifact 和 `docs/sql` 导航 | `NOT_RESOLVED` | 追溯文件有增量，但计划指定 inventory、API gate artifact 与 `docs/sql/**` 仍缺失。 |

## 4. 主要发现

### 4.1 HIGH：受保护 release workflow 的完整正向链路仍不可达

证据：

1. `eng/ci/Invoke-ReleaseCandidateValidation.ps1` 运行 SQLite producer 后，调用 aggregate 时只传 Provider、Unit、FormalHost、RS0026、API 目录，未传 CLI 已支持的 `--sqlite-results-directory`。
2. `eng/Bing.ProviderEvidence.Cli/Program.cs` 仅在显式收到 `--sqlite-results-directory` 时调用 `LoadSqliteRuns()`；否则 SQLite runs 为空，readiness 无法达到计划要求的 8 个唯一 Provider/TFM runs。
3. 即使补传当前 SQLite release 目录，producer 固定输出到 `artifacts/provider-test-results/sqlite-<tfm>-release`，而 SQLite loader 将受控根固定为 `artifacts/test-results`。当前 producer/consumer 路径契约互斥。
4. `appveyor.yml` 只有基于 `PROVIDER_TEST_LANE` 的 switch 分支，没有 `environment.matrix`、jobs 或其它仓库内配置把 `release` lane 实例化；变量为空时只执行 `common`。存在 case 分支不等于 release workflow 会被调度。
5. `framework/tests/Bing.Data.Sql.Benchmarks/Program.cs` 默认 `BenchmarkSwitcher.FromAssembly(...)`，RC orchestrator 未传 `--filter`；同一 assembly 同时包含多个 `FormalHost` 类型及 `DryJob` 类型。
6. `ValidateFormalHostEvidence()` 要求结果目录中的每个 CSV 都包含 `,FormalHost,`。默认全程序集运行一旦生成 DryJob CSV，validator 会拒绝整组结果；当前 runner 与 validator 的集合契约不一致。
7. 当前 aggregate smoke 正确 fail-closed 为 `0/8`，只能证明负向拒绝，不能证明受控正向 workflow 可完成。

影响：

- 即使提供 clean commit、数据库凭据和 reset 授权，现有 orchestrator 仍不能稳定产生计划要求的统一 8-run 发布矩阵。
- AppVeyor 仓库配置本身不保证受保护 release lane 会运行。
- FormalHost 完整执行可能因 DryJob 制品被确定性判失败。

### 4.2 MEDIUM：计划要求的发布治理交付物仍不完整

证据：

- 不存在 `artifacts/reports/rs0026-inventory.md`。
- 不存在 `api-gate.json` 实际制品；当前仅有 CI 内生成入口和 aggregate fail-closed 输入模型。
- 不存在 `formal-host-complete.json` 实际制品。
- 不存在 `docs/sql/**`；计划要求的 SQL landing、Provider capabilities、migration 与 testing 导航未落地。
- `ai_docs/sql-metadata-test-traceability.md` 已有本轮增量，但最终报告仍明确记录完整 runner/CLI/SQLite/MySQL Batch/PostgreSQL Function/SQL Server T12 映射待复核。
- `artifacts/benchmarks/benchmark-report.md` 仍为 `BLOCKED / NOT COMPARABLE`，仅有部分 FormalHost 日志，无完整 raw/CSV/Markdown/HTML 制品。

影响：

- T02、T19、T20、T21、T22 的计划验收仍不完整。
- 当前最终报告正确保持 `RC NOT READY`，但不能作为发布放行证据。

## 5. 已确认通过的边界

- ReleaseEvidence current/archive 验证已分离，archive matrix、TRX、manifest、hash、session/source 校验均有直接实现和测试。
- `TestGenerated` SQLite 证据不能被 archive 复制升级为 `ReleaseEvidence`。
- run-local 会复制测试、共享测试和生产 Provider DLL，并记录 SHA-256 manifest。
- Evidence CLI 和 readiness 对 source state、source identity、session、hash、能力覆盖及外部门禁保持 fail-closed。
- Provider runner self-test 验证环境变量隔离、恢复和安全数据库/reset 前置条件。
- 上一轮 `FIX-004` 的敏感配置整改未发现回归。

## 6. 结构化整改项

### FIX-003

- Severity：`HIGH`
- Priority：`MUST_FIX`
- Status：`OPEN / PARTIAL`
- Scope：发布工作流正向可达性与统一证据链

#### FIX-003 Required Changes

1. 让 RC orchestrator 将两个 SQLite ReleaseEvidence 目录显式传给 aggregate，且 CLI 能装载为 SQLite `net6.0` / `net8.0` 两个唯一 runs。
2. 统一 SQLite producer 与 loader 的受控根目录契约；保持 canonical path、越界、TestGenerated、session/source/hash 校验 fail-closed。
3. 在仓库 CI 配置中明确 materialize 受保护的 `release` workflow，并保留 secrets、clean source、专用测试库和 reset 授权门禁。
4. FormalHost 执行只选择声明 `FormalHost` job 的正式 benchmark 类型，或让 validator 按明确 allowlist 忽略 DryJob 制品；不能通过降低完整性要求放行部分结果。
5. 增加受控 workflow 测试，至少覆盖完整正向 8-run 装载以及缺 SQLite、路径越界、session/source/hash 不一致、DryJob 混入和未调度 release lane 的负向场景。
6. 在 immutable clean commit 上生成同一 session/source 的完整证据后，再执行 aggregate；没有真实证据时继续保持 `ReleaseReady=false`。

#### FIX-003 Acceptance

- 自动化测试可证明 producer 到 aggregate 的完整正向路径产生 4 Provider × 2 TFM 唯一 runs。
- AppVeyor 配置可由静态配置或受控测试证明 `release` lane 会被实际调度。
- FormalHost 目录只包含允许的正式 job，完整 validator 通过；任一部分、DryJob、错误 job 或缺失 CSV 均拒绝。
- 缺失、dirty、TestGenerated、placeholder、路径越界、session/source/hash 不一致继续 fail-closed。

### FIX-005

- Severity：`MEDIUM`
- Priority：`SHOULD_FIX`
- Status：`OPEN / NOT_RESOLVED`
- Scope：发布 inventory、最终追溯、API artifact 与 SQL 文档导航

#### FIX-005 Required Changes

1. 按计划生成 UTF-8 `artifacts/reports/rs0026-inventory.md`，列明项目级结果与例外为零的证据。
2. 完成最终生产符号到测试方法的职责级追溯，补齐 runner、CLI、SQLite、MySQL Batch、PostgreSQL Function、SQL Server T12 和聚合发布链。
3. 在受保护执行中生成并保留 `api-gate.json`，内容必须来自实际 PublicAPI/Breaking Change gate，而不是手工常量。
4. 建立 `docs/sql/` landing、Provider capabilities、migration 和 testing 导航，并与 Release Notes、集成测试说明交叉链接。
5. 在最终报告中引用本任务实际生成的 inventory、API、FormalHost、aggregate 与文档制品。

#### FIX-005 Acceptance

- 上述文件存在、UTF-8 可读、路径与内容符合 `plan.md`。
- gate artifact 可追溯到本次 source/session，且验证失败时不会生成通过状态。
- 文档链接检查通过，无断链或把历史制品当作本次发布证据。

## 7. 计划任务判定

| 任务 | 状态 | Reviewer 结论 |
| --- | --- | --- |
| T01 | PASS | 基线和边界已记录，最终结论保持 blocked。 |
| T02 | PARTIAL | Public API 测试通过，但 inventory、最终 baseline/API artifact 未闭环。 |
| T03-T18 | PARTIAL/PASS | 大量代码、Provider 合同与 fail-closed 测试已完成；发布级真实性仍依赖 `FIX-003`。 |
| T19 | BLOCKED | FormalHost 正式制品不完整，且默认运行集合与 validator 冲突。 |
| T20 | PARTIAL | Release Notes 和集成测试说明已更新，`docs/sql/**` 缺失。 |
| T21 | BLOCKED | 统一受保护序列源码存在正向链路阻断，当前仅验证负向 fail-closed。 |
| T22 | PARTIAL | 最终报告存在且结论正确，但发布治理制品和完整追溯未完成。 |

## 8. 最终判定

`FIX-004` 已解决；`FIX-003` 部分完成但仍是发布阻断；`FIX-005` 未解决。

在 `FIX-003` 的 producer/consumer/CI/FormalHost 正向链完成并以 clean 同源证据验证之前，本任务必须保持：

**NEEDS_FIX / RC NOT READY**。
