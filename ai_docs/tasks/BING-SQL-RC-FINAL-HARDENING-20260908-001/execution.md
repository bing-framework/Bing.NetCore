<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RC-FINAL-HARDENING-20260908-001
AI_EXECUTION_FINISHED_AT: 2026-09-08T16:08:00+08:00

# 实施执行报告

## 执行结论

本轮执行完成仓库内 CI 编排、文档一致性、测试追溯、当前 HEAD 回归和 FormalHost 性能审计，结论为 `PARTIAL`，RC 结论为 `RC NOT READY`。未配置的受保护 AppVeyor/MySQL 环境导致正向外部 Provider 证据、8/8 `ReleaseEvidence`、同 session aggregate 无法在本地合法产生；独立 Review 已完成仓库内复核但结论为 `BLOCKED`，没有使用历史、dirty、DryJob 或手工拼接制品替代。

## 任务信息

- Task：`BING-SQL-RC-FINAL-HARDENING-20260908-001`
- Plan：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/plan.md`
- HEAD：`de1b7d9da516e621b5d2aedd42715c4c247f2f11`
- 当前源状态：`dirty`，改动均为本 Task 的 CI、文档、追溯和报告文件。
- SDK：`8.0.424`；FormalHost runtime：`.NET 8.0.30`；BenchmarkDotNet：`0.14.0`。
- 未执行 `git commit`、`git push`、merge、PR 或包发布。

## 计划执行情况

| 阶段 | 结果 | 说明 |
| --- | --- | --- |
| FINAL-001 证据边界 | 完成 | 固定 HEAD、dirty 状态和本 Task 独立 artifact 根；本地结果不升级为 ReleaseEvidence。 |
| FINAL-002 AppVeyor 拓扑 | 完成 | 移除独立 aggregate matrix lane；release job 单独串行调用 RC orchestrator；增加 AppVeyor lane self-test 和 release `git diff --check`。 |
| FINAL-101 MySQL 环境 | 阻塞 | 无安全 TLS/RSA/认证数据库和 reset 授权；缺 gate ValidateOnly 按预期 fail-closed。 |
| FINAL-102 Unit/Analyzer/API/SQLite | 完成 | Release build、11 个验证项目、缓存专项、脚本 AST、自检和 SQLite 双 TFM 已完成。 |
| FINAL-201 Provider protected positive | 阻塞 | MySQL/PostgreSQL/SQL Server 双 TFM 8 个正向 ReleaseEvidence 未执行。 |
| FINAL-202 FormalHost | 本地审计完成 | 当前 HEAD 136/136，9 CSV/9 Markdown/9 HTML，全部 FormalHost 3/6/15；source dirty，且 runtime 与历史基线不同。 |
| FINAL-301 aggregate | 未完成 | 受保护同 session 输入不存在，CLI 未被伪造调用；fail-closed 仍有效。 |
| FINAL-302 文档/追溯 | 完成 | 集成测试文档、SQL 使用文档、AppVeyor 文档和本 Task 追溯段已同步。 |
| FINAL-303 最终 RC Gate | 部分完成 | 本地门禁通过，受保护 positive 和独立 Review 仍需外部环境。 |

## 已完成事项

1. `appveyor.yml` 现在只 materialize `common`、`mysql`、`postgresql`、`sqlserver`、`release`；aggregate 由同一 release job 的 RC orchestrator 生成，不再假设 matrix job 共享工作区。
2. `Invoke-ReleaseCandidateValidation.ps1 -SelfTest` 新增 AppVeyor lane、unit inventory/hash 篡改和 FormalHost manifest 自检，并全部通过。
3. `docs/integration-testing.md`、`docs/sqlquery-usage.md`、`docs/testing/database-integration-tests.md` 统一显式 `integration.runsettings.local`、Provider-scoped gate、专用数据库/reset、MySQL TLS/RSA 和受保护 CI 禁止 DefaultConnection/global gate 的口径。
4. `ai_docs/sql-metadata-test-traceability.md` 新增本 Task 的最终生产符号、直接测试方法、制品路径和 ReleaseEvidence 边界。
5. 生成 `artifacts/reports/BING-SQL-RC-FINAL-HARDENING-20260908-001/` 下的最终报告、summary JSON 和 artifact index。
6. `eng/Bing.ProviderEvidence.Cli/Program.cs` 与 RC orchestrator 已把 FormalHost 九组 136 行合同、unit inventory 的 21 个 project/TFM、session/source、时间窗、hash 和 TRX 文件名归属纳入 aggregate 门禁；aggregate 保留重复 Provider/TFM 输入，由唯一性检查 fail-closed。

## 测试结果

### Unit、Analyzer 和缓存

- `Bing.Data.Sql.Tests`：net6.0 `1307 passed`；net8.0 `1307 passed`。
- Provider/核心 Unit 项目（Core、Dapper.Core、MySQL、PostgreSQL、SQL Server、SQLite、Oracle、CustomProvider）：双 TFM 均 `0 failed`，合计 `2474 passed`。
- `Bing.Test.Shared`：net6.0/net8.0 各 `91 passed`。
- `Bing.Data.Sql.Analyzers.Tests`：net8.0 `32 passed`。
- EntityMapping/BoundedLazy/CacheKey 职责筛选：net6.0、net8.0 各连续 3 轮，每轮 `30 passed`，共 `180 passed`。
- 直接 Unit/Shared/Analyzer 验证合计：`5302 passed, 0 failed, 0 skipped`。

### SQLite 和 Provider 门禁

- `Invoke-SqliteContractTests.ps1` net6.0：`1 passed, 0 failed, 0 skipped`，Matrix Entries=2，`TestGenerated`。
- `Invoke-SqliteContractTests.ps1` net8.0：`1 passed, 0 failed, 0 skipped`，Matrix Entries=2，`TestGenerated`。
- `Invoke-ProviderIntegrationTests.ps1 -Provider MySql|PostgreSql|SqlServer -SelfTest`：串行均通过；self-test 生成的矩阵保持 `ReleaseReady=False`。
- MySQL 无 gate `-ValidateOnly`：退出码 1，诊断 `BlockedReason=ProviderGateDisabled`，无密码或连接串泄漏。
- 受保护 RC 负向探针（设置本地 `CI=true`/`APPVEYOR_BUILD_ID`）：退出码 1，拒绝 dirty source，证明 clean-source gate fail-closed。

### FormalHost

- 命令使用批准的九组 filter，BenchmarkDotNet 日志确认 `executed benchmarks: 136`，退出码 0。
- 报告完整性：9 CSV、9 Markdown、9 HTML；CSV 总计 136 行；所有行 `Job=FormalHost`、`LaunchCount=3`、`WarmupCount=6`、`IterationCount=15`；无无效行。
- Manifest：`artifacts/benchmarks/BING-SQL-RC-FINAL-HARDENING-20260908-001/formal-host/formal-host-complete.json`，28 个 SHA-256 记录重新计算全部匹配。
- 结果是当前 HEAD 的本地性能审计；source 为 dirty，runtime 为 .NET 8.0.30，不能与历史 .NET 8.0.27 基线直接计算 Delta，也不能作为 ReleaseEvidence。

### Smoke

- CI smoke：1 row，`Dry` Job。
- SQLite E2E smoke：24 rows，`Dry` Job。
- 两者仅作诊断，不进入 FormalHost 或 RC aggregate。

## Build、脚本和文档验证

- `dotnet build .\\Bing.All.sln -c Release --no-restore --nologo`：成功，0 errors；89 条既有 warnings。
- `git diff --check`：通过；Git 对 `eng/ci/Invoke-ReleaseCandidateValidation.ps1` 的 CRLF/LF 提示是既有换行提示，不是 check failure。
- `eng/ci/Invoke-ReleaseCandidateValidation.ps1`、`Invoke-ProviderIntegrationTests.ps1`、`Invoke-SqliteContractTests.ps1`：PowerShell AST parse 通过。
- RC AppVeyor lane、unit inventory/hash tamper、FormalHost manifest self-test：通过。
- Aggregate negative probe：缺少 unit inventory、同 session/source 和 clean FormalHost 时，CLI 输出 `ReleaseReady=false`、`UnitEvidenceComplete=false`、`ProviderRunsValidated=0`，证明 fail-closed。
- Aggregate duplicate policy：移除按 Provider/TFM 选择“最新”运行的折叠逻辑，重复输入现在会到达 `ProviderReleaseReadiness` 的 `matches.Length != 1` 唯一性门禁。
- Unit identity policy：loader 解析 `<Project>-<TFM>.trx` 文件名，inventory 校验文件名、解析出的项目/TFM、路径和计数必须一致，避免跨项目冒用通过 TRX。
- 受影响 Markdown/YAML/PowerShell 文件：UTF-8 严格解码通过；文档相对链接检查通过。
- 旧口径扫描仅保留明确的历史直接本地 DefaultConnection 兼容回退说明；受保护 runner/CI 禁止该回退。

## 部分/未完成事项

1. 没有真实受保护 AppVeyor build identity、Provider-scoped secret、TLS/RSA 配置和专用数据库，因此 MySQL、PostgreSQL、SQL Server 双 TFM 正向运行未执行。
2. 因上述输入缺失，没有生成 8 个同 session/source 的 `ReleaseEvidence`，也没有运行合法的正向 aggregate。
3. 当前 FormalHost 虽为 136/136 完整，但 source dirty 且 runtime 与历史不同，只能作为本地审计；不能满足 Definition of Done 的 clean protected evidence。
4. 独立 `review.md` 已完成第二轮复核，状态为 `BLOCKED`：仓库内没有未解决的 `MUST_FIX`/`SHOULD_FIX`，但 protected positive、8/8 ReleaseEvidence 和同 session aggregate 仍缺外部输入，不能写 `PASS`。

## 计划偏差和基线问题

- 任务状态文件第一次普通写入因 Windows ACL 返回 EPERM；按执行规范使用受控 elevated 重试后成功注册当前 Task，未修改业务文件。
- Provider self-test 曾与长时间 FormalHost 共享 Release 输出触发构建文件锁；停止并行构建后串行重跑三类 self-test 全部通过。该事件不改变源码或测试结论。
- 既有 baseline 中的 MySQL `caching_sha2_password` SSL/RSA 认证失败、旧 source/dirty FormalHost 和缺少 AppVeyor 正向执行仍是外部阻塞；本轮没有用本地结果覆盖这些问题。

## API、数据和配置变化

- 没有新增或删除公共 API，没有修改 SQL 输出、Provider 能力语义、实体映射或缓存生产实现。
- AppVeyor 发布拓扑从“独立 aggregate matrix lane”收敛为单一 protected `release` producer。
- 文档和追溯更新不包含连接字符串、密码、secret 或生产数据库信息。

## 风险与回归关注点

- 外部数据库必须使用专用非生产数据库、显式 reset 授权和 Provider-scoped secret；MySQL 必须由受信 TLS 或受控 RSA public key retrieval 解决认证策略，不能降低安全门槛。
- 任何未来 protected run 都必须使用新的 run id/session，从 clean source 重新生成所有 TRX、sidecar、binary hash、FormalHost manifest 和 aggregate；不能复用本地或历史 artifact。
- FormalHost 历史基线与当前 .NET runtime 不同；发布前需在同一环境和同一 source/session 下重新比较。

## Git 状态

当前预期改动：

```text
 M ai_docs/sql-metadata-test-traceability.md
 M appveyor.yml
 M docs/integration-testing.md
 M docs/sqlquery-usage.md
 M docs/testing/database-integration-tests.md
 M eng/Bing.ProviderEvidence.Cli/Program.cs
 M eng/ci/Invoke-ReleaseCandidateValidation.ps1
?? ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/
```

未自动执行 Git 发布操作。下一步是由具备受保护 AppVeyor/MySQL 权限的环境从 clean commit 运行 RC orchestrator，然后基于同 session/source 制品重新进行独立 review；在此之前状态保持 `PARTIAL` / `RC NOT READY`。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md`

#### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `eng/ci/Invoke-ReleaseCandidateValidation.ps1`
  - `eng/Bing.ProviderEvidence.Cli/Program.cs`
- 根因：FormalHost 之前只检查报告文件、Job 和 `3/6/15`，没有锁定批准的 136 行和九组计数。
- 修复：两侧加入批准集合计数（19/6/2/3/24/28/12/27/15，总计 136）、逐行校验 CSV 的 Job/Launch/Warmup/Iteration、clean source 检查和缺行 self-test。
- 验证：RC `-SelfTest`、CLI Release build、PowerShell AST：PASS；真实本地 FormalHost artifact hash 仍为 136 行、28 个 hash 全匹配。

#### FIX-002

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `eng/ci/Invoke-ReleaseCandidateValidation.ps1`
  - `eng/Bing.ProviderEvidence.Cli/Program.cs`
- 根因：unit TRX 只有 VSTest 计数，未被 aggregate 绑定到当前 session/source，也没有防止复用旧结果。
- 修复：RC unit 阶段拒绝非空结果目录、确认每个预期 TRX 已生成并记录 SHA-256/时间；写入 `unit-inventory.json`，CLI 校验 clean source、session、21 个 project/TFM、路径、hash、时间窗和零失败零跳过计数，并将结果纳入 `UnitEvidenceComplete`。
- 验证：CLI Release build、RC `-SelfTest`、PowerShell AST：PASS；缺 inventory 或不完整 unit 输入会使 aggregate 的 unit 门禁保持 false。

#### FIX-003

- 严重程度：LOW
- 处理要求：OPTIONAL
- 执行状态：COMPLETED
- 修改文件：
  - `docs/testing/database-integration-tests.md`
- 修复：明确区分 settings XML 中未允许变量被忽略与进程环境中全局/其他 Provider 冲突变量被 preflight 拒绝，和 runner 实际行为一致。
- 验证：旧口径扫描、文档相对链接检查、Provider runner self-test：PASS。

### Round 1 汇总

- MUST_FIX：已完成 FIX-001。
- SHOULD_FIX：已完成 FIX-002。
- OPTIONAL：FIX-003 已额外完成，文档口径已统一。
- PARTIAL/BLOCKED：真实受保护 AppVeyor/MySQL、8/8 Provider ReleaseEvidence、同 session aggregate 和最终 PASS Review 仍缺外部输入。
- 回归验证：CLI build、RC/Provider self-test、PowerShell AST 通过；未重跑 95 分钟 FormalHost，已有当前 HEAD 136/136 本地审计保留为非发布证据。
- 下一步：重新执行独立 Review。

### Round 2

- Review 状态：`BLOCKED`（`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md`）。
- 复核结论：FIX-001、FIX-002、FIX-003 均 `CLOSED`，没有未解决的 `MUST_FIX` 或 `SHOULD_FIX`。
- 阻塞条件：真实受保护 AppVeyor build identity、MySQL TLS/RSA/专用数据库/reset 授权、8/8 Provider `ReleaseEvidence`、同 session/source aggregate 和 clean FormalHost 发布制品均缺失。
- 本地验证：CLI Release build、RC/Provider self-test、aggregate 负向探针、PowerShell AST、solution build 和 `git diff --check` 通过；没有重跑 95 分钟 FormalHost，保留已有 136/136 dirty 本地审计作为非发布证据。

### Round 3 当前 Diff 复核

- 变更：`Program.cs` 不再按 Provider/TFM 选择“最新”运行；重复输入保留并交给 `ProviderReleaseReadiness` 的 `matches.Length != 1` 唯一性门禁。
- 验证：CLI Release build、RC self-test、Shared Provider 唯一性测试双 TFM 各 15 项、PowerShell AST、`git diff --check` 和 aggregate 负向探针通过。
- Review 结论：未发现新的 `MUST_FIX` 或 `SHOULD_FIX`；`review.md` 仍为 `BLOCKED`，仅因受保护 AppVeyor/MySQL、8/8 ReleaseEvidence、同 session/source aggregate 和 clean FormalHost 证据缺失。

### Continuation Audit

- 当前环境复核仍未发现 `CI`、`APPVEYOR_BUILD_ID`、Provider-scoped gate/connection、reset 授权或 evidence session/source 变量；HEAD 仍为 `de1b7d9da516e621b5d2aedd42715c4c247f2f11`，工作树为 dirty。
- 直接运行 `Invoke-ReleaseCandidateValidation.ps1 -Configuration Release` 以退出码 1 拒绝非受保护环境；直接运行 MySQL `-ValidateOnly` 以退出码 1 报告 `ProviderGateDisabled`，生成的无密诊断保持 `ConnectionConfigured=false`、`DatabaseReachable=false`，未产生正向 ReleaseEvidence。
- 复核验证：Evidence CLI Release build 0 errors；RC lane/FormalHost self-test PASS；`ProviderContractRunnerTest` net6/net8 各 15 passed；`Bing.Test.Shared` 全量 net6/net8 各 91 passed；`git diff --check` PASS。
- 结论未变化：本地 fail-closed 合同保持有效，真实 protected positive、8/8 ReleaseEvidence、同 session/source aggregate 和 clean FormalHost 仍需外部受保护环境；RC 继续为 `NOT READY`。

### Round 4 负向合同补强

- 新增 `Bing.Test.Shared.ProviderContractRunnerTest.ReleaseReadiness_WhenProviderFrameworkRunIsDuplicated_ShouldRemainFalse`，构造完整四 Provider × 双 TFM 集合后重复 `MySql/net6.0`，直接断言 readiness 为 false 且重复键进入缺失列表。
- 该测试 net6.0、net8.0 均为 `1 passed, 0 failed, 0 skipped`；最终生产符号到测试方法追溯已同步到 `ai_docs/sql-metadata-test-traceability.md`。
- 该补强只覆盖重复输入的本地职责合同，不改变 protected positive 缺失、8/8 ReleaseEvidence、同 session/source aggregate 或 clean FormalHost 的外部阻塞结论。

### Round 5 Unit Inventory 自检补强

- `Invoke-ReleaseCandidateValidation.ps1 -SelfTest` 新增 `Invoke-UnitInventorySelfTest`：用 21 个最小合法 TRX 和 clean session/source inventory 调用真实 Evidence CLI，先断言 `UnitEvidenceComplete=true`、`UnitTestsPassed=true` 且 `ReleaseReady=false`，再保持 XML 合法地修改一个 TRX 内容并断言 `UnitEvidenceComplete=false`。
- 自检输出 `Unit inventory self-test passed.`，临时 unit/provider 目录在 `finally` 中清理；PowerShell AST、CLI build、RC self-test 和 `git diff --check` 通过。
- 该自检仅证明 unit inventory validator 的正向识别与篡改拒绝，不生成 Provider `ReleaseEvidence`，不改变受保护环境阻塞结论。

### Round 6 Review Fix

- Review 状态：`NEEDS_FIX`；Fix Scope：`recommended`；Review 文件：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md`。

#### FIX-004

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 修改文件：`eng/ci/Invoke-ReleaseCandidateValidation.ps1`。
- 根因：RC 主流程只在开始及 Unit 阶段读取源码身份，Provider/SQLite/Analyzer/API/FormalHost 完成后直接使用旧身份聚合。
- 修复：新增严格 Ordinal 的 `Assert-SourceIdentity`；aggregate 前重新读取并比较 source identity，最终 READY 输出前再次读取，并确认与 aggregate 使用的身份一致；新增 Unit 后漂移和 aggregate 后漂移 self-test。
- 验证：source identity self-test、完整 RC `-SelfTest`、PowerShell AST：PASS；漂移场景均 fail-closed。

#### FIX-005

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 修改文件：`eng/Bing.ProviderEvidence.Cli/Program.cs`、`eng/ci/Invoke-ReleaseCandidateValidation.ps1`。
- 根因：Unit TRX 的 Project/TFM 仅由可改名文件名提供，验证器未检查内部程序集身份或计数一致性。
- 修复：解析并校验每个 `TestMethod@codeBase` 的期望程序集和 TFM 路径；拒绝缺少测试定义、错误/混合程序集的 TRX；校验 `total=executed+notExecuted`、`executed=passed+failed`；将身份结果纳入 `UnitEvidenceComplete`。RC synthetic TRX 加入真实程序集定义，self-test 覆盖错误程序集、缺定义、计数不一致、哈希篡改和合法 21 项输入。
- 验证：CLI Release build、完整 RC `-SelfTest`、Bing.Test.Shared 双 TFM 回归：PASS。

#### FIX-006

- 严重程度：LOW；处理要求：OPTIONAL；执行状态：DEFERRED。
- 未修改原因：本轮 fixScope 为 `recommended`，按协议默认跳过 OPTIONAL；该文案不影响 READY 门禁。

### Round 6 汇总

- MUST_FIX：FIX-004、FIX-005 已完成。
- SHOULD_FIX：无。
- OPTIONAL：FIX-006 按默认范围跳过并记录 DEFERRED。
- PARTIAL/BLOCKED：受保护 AppVeyor/MySQL、8/8 Provider ReleaseEvidence、同 session/source aggregate 和 clean FormalHost 仍需外部环境；RC 仍为 `NOT READY`。
- 下一步：执行 `task-finish.mjs`，随后重新进行独立 Review。

### Round 7 Review Fix

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md`

#### FIX-007

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`eng/ci/Invoke-ReleaseCandidateValidation.ps1`。
- 修复：aggregate CLI 改为写入 `artifacts/test-results/.rc-staging-<run>` 临时目录；只有在 `ReleaseReady=true`、发布前源码身份复核通过后，才将聚合目录移动到权威 `artifacts/test-results/release-candidate/provider-aggregate`。移动后再次复核源码身份；若漂移则删除已发布目录并失败退出。临时目录位于 AppVeyor 上传 glob 之外，并在 `finally` 清理。
- 自检：`Invoke-SourceIdentitySelfTest` 覆盖 stable publish 和 post-publish drift，后者断言失败且最终目录不存在正向矩阵。

#### FIX-008

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`eng/ci/Invoke-ReleaseCandidateValidation.ps1`。
- 修复：Unit inventory self-test 保存完整合法 TRX/inventory 快照，每个负向场景前恢复 clean fixture；分别覆盖错误程序集、错误 TFM、混合定义、缺少定义、Counters 不一致和 hash 篡改。
- 自检：每个场景单独调用真实 Evidence CLI aggregate，并断言仅该场景变更导致 `UnitEvidenceComplete=false`。

### Round 7 验证

- `pwsh -NoProfile -File .\eng\ci\Invoke-ReleaseCandidateValidation.ps1 -SelfTest`：PASS（AppVeyor lane、source identity publish、Unit inventory、FormalHost）。
- `dotnet build .\eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj -c Release --no-restore --nologo`：PASS，0 error；4 个 NU1900 网络漏洞源警告。
- `Bing.Test.Shared` net6.0/net8.0 串行 `dotnet test --no-build --no-restore`：各 91 passed，0 failed，0 skipped。
- PowerShell AST：PASS；`git diff --check`：PASS（仅 CRLF/LF 提示）。

### Round 7 汇总

- MUST_FIX：无未解决项。
- SHOULD_FIX：FIX-007、FIX-008 已完成。
- OPTIONAL：FIX-006 按 `fixScope=recommended` 跳过并保持 DEFERRED。
- 外部阻塞未变化：受保护 AppVeyor/MySQL、8/8 Provider `ReleaseEvidence`、同 session/source aggregate 和 clean FormalHost 仍需外部环境；RC 仍为 `NOT READY`。
- 下一步：重新执行独立 Review；在受保护证据缺失时，Review 仍应保持 `BLOCKED`，不得判定 `PASS` 或 `RC READY`。

### Round 8 Review Fix

- Review 状态：`NEEDS_FIX`；Fix Scope：`recommended`；Review 文件：`ai_docs/tasks/BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md`。

#### FIX-009

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 修改文件：`eng/ci/Invoke-ReleaseCandidateValidation.ps1`。
- 根因：Provider runner 在清理进程环境后无条件设置 `ALLOW_DATABASE_RESET_FOR_TESTS=true`，使 RC 编排器能够自行授予数据库 reset 权限。
- 修复：`Invoke-ProviderRuns` 在任何 Provider 执行前读取并严格校验外部环境中的 `ALLOW_DATABASE_RESET_FOR_TESTS=true`；缺失、`false` 或其他值在 runner 调用前失败。通过环境清理后仅恢复已验证的 canonical `true`，并增加可注入 runner 的 reset 授权负向 self-test，验证拒绝场景不会调用 runner。
- 验证：RC `-SelfTest` 输出 `Reset authorization self-test passed.`；PowerShell AST：PASS。

#### FIX-010

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`eng/Bing.ProviderEvidence.Cli/Program.cs`。
- 根因：Integration Markdown 仅根据 failed/core skip 和 clean source 判断 PASS，可能把 `ReleaseEvidenceValid=false` 或未执行的结果显示为 PASS；文案仍暗示重复 Provider/TFM 会被折叠。
- 修复：报告 PASS 必须同时满足 `ReleaseEvidenceValid=true`、`ExecutionStatus=Executed`、`Executed>0`、`Failed=0`、`CoreSkipped=0` 和 clean source；否则输出 `NOT VERIFIED`（失败或核心跳过仍输出 `FAILED`）。文案改为保留全部 Provider/TFM 输入，唯一性由 readiness 门禁校验。
- 验证：Evidence CLI Release build：0 errors（4 个 NU1900 网络漏洞源警告）；RC `-SelfTest`：PASS。

### Round 8 验证与结论

- `pwsh -NoProfile -File .\\eng\\ci\\Invoke-ReleaseCandidateValidation.ps1 -SelfTest`：PASS（AppVeyor lane、reset authorization、source identity、unit inventory、FormalHost）。
- `Bing.Test.Shared` net6.0/net8.0：各 91 passed，0 failed，0 skipped。
- PowerShell AST：PASS；`git diff --check`：PASS（仅 CRLF/LF 提示）。
- MUST_FIX：FIX-009 已完成；SHOULD_FIX：FIX-010 已完成；OPTIONAL FIX-006 继续按 `recommended` 跳过并保持 `DEFERRED`。
- 外部阻塞未变化：受保护 AppVeyor/MySQL、8/8 Provider `ReleaseEvidence`、同 session/source aggregate 和 clean FormalHost 仍需外部环境；RC 仍为 `NOT READY`。下一步执行独立 Review，预计在外部证据缺失时保持 `BLOCKED`。
