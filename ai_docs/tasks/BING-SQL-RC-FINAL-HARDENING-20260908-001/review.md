<!-- AI_REVIEW_STATUS: BLOCKED -->
AI_TASK_ID: BING-SQL-RC-FINAL-HARDENING-20260908-001
AI_REVIEWED_AT: 2026-09-08T16:21:16.3070866+08:00

# 独立代码审查

## 结论

状态为 `BLOCKED`。本轮没有发现新的 `MUST_FIX` 或 `SHOULD_FIX`：`FIX-009` 已使 RC 编排只能恢复外部预先授权的 reset gate，缺失或非法值会在任何 Provider runner 调用前失败；`FIX-010` 已让集成报告仅在归档 Release Evidence 有效、真实执行、执行数大于零、零失败、零核心 Skip 且 clean source 时显示 `PASS`。

仓库内 fail-closed 行为已闭环，但正式验收所需的真实受保护 AppVeyor build identity、MySQL 安全测试环境、四核心 Provider 双 TFM 8/8 `ReleaseEvidence`、clean FormalHost 136/136 和同 session/source 正向 aggregate 仍不存在。根据计划 Definition of Done，这些关键发布证据不能由本地 self-test、dirty benchmark 或 TestGenerated SQLite 结果替代，因此不能判定 `PASS` 或写入 `RC READY`。

## Findings

未发现新的仓库内可执行修复项。本轮不生成新的 `FIX-xxx`。

## 本轮修复复核

| FIX | 结果 | 复核证据 |
| --- | --- | --- |
| FIX-009 | CLOSED | `Invoke-ProviderRuns` 首先调用 `Get-ValidatedResetAuthorization` 读取外部 `ALLOW_DATABASE_RESET_FOR_TESTS`；只有显式 true 被 canonicalize，循环清理后恢复该已验证值。reset 缺失、false、其他值的 RC self-test 均在 runner 调用前拒绝。Provider runner 自身仍保留 reset、专用连接和数据库安全 preflight。 |
| FIX-010 | CLOSED | `CreateIntegrationMarkdown` 的 `PASS` 分支已包含 `ReleaseEvidenceValid`、`ExecutionStatus=Executed`、`Executed>0`、零失败、零核心 Skip 和 clean source；旧“仅记录最新”文案已改为保留全部输入并由 readiness 校验唯一性。独立反射行为探针确认 invalid/TestGenerated/zero 为 `NOT VERIFIED`、合法 evidence 为 `PASS`、失败计数为 `FAILED`。 |

## 验收矩阵

| 计划项 | 结果 | 证据与说明 |
| --- | --- | --- |
| FINAL-001 证据边界 | PARTIAL | session/source/hash 和 staging 发布合同已实现；当前工作树为 dirty，没有 clean protected identity。 |
| FINAL-002 AppVeyor 拓扑 | PARTIAL | 独立 aggregate lane 已移除，release 是唯一 RC producer；配置 self-test 通过，但真实 AppVeyor release job 未执行。 |
| FINAL-101 MySQL 安全环境 | NOT_VERIFIABLE | reset 授权绕过已关闭；MySQL TLS/RSA、专用数据库、Provider-scoped secret 和 protected positive 仍缺外部环境。 |
| FINAL-102 Unit/Analyzer/API/SQLite | PARTIAL | 本地 Release build、Shared 双 TFM、既有 Unit/Analyzer/SQLite 证据通过；这些结果不是同 session protected 发布证据。 |
| FINAL-201 Provider protected positive | NOT_VERIFIABLE | 当前任务 summary 记录 Provider ReleaseEvidence 为 `0/8`；三种外部 Provider 与 SQLite 的同源 protected 运行未形成。 |
| FINAL-202 FormalHost | PARTIAL | 本地 manifest 为 136 行，但 `SourceState=dirty`、session 为 local，不能作为 ReleaseEvidence。 |
| FINAL-301 aggregate | PARTIAL | validator、重复输入和报告状态均 fail-closed；现有负向 aggregate 为 `ReleaseReady=false`，没有合法正向输入。 |
| FINAL-302 文档/追溯 | PASS | Provider-scoped gate、显式 reset、专用数据库和重复输入口径与当前实现一致。 |
| FINAL-303 最终 RC Gate | NOT_VERIFIABLE | 所有仓库内 MUST_FIX/SHOULD_FIX 已关闭，但 protected positive、clean FormalHost 和正向 aggregate 缺失。 |

## 独立验证

| 验证 | 结果 |
| --- | --- |
| `dotnet build .\\Bing.All.sln -c Release --no-restore --nologo` | PASS，0 error，89 warning。 |
| `Bing.Test.Shared` net6.0 | PASS，91 passed，0 failed，0 skipped。 |
| `Bing.Test.Shared` net8.0 | PASS，91 passed，0 failed，0 skipped。 |
| RC `-SelfTest` | PASS：AppVeyor lane、reset authorization、source identity、Unit inventory、FormalHost。 |
| Provider runner `-SelfTest` | MySQL、PostgreSQL、SQL Server 均 PASS；self-test matrix 保持 `ReleaseReady=False`。 |
| Integration Markdown 独立行为探针 | PASS：invalid/TestGenerated/zero、valid、failed 五类状态及唯一性文案符合预期。 |
| PowerShell AST | PASS。 |
| `git diff --check` | PASS；仅 CRLF/LF 提示。 |
| 非受保护 RC 入口 | 按预期以退出码 1 拒绝，错误为必须在 protected AppVeyor CI 运行。 |
| 当前环境与 artifact | `CI`、`APPVEYOR_BUILD_ID`、reset、三种 Provider gate/connection 均缺失；task summary 为 `ReleaseReady=false`、Provider `0/8`；FormalHost 为 dirty/local。 |

## 残余风险

- `CreateIntegrationMarkdown` 的状态分支目前由本轮独立行为探针验证，尚无提交到仓库的专门格式化测试；逻辑简单且 readiness 本身另有直接测试，本轮不升级为 `SHOULD_FIX`。
- aggregate 发布后源码漂移时的删除仍使用 `Remove-Item -ErrorAction SilentlyContinue`；此前审查记录的极低概率文件系统残余风险未扩大，本轮不新增修复项。

## 外部解除条件

在 clean commit 的受保护 AppVeyor release job 中显式注入 reset 授权及三种 Provider 专属 secret，完成 MySQL/PostgreSQL/SQL Server/SQLite 双 TFM 共 8/8 真实执行；同一 session/source 下重新生成 Unit、Analyzer/API、FormalHost 136/136 和 aggregate，并重验 hash、执行计数、零核心 Skip 与无密输出。只有这些证据齐备后，才能再次独立 Review 并考虑 `PASS`；此前 RC 必须保持 `NOT READY`。
