<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260906-001
AI_EXECUTION_FINISHED_AT: 2026-09-07T07:44:52.7606025Z

# 实施执行报告

## 执行结论

当前状态：`PARTIAL`，RC 结论为 `RC NOT READY`。本轮完成了 PostgreSQL Capability 语义修正、Transaction 公共 API 收敛、RS0016/17/18/26 公共门禁、Provider Settings 本地导入、RC Unit 清单、AppVeyor lane matrix、当前 Task 的 Unit/Integration/Capability/Benchmark 报告和文档导航。SQLite、PostgreSQL、SQL Server 的本地/专用数据库合同真实执行通过，但工作树 dirty，Runner 按设计拒绝升级为 ReleaseEvidence。MySQL net6/net8 在连接认证阶段被本机 SSL/RSA 配置阻塞；批准的 136 个 FormalHost 组合已全部完成，但 clean-source 和受保护 CI Gate 仍未满足。未执行 git add、commit、push、merge、PR、reset、clean。

## 任务信息

- Task ID：`BING-SQL-RC-HARDENING-20260906-001`
- 执行器：Codex
- HEAD：`405e67b7df381678203fb07295f898d7cf9dd698`
- SDK：`8.0.424`
- Runtime：`.NET 8.0.30`
- OS：Windows 10.0.19045
- 执行时间：2026-09-07（Asia/Shanghai）
- 状态注册：`task-state.mjs start` 首次受 `.agents/runtime` ACL 拒绝，使用必要权限重试后成功；execution state 已注册。

## 计划执行情况

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| T01-T02 | COMPLETED | 顺序 Release build 七项目全部 exit 0；RS0016/17/18/26 均 0；inventory 位于 `artifacts/reports/BING-SQL-RC-HARDENING-20260906-001/rs0026/`。 |
| T03-T04 | COMPLETED/PARTIAL | PostgreSQL Profile、Catalog、runtime gate 和 direct/integration tests 已一致；六 Provider 静态 baseline 64 项已生成；Oracle/Doris 外部 fixture 仍非阻塞。 |
| T05-T06 | COMPLETED | Transaction explicit overload、PublicAPI shipped baseline、反射合同和公共 Analyzer gate 已更新；Data.Sql net6/net8 1283/1283。 |
| T07-T08 | COMPLETED | `.local` 显式导入和目标变量过滤已实现；runner self-test、ProviderGateDisabled/ConnectionMissing 失败探针通过，失败路径生成脱敏 `provider-startup-diagnostic.json` 并保持非零退出。 |
| T09-T10 | COMPLETED/PARTIAL | AppVeyor matrix 已显式列出 common/mysql/postgresql/sqlserver/release/aggregate；RC Unit 清单已覆盖 11 个 Unit 工程，Analyzer 单独 net8；FormalHost runner 现在从实际 CSV 验证九个批准类型、Job/3/6/15 和组合计数，`-SelfTest` 正例/错误 Job 负例均通过；真实 protected positive workflow 尚未在 AppVeyor 执行。 |
| T11 | COMPLETED | 新增 PostgreSQL Profile、Procedure fail-fast、Transaction API、Catalog direct tests；更新生产符号到测试方法追溯。 |
| T12 | PARTIAL | SQLite net6/net8 1/1；PostgreSQL net6/net8 44/44；SQL Server net6/net8 21 executed passed、1 optional skip；MySQL 两个 TFM 均在认证阶段失败。 |
| T13 | PARTIAL | SQL Server 参数边界和资源合同已由既有真实合同执行；本轮未新增执行中 cancellation 场景。 |
| T14 | PARTIAL | 当前 Task Unit/Integration/Capability 报告已生成；dirty source/TestGenerated 仍不能形成 ReleaseEvidence。 |
| T15 | COMPLETED/PARTIAL | 四个并行 FormalHost 分组共 136/136 组合以 3/6/15 完成，生成完整 CSV/Markdown/HTML/log/hash manifest；dirty source 仍不具备发布资格。 |
| T16 | COMPLETED | 新增 `docs/sql` 入口、Provider 能力、PostgreSQL Function、迁移说明，并更新集成测试/RunSettings 文档。 |
| T17 | BLOCKED | 最终 RC Gate 不能通过：MySQL 外部认证、source dirty 和受保护 AppVeyor positive workflow 仍未满足；因此没有输出 RC READY。 |

## 已完成事项

### PostgreSQL Capability

- `PostgreSqlSqlProvider.Profile` 现在声明 `SupportsStoredProcedures=false`、`SupportsOutputParameters=false`，失败原因均为 `DatabaseUnsupported`。
- Function 结果集保持文本 SQL 合同；Procedure 集成测试验证框架在执行前 fail-fast。
- PostgreSQL net6/net8 真实 lane 各发现并通过 44 个测试；第一次错误调用确认 Npgsql 6.0.11 对当前 `CommandType.StoredProcedure` 路径返回 `42809 ... is a procedure`，未将其包装成成功。
- Catalog 的 PostgreSQL output parameter scenario 从 `ImplementationGap` 改为 `Unsupported`，避免把数据库天然语义差异误报为框架实现缺口。

### API / Analyzer

- Transaction factory 从可选 `dbKey` 族收敛为显式无键、`dataSourceKey`、隔离级别和 CancellationToken overload。
- `PublicAPI.Shipped.txt`（Bing.Data.Sql、Bing.Dapper.Core）和 direct reflection test 已同步。
- `common.props` 对七个 SQL/Dapper 生产项目统一加入 RS0016/17/18/26 WarningsAsErrors；七项目 Release build 全部 0 error、0 目标 API diagnostic。
- `ToEntity` 保留为唯一高层实体终结入口；现有 API contract 明确禁止再引入同义 `SingleOrDefault` 查询描述入口。

### CI / Settings / Evidence

- Provider runner 新增 `-Settings`（Alias）本地导入，要求仓库内 UTF-8 `integration.runsettings.local`，只加载目标 Provider gate/connection/reset 和 MySQL cross-database 选项。
- 修复 SQLite、Provider、RC 脚本中 PowerShell native git exit code 检查，避免 `$LASTEXITCODE` 为空误判 HEAD 无法解析。
- AppVeyor environment matrix 显式列出 common、mysql、postgresql、sqlserver、release、aggregate；Provider secrets 仍必须由受保护环境注入。
- RC Unit 清单覆盖 Bing.Core、Dapper Core、MySQL/PostgreSQL/SQL Server/SQLite/Oracle、Data.Sql、CustomProvider、Analyzer、Test.Shared；Analyzer 工程只跑 net8.0。

### Reports / Docs

- `unit-test-report.md`、`integration-test-report.md`、当前 64 项 `provider-capability-matrix.md/json`、`benchmark-report.md` 已生成。
- 新增 `docs/sql/README.md`、`provider-capabilities.md`、`postgresql-functions.md`、`migration-guide.md`。
- `ai_docs/sql-metadata-test-traceability.md` 已追加本 Task 生产符号→直接测试→当前证据映射。

## 部分/未完成事项

1. MySQL net6/net8 连接在本机 MySQL 服务器认证阶段失败：先出现 SSL credential error，临时 `SslMode=None` 后出现 `caching_sha2_password` RSA public-key requirement。没有修改用户的 ignored Settings，也没有将此失败伪装为 Skip/Pass。需要受保护 CI 的正确 TLS/RSA 连接策略或专用账号配置。
2. 所有本地 Provider 运行 source state 为 dirty；Provider validator 正确拒绝 ReleaseEvidence。需要用户提交当前变更后在 protected CI 重新运行同一 session/source。
3. FormalHost 全量已完成：Aggregate 27、SQLite 24、Lambda 40、Metadata/Mutation/Debug 45，共 136/136；报告和 SHA-256 manifest 位于 `artifacts/benchmarks/BING-SQL-RC-HARDENING-20260906-001/formal-full/`。
4. AppVeyor matrix 已落盘，但真实平台的 secret scope、job artifact fan-in 和 release/aggregate positive path 未执行。
5. Provider runner 失败诊断已补齐；数据库执行中发生的认证/网络失败仍需在真实受保护环境中按 Provider 运行后分类为 `Unreachable` 或测试失败。

## 修改文件

### 新增

- `ai_docs/tasks/BING-SQL-RC-HARDENING-20260906-001/execution.md`
- `docs/sql/README.md`
- `docs/sql/provider-capabilities.md`
- `docs/sql/postgresql-functions.md`
- `docs/sql/migration-guide.md`
- 当前 Task 的 Unit/Integration/Capability/Benchmark 报告和 TRX/JSON/log 制品。

### 修改

- `appveyor.yml`、`common.props`
- `eng/ci/Invoke-ProviderIntegrationTests.ps1`
- `eng/ci/Invoke-ReleaseCandidateValidation.ps1`
- `eng/ci/Invoke-SqliteContractTests.ps1`
- `eng/Bing.ProviderEvidence.Cli/Program.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlTransactionScopeFactory.cs`
- `framework/src/Bing.Data.Sql/PublicAPI.Shipped.txt`
- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlTransactionScopeFactory.cs`
- `framework/src/Bing.Dapper.Core/PublicAPI.Shipped.txt`
- `framework/src/Bing.Dapper.PostgreSql/Bing/Data/Sql/Builders/PostgreSqlSqlProvider.cs`
- PostgreSQL integration/unit tests and DatabaseScript
- `framework/tests/Bing.Data.Sql.Tests/TransactionApiContractTest.cs`
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidence.cs`
- `framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs`
- `docs/testing/database-integration-tests.md`
- `ai_docs/sql-metadata-test-traceability.md`
- `artifacts/benchmarks/BING-SQL-RC-HARDENING-20260906-001/formal-debug/formal-host-subset.json`
- `artifacts/benchmarks/BING-SQL-RC-HARDENING-20260906-001/formal-full/formal-host-complete.json`

### 删除 / 重命名

- 无。

## API/数据/配置变化

- Transaction public API 有 Breaking Change：去除可选 `dbKey` 入口，改用显式 `dataSourceKey` overload；迁移说明见 `docs/sql/migration-guide.md`。
- PostgreSQL Capability 声明变更为不支持当前统一 Procedure/OUT 参数合同；Function Result Set 使用方式见 `docs/sql/postgresql-functions.md`。
- 未修改数据库 schema；本地 Settings 只读并过滤导入，未写入或提交密码。

## 测试结果

- 七个生产项目：Release build 全部 PASS；RS0016/17/18/26 全部 0。
- Unit：11 个工程适用 TFM 全部最终 PASS；关键计数包括 Data.Sql 1283/1283、Dapper Core 161/161、PostgreSQL 136/136、Shared 90/90。
- SQLite：net6/net8 各 1/1，TestGenerated。
- PostgreSQL：net6/net8 各 44/44，dirty source，NOT VERIFIED。
- SQL Server：net6/net8 各 21 executed passed、0 failed、1 optional skip，dirty source，NOT VERIFIED。
- MySQL：net6/net8 各 59 discovered、0 executed、59 connection/auth failures，BLOCKED。
- Provider runner self-test、Settings ValidateOnly、三个 PowerShell AST parse：PASS；ProviderGateDisabled 与 ConnectionMissing 失败探针均 exit 1 且诊断无秘密；RC FormalHost manifest self-test 正例/负例通过且无残留目录。
- Benchmark smoke：CI 1 case、SQLite E2E 24 cases PASS；FormalHost 全量 136/136 组合完成 3/6/15，完整清单见 `artifacts/benchmarks/BING-SQL-RC-HARDENING-20260906-001/formal-full/formal-host-complete.json`。

## Build/Typecheck/Lint/Format

- `dotnet build .\Bing.All.sln -c Release --no-restore --nologo`：PASS，仓库既有 warnings；并发单项目构建曾产生共享 obj 文件锁，随后顺序构建验证通过。
- 七项目独立 Release build：PASS，目标 API diagnostic 0。
- `git diff --check`：PASS（仅提示若 Git 重新写入时 CRLF 将转 LF）。
- PowerShell AST parse：PASS。

## 计划偏差

- 计划中推荐将 `ToEntity` 改名为 `SingleOrDefault`；真实当前 API contract 已明确冻结 `ToEntity` 且禁止同义 `SingleOrDefault`，因此保留现状并在迁移文档记录最终决策，避免新增第二套 API。
- 计划建议 Procedure 成功合同；真实 PostgreSQL/Npgsql 运行证明当前框架命令路径不支持 procedure，按 fail-closed 原则改为 Unsupported + direct fail-fast test。
- 本地 MySQL 认证缺少外部 RSA/TLS 条件，未修改 ignored Settings 或放宽安全规则。

## 基线问题

- 当前工作树包含用户既有 `.agents/.codex/docs` 修改和本轮变更，不能生成发布级 clean evidence。
- AppVeyor matrix 需要真实平台 secret scope 与 artifact 传递配置，仓库内无法替代受保护外部状态。

## 已知问题

- FormalHost 正式制品已完整生成；无同 key before/after baseline，未计算性能 Delta，且 dirty source 使其不能单独升级为 ReleaseEvidence。
- MySQL 受本机 server auth policy 阻塞，需受保护 CI 或调整专用测试账号/公钥策略。
- Oracle/Doris 仍保持 non-blocking；没有安全 fixture 时不得声明真实集成通过。

## 风险与回归关注点

- Transaction API Breaking Change 需要下游调用方按迁移指南更新。
- PostgreSQL 用户若需要真正 `CALL` procedure，当前 `Procedure()` 入口会 fail-fast；必须使用明确的 Provider 原生方案，不得吞掉异常。
- 外部 Provider fixture 会 reset 专用测试库；继续要求安全数据库名和显式 reset 授权。

## Reviewer 注意事项

- 不要把 `TestGenerated`、dirty source、DryJob 或 `Executed=0` 解读为 ReleaseEvidence/RC READY。
- 当前正式结论必须是 `RC NOT READY / PARTIAL`。
- 未自动 git add、commit、push、merge 或创建 PR。

## Git 状态

- HEAD：`405e67b7df381678203fb07295f898d7cf9dd698`
- 工作树：dirty，含用户既有 Agent workflow 改动、本 Task 代码/测试/文档/报告。
- 未执行自动 Git 发布操作。

## Continuation Round

本轮继续审计并完成以下可在当前工作树安全执行的事项：

- 修复 `Invoke-ReleaseCandidateValidation.ps1` 的 `Invoke-Captured` 原生命令退出码捕获，成功/失败合成命令分别验证为 0/1。
- RC RS0026 inventory 扩展为同时记录和校验 RS0016、RS0017、RS0018、RS0026，并新增 `rs0026-inventory.md`。
- Provider runner 增加无密 startup diagnostic；PostgreSQL Settings ValidateOnly 输出 ConfigurationSource、gate、数据库名、reset、reachability/execution 状态；preflight 失败写入脱敏 `BlockedReason` JSON 并保持 fail-closed。
- 重新执行完整解决方案 Release build：0 error；Provider runner self-test、三个 PowerShell AST parse、git diff check 通过。

未改变核心遗留：MySQL 本机认证条件、clean-source ReleaseEvidence 和受保护 AppVeyor positive workflow 仍需外部环境；FormalHost 全量证据已在本轮补齐。

本轮先发现并修复 RC runner 的无效 `|` 过滤器和 PowerShell 数组展开问题，然后将批准集合拆为四个独立组并行执行：Aggregate 27/27、SQLite 24/24、Lambda 40/40、Metadata/Mutation/Debug 45/45。每组均为 FormalHost 3/6/15，最终生成 9 CSV、9 Markdown、9 HTML、4 原始日志及 `formal-host-complete.json`；所有报告哈希已复核。RC 脚本同步改为从实际 CSV 生成 `BenchmarkCount`、`GroupCounts` 和批准过滤器，缺报告/错误 Job/参数时 fail-closed。
