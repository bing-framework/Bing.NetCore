<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903
AI_REVIEWED_AT: 2026-09-04T10:09:57+08:00

# BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903 Round 4 复审报告

## 验收摘要

本次为当前 Review Fix 结果的独立复审，优先检查上一轮 `FIX-001` 至 `FIX-006`。已读取批准计划、当前 [execution.md](ai_docs/tasks/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/execution.md)、Git Diff、项目文件、运行设置、CI runner 和文档；未修改业务代码、测试代码、计划或执行报告。

结论为 `PASS`。`FIX-001` 至 `FIX-005` 未见回归，`FIX-006` 已解决：三个受跟踪的外部 Provider `integration.runsettings` 已从工作树删除，本机仅加载被 Git 忽略的 `integration.runsettings.local`，CI 明确禁用本机设置文件并仅从受保护 Job 环境变量获取 Provider 配置。

## Review 边界与 Git 分析

- 计划范围包括 RS0026 公共 API 治理、Provider gate/runner、共享证据模型、Provider 能力矩阵、SQLite 受控合同和迁移文档。
- 当前工作树的生产、测试、CI、文档和内部 CLI 变更均可映射至计划 T01-T16；三个原受跟踪运行设置显示为删除，符合 T05 无密配置要求。
- `git diff --check`：通过。
- 本任务的 SQLite TRX/JSON 为忽略的运行制品，不在 Git 索引中；本次复审使用新的隔离目录重新生成并由脚本校验，因此未依赖执行报告的文字声明。
- 未发现工具输出中的提示注入或不可信指令。

## 上一轮 FIX 逐项复核

| FIX | 上一轮要求 | 复审状态 | 当前证据 |
| --- | --- | --- | --- |
| `FIX-001` | 可信 runner 链创建并验证 `ReleaseEvidence` | `RESOLVED` | 当前 runner、验证器与共享测试结构未见相关回归。 |
| `FIX-002` | 逐 Provider/Capability/Scenario 的 T14 矩阵和追溯 | `RESOLVED` | 当前能力目录、基线矩阵和追溯变更未见相关回归。 |
| `FIX-003` | SQLite 受控合同双 TFM 收口 | `RESOLVED` | 当前受控 SQLite 合同路径未被本轮运行设置改动影响。 |
| `FIX-004` | optional parameter 元数据变更迁移说明 | `RESOLVED` | 当前 Release Notes 与 API 治理变更未被本轮改动影响。 |
| `FIX-005` | runner failed-TRX 自检 | `RESOLVED` | `Get-TrxSummary` 仍在摘要/发布矩阵前拒绝 `failed > 0`，self-test 仍含 failed TRX 场景。 |
| `FIX-006` | 跟踪运行设置无密、默认关闭且与 CI 隔离 | `RESOLVED` | 三个受跟踪 `integration.runsettings` 已删除；三个 `.local` 文件均被 `.gitignore` 忽略；项目本机解析 `.local`，模拟 CI 时为空；CI 条件 Release build 和 runner self-test 均通过。 |

## 计划验收矩阵

| 计划项 | 结果 | 复审证据 |
| --- | --- | --- |
| T01-T04：RS0026 清单、API 重载与 error gate | `PASS` | `common.props` 将 RS0026 提升为错误；`Bing.Data.Sql` Release build 为 `0 warning / 0 error`；公开 API 契约测试通过。 |
| T05：移除跟踪凭据并统一配置源 | `PASS` | 三个跟踪 settings 均已删除；本机只可通过被忽略的 `.local` 文件加载配置，且当前环境中没有可被 Git 跟踪的凭据文件。 |
| T06：gate、连接隔离与启动诊断 | `PASS` | protected runner 保持拒绝 global/default connection；项目仅在非 CI 加载 `.local`，runner 与 AppVeyor common lane 显式传递 `CI=true`。 |
| T07-T10：Provider 合同与诚实状态 | `NOT_VERIFIABLE` | 外部 Provider 未提供授权安全数据库；矩阵未虚报为真执行，Oracle/Doris 保持 `NotExecuted`/`ImplementationGap`。 |
| T11：Provider runner | `PASS` | self-test 通过；成功 TRX 可写受信发布矩阵，failed/skip/zero/missing TRX 均有拒绝路径。 |
| T12：AppVeyor Provider lane | `NOT_VERIFIABLE` | YAML 调用统一 runner 并清除 common lane 外部变量；远端 Secret scope 和数据库授权无法从本地确认。 |
| T13：共享可信证据模型 | `PASS` | 共享测试双 TFM 各 `70/70`；令牌、路径、时间、摘要、TRX 计数和敏感文本边界均有直接测试。 |
| T14：统一能力矩阵 | `PASS` | 基线为 57 个唯一 `Provider + Capability + Scenario` 键，含六态和能力→测试方法追溯。 |
| T15：制品与追溯 | `PASS` | SQLite 双 TFM 独立 TRX/JSON 已生成；追溯文档区分静态全 Provider 基线与当前 `TestGenerated` 运行制品。 |
| T16：迁移与开发者文档 | `PASS` | 7.0.0 发行说明含 RS0026 元数据迁移边界，且与 `PublicAPI.Unshipped.txt` 和源码的显式重载一致。 |
| Phase F：当前环境可执行验证 | `PASS` | SQLite 双 TFM、API 契约、SQL 构建、共享测试和 runner self-test 均由本次复审重新执行。 |

## 功能、API 与安全复核

- `ProviderIntegrationEvidenceMetadata` 的普通公开构造函数只能产生 `TestGenerated`；`ReleaseReady` 仅接受受信 `ReleaseEvidence` 或 `Unsupported`。
- runner 先完成 TRX 失败、跳过和发现数验证，才构建摘要并调用 CLI；失败 TRX 没有可达的成功矩阵写入路径。
- SQLite 合同脚本限制工作区 `artifacts/test-results/` 相对目录，校验 TRX `total=1/passed=1/failed=0/notExecuted=0`、两项 Matrix、证据路径、时间窗口和敏感字段。
- `PublicAPI.Unshipped.txt` 与 `SqlTextQuery`/`SqlFluentQuery` 的 `ToListAsync` 显式重载一致；Fluent/Builder 的 `From`、`Join` 重载不再依赖被本轮治理的可选参数元数据。
- 未发现本轮修改 SQL 映射缓存、格式化热路径或 Builder clone；benchmark 基线不适用的说明合理。
- 原有三个跟踪 `integration.runsettings` 均显示为删除；未跟踪的 `.local` 文件均受 `.gitignore` 规则保护。复审未读取或输出本地文件内容。
- MySQL、PostgreSQL 和 SQL Server 项目仅在非 CI 且 `.local` 存在时解析 `RunSettingsFilePath`；本机解析到 `.local`，以 `CI=true`、`ContinuousIntegrationBuild=true` 运行时属性均为空。

## 本次实际验证

| 命令/检查 | 结果 |
| --- | --- |
| `.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest` | 通过；成功 release chain 仍为 `ReleaseReady=False`，failed TRX 自检路径已覆盖。 |
| `Invoke-SqliteContractTests.ps1 ... -Framework net8.0` | `1/1 PASS`；`artifacts/test-results/rs0026-review-round3-net8/` 下 Matrix 为两项 `TestGenerated`，`ReleaseReady=false`。 |
| `Invoke-SqliteContractTests.ps1 ... -Framework net6.0` | `1/1 PASS`；`artifacts/test-results/rs0026-review-round3-net6/` 下 Matrix 为两项 `TestGenerated`，`ReleaseReady=false`。 |
| `dotnet test Bing.Data.Sql.Tests ... SqlQueryApiContractTest\|TransactionApiContractTest` | `26/26 PASS`。 |
| `dotnet build framework/src/Bing.Data.Sql/Bing.Data.Sql.csproj -c Release --no-restore` | `0 warning / 0 error`。 |
| `dotnet test Bing.Test.Shared ... -f net8.0` | `70/70 PASS`。 |
| `dotnet test Bing.Test.Shared ... -f net6.0` | `70/70 PASS`。 |
| `git diff --check` | 通过。 |

## 问题分级

### BLOCKER

无。

### HIGH

无。

### MEDIUM

无。

### LOW

无。

## 未完成与环境边界

- MySQL、PostgreSQL、SQL Server 真实 Provider lane 仍需在具备专属安全连接、Provider gate 和 reset 授权的受保护环境中执行。未跟踪本机配置不能作为授权环境或发布证据。
- Oracle/Doris 没有授权 fixture，状态继续为 `NotExecuted` 或 `ImplementationGap`，符合批准计划的诚实状态要求。

## 最终验收 Checklist

- [x] 已读取批准计划、上一轮 review、当前 execution、源码、测试、CI、文档和矩阵制品。
- [x] `FIX-001` 可信 `ReleaseEvidence` 链已复审为解决。
- [x] `FIX-002` 逐场景能力矩阵和追溯已复审为解决。
- [x] `FIX-003` SQLite 受控合同已双 TFM 独立复跑并收口。
- [x] `FIX-004` optional parameter metadata 迁移说明已与 API 契约对齐。
- [x] `FIX-005` failed-TRX runner 自检已覆盖并独立执行。
- [x] `FIX-006` 跟踪运行设置已移除，本机/CI 配置源隔离完成。
- [x] 当前矩阵没有误报 `ReleaseReady` 或外部 Provider 真实执行。
- [x] `git diff --check` 通过。

结论：`PASS`。本 Reviewer 未执行 commit、push、PR 或任何业务/测试代码修改。
