<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261022-slnx-migration
AI_REVIEWED_AT: 2026-10-22T03:59:26Z

# 独立代码审查

## 结论

Round 2 已独立核对 Round 1 的 3 项修复，均有真实改动和本地验证证据，未发现新的 MUST_FIX/SHOULD_FIX，审查状态为 `PASS_WITH_ISSUES`。该状态只表示本任务可修实现问题已经关闭：主方案 Release 测试仍为 exit 1、181 failed、869 NotExecuted；远端 CI 和 IDE 实际打开流程未验证；旧 SolutionGuid 未由 SLNX 表示。发布状态仍为 `BLOCKED`。

| 状态维度 | 判定 | 依据 |
| --- | --- | --- |
| IMPLEMENTATION_STATUS | PASS | `FIX-001` 至 `FIX-003` 均已复核为 `CLOSED`；当前 `OPEN_ACTIONABLE=0`。 |
| TEST_STATUS | PARTIAL | 新方案 Debug/Release 构建及 Bing.Test.Shared、SQLite 测试通过；主方案 Release 测试 181 失败。 |
| PERFORMANCE_EVIDENCE_STATUS | NOT_APPLICABLE | 计划排除性能测试，未更改预算或热路径。 |
| RESOURCE_EVIDENCE_STATUS | NOT_APPLICABLE | 本次无资源门槛。 |
| EXTERNAL_GATE_STATUS | BLOCKED_EXTERNAL | 远端 DocFX、Travis、AppVeyor job 未运行。 |
| RELEASE_STATUS | BLOCKED | 主测试非绿色，远端 CI 与 IDE 工作流未验证。 |
| GOAL_STATUS | STOPPED_NO_ACTIONABLE | 本轮不因 `PARTIAL` 或 `BLOCKED_EXTERNAL` 再次进入自动修复。 |

## 计划验收

| 计划项 | 判定 | 审查依据 |
| --- | --- | --- |
| T01 保存迁移起点 | PASS | `artifacts/baseline/t01-manifest.json` 记录 23 个初始文件及后续兼容改动前快照；旧 `Bing.All.sln` 快照 SHA-256 与实际文件一致。快照包含相对 HEAD 新增的 Sample 项。SDK 8 旧方案 Release 构建 exit 0。暂存区为空。 |
| T02 统一构建 SDK | PASS | `global.json` 精确设置 10.0.400/latestPatch/allowPrerelease=false；SDK 10 旧方案 Release 构建 exit 0，Reverse 显式 LINQ 绑定保持原意。安装脚本的 SDK、net6/net8 Core 和 ASP.NET Core runtime 参数符合[微软脚本说明](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)，各步骤检查 PATH 和安装根；Travis 已改为受支持的 Jammy。CI 仅通过本地语法静态检查，远端仍未验证。 |
| T03 官方转换并核对语义 | DEVIATED_OK | `dotnet sln Bing.All.sln migrate` exit 0；比较器与 JSON 表明 159 项目、63 文件夹、28 Solution Items、无显式依赖差异，Debug/Release 映射与 Build 标记等价。SLNX 省略默认配置值有明确归一化。旧 SolutionGuid 未由官方序列化器表示，属于 IDE/扩展身份元数据差异，仓内搜索未发现消费者；不能宣称元数据零差异。 |
| T04 切换入口与兼容引用 | PASS | 新方案 list/restore/Debug/Release 构建后才删除旧 `.sln`；根目录裸 Release 构建 exit 0；CodeSmith 独立 `.sln` 未见改动；根定位保留 `.slnx` 与 `.sln` 检测，net6/net8 各 91/91 通过。当前文档的 SDK 及 Payments 归属错误已修复。 |
| 计划内验证 | PARTIAL | 主方案 Release 测试 exit 1；12,542 passed、181 failed、869 NotExecuted，总数 13,592。SQLite 集成每个 TFM 231 passed/1 skip，样例每个 TFM 12 passed/15 skip；skip 未计入 passed。Round 2 静态 CI、文档索引检查与 `git diff --check` exit 0。 |

## Round 2 复核

| Round 1 finding | Round 2 状态 | 独立复核 |
| --- | --- | --- |
| FIX-001 | CLOSED | `.travis.yml:3` 为 `dist: jammy`；本轮重新执行 `python ai_docs/tasks/20261022-slnx-migration/artifacts/validate_ci_config.py`，3 份 YAML、DocFX/Travis Bash、AppVeyor PowerShell AST 全部 exit 0。未执行 hosted job，故只关闭本地可修配置问题。 |
| FIX-002 | CLOSED | `samples/README.md:46`、`docs/getting-started/API速查索引.md:98`、`docs/migrations/README.md:105` 均写 10.0.400，与 `global.json` 一致。限定扫描仅找到历史计划、历史对照及旧迁移执行中的 SDK 8/旧 `.sln` 记录；本轮 `python eng/ci/sync-docs-index.py --check` exit 0。 |
| FIX-003 | CLOSED | `CONTRIBUTING.md:45` 明确 Payments 已纳入主方案，定向运行条件也准确；`Bing.All.slnx:189` 包含其项目路径，`slnx-release-trx-summary.json` 中 net6/net8 各 11 total、10 passed、0 failed、1 NotExecuted。 |

本轮 `git diff --check` exit 0；仅出现此前未相关报告的 CRLF 转换提示。`git diff --cached --quiet` exit 0，暂存区仍为空。`execution.md` 已记录 Round 1 修复命令及结果，并保留主测试 `PARTIAL`、远端 CI 未运行、SolutionGuid 差异和 SDK 8 对照的适用边界。未因文档与 CI 配置修复重跑全方案测试。

下列 FIX 条目保留 Round 1 的原始问题、影响和验收要求；当前状态以 Round 2 的 `CLOSED` 和上表复核证据为准。

## FIX-001：Travis 运行环境仍为 Ubuntu 20.04

- 严重程度：HIGH
- 处理要求：MUST_FIX
- Finding 状态：CLOSED（Round 2）
- 问题：`.travis.yml:3` 保留 `dist: focal`，但新脚本在此环境安装并运行 SDK 10.0.400。微软 [.NET/Ubuntu 支持矩阵](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-decision) 对 .NET 10 列出 Ubuntu 22.04 及更新版本，未列 20.04；Travis [Jammy 镜像文档](https://docs.travis-ci.com/user/reference/jammy/) 明确支持 `dist: jammy`。
- 证据：`.travis.yml:3,14-44`；`artifacts/logs/ci-static-validation-final.log` 只验证 YAML 和 Bash 语法，未执行 hosted runner。安装根与 PATH 检查无法修复操作系统依赖不受支持的问题。
- 影响：Travis 的 SDK 10 job 可能在安装后首次执行 `dotnet` 时失败，工具链统一目标无法验收。
- 修复目标：将该 lane 切换到受 .NET 10 支持的 Travis Linux 发行版，同时保持现有命令参数、测试范围与运行时安装。
- 修复要求：优先选 Travis 已提供的 `jammy`；静态核对 YAML 与 Bash，并在执行报告中维持远端 job 未验证的事实。
- 验证方式：配置中不再固定 Focal；本地 YAML/Bash 静态检查通过；远端实际结果仍记 `BLOCKED_EXTERNAL`，直到独立 job 运行。

## FIX-002：当前使用文档仍声称 SDK 8.0.424

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- Finding 状态：CLOSED（Round 2）
- 问题：`samples/README.md:46`、`docs/getting-started/API速查索引.md:98`、`docs/migrations/README.md:105` 均把 `global.json` 的当前锁定版本写为 8.0.424，实际 `global.json` 为 10.0.400。
- 证据：这些段落使用“当前”“由 global.json 固定”等现时措辞；历史 `docs/plans/`、既有迁移执行日志及 baseline 的旧 SDK 记录不属于修复范围。
- 影响：开发者按现行说明准备错误的 SDK，命令在仓库根目录会被 `global.json` 拒绝或选择不同工具链。
- 修复目标：现行环境说明与 `global.json` 一致，历史验证记录保持原样。
- 修复要求：仅更新这 3 处当前事实；保留各文件其他未提交内容，并按既有 README 派生流程检查 `docs/index.md`。
- 验证方式：对现行文档做限定范围引用扫描；运行文档索引一致性检查和 `git diff --check`。

## FIX-003：贡献指南误报 Payments 测试项目在主方案外

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- Finding 状态：CLOSED（Round 2）
- 问题：`CONTRIBUTING.md:45` 在迁移中改为“`Bing.Biz.Payments.Tests` 独立于 `Bing.All.slnx`，需单独跑”，但 `Bing.All.slnx:189` 明确包含该测试项目；主方案测试 TRX 也记录其 net6/net8 测试。
- 证据：新方案原文、`artifacts/logs/slnx-release-trx-summary.json` 中 `Bing.Biz.Payments.Tests.dll` 各 TFM 11 项。该错误在旧文档中已存在，但迁移后的当前使用说明仍错误。
- 影响：贡献者误判主方案测试覆盖范围，额外独立运行并对全套结果形成错误理解。
- 修复目标：贡献指南准确说明项目已纳入主方案，以及何时需要单独运行该项目。
- 修复要求：只修正该条当前事实，不改变既有项目或方案清单。
- 验证方式：按 `Bing.All.slnx` 项目路径和 TRX 核对说明，再执行文档检查与 `git diff --check`。

## 非修复项与证据边界

- 主套件的 154 个 MySQL/SQL Server 连接失败是真实失败，不是 skip。关闭外部门控变量后这些既有测试仍尝试连接；不在本迁移任务中静默关闭或修改其业务测试。连接未完成可用数据库会话，但 SQL Server 错误明确到达 pre-login 阶段，不应写成完全未接触主机。状态 `BLOCKED_EXTERNAL`，发布门槛不能记 PASS。
- Logging Serilog 集成各 TFM 12 个失败在 SDK 8 探针中复现；该探针使用 SDK 10 已编译程序集且 `--no-build`，只证明 SDK 8 选用下的同一二进制仍失败，不构成 SDK 8 编译产物对照。AOP 2 项与 EventBus 1 项的 SDK 8/10 精准复测均通过，主套件失败原因尚未确定。当前“无新增失败”只能维持部分证据结论。
- SolutionGuid `{C1202A0F-83CC-4602-BCE5-20CB640BCAD4}` 缺失已在比较结果与 execution.md 披露。仓内无消费者，构建语义已比较；外部 IDE/扩展身份依赖仍是未验证限制。`HideSolutionNode=FALSE` 与新方案默认显示行为等价。
- SDK 8 旧 `.sln` Release 构建、SDK 10 旧 `.sln` Release 构建与新 `.slnx` Debug/Release 构建均有命令、退出码和日志。旧方案删除发生在语义比较及新方案构建通过后，工作区原始 `.sln` 快照保留。`git diff --cached --quiet` exit 0；未见 CodeSmith 方案或既有 F008/Uow/CAP/Sample 内容被回退。
- CI 的 YAML、DocFX/Travis Bash、AppVeyor PowerShell AST 静态检查 exit 0。AppVeyor 使用 `Set-AppveyorBuildVariable` 传递 `DOTNET_ROOT` 与 `PATH`，并在后续阶段检查，符合其[构建变量接口](https://www.appveyor.com/docs/build-worker-api/)；真实 hosted runner 的下载安装与跨步骤行为仍需远端证据。

## 主要证据

- `artifacts/baseline/t01-manifest.json`、`worktree_snapshot/Bing.All.sln`、`Bing.All.sln.semantics.json`、`t03-sln-vs-slnx-semantic-comparison.json`
- `artifacts/logs/sdk8-baseline-sln-release-build-confirmation2.json`、`sdk10-old-sln-release-unlocked-after-compat-fixes.json`、`slnx-restore.json`、`slnx-debug-build-m1-retry.json`、`slnx-release-build-m1.json`、`root-bare-release-build.json`
- `artifacts/logs/shared-net6-post-delete.json`、`shared-net8-post-delete.json`、`slnx-release-tests.json`、`slnx-release-trx-summary.json`、`slnx-release-test-failure-summary.log` 及 `artifacts/test-results/` 原始 TRX
- `artifacts/logs/ci-static-validation-final.log`、`docs-index-check-post-delete.json`、`reference-search-post-delete.json`、`git-diff-check-final.json`
