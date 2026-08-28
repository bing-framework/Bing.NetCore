<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260827-003
AI_REVIEWED_AT: 2026-08-28T11:21:14.9943541+08:00

# Review Fix Round 3 复审报告

## 验收摘要

最终结论：`NEEDS_FIX`。

本轮独立复审基于当前 HEAD `3059a5971e0d3b52705a8d63eb077763e61d3a9d`、未暂存 Git Diff、[plan.md](plan.md)、当前 [execution.md](execution.md)、上一轮 [review.md](review.md)、实际源码和本轮命令结果完成。Round 2 没有处理 MUST_FIX 以外的内容，因此优先逐项复验上一轮所有 FIX；未发现与 Round 1 修复相关的 BLOCKER 或 HIGH 回归。

- `FIX-001`：`NOT_RESOLVED`。仍缺少真实 Provider non-skip 执行、受保护远端 CI、FormalHost before/after 与发布追溯的可审计证据。这是 SHOULD_FIX，故不能升为 `PASS_WITH_ISSUES`。
- `FIX-002`：`NOT_RESOLVED`。`IntegrationTestConnectionStringResolver.ProviderSettings` 的局部缩进与尾部空行仍未调整；维持 OPTIONAL。
- 上一轮已解决的 runsettings 自动加载隔离、本地兼容 fallback、SQL Server 多 Provider 路由、runner 合同和 AppVeyor 三条静态路径均仍成立，没有回归。

本轮没有读取、修改、清空或输出八个受保护用户配置内容。通过仅检查 Git 路径差异确认三个 Provider 的 `integration.runsettings`、三个 Provider 的 `appsettings.json` 和两个 Admin `appsettings.json` 均不在 Git Diff 中。

## 上轮 FIX 复验矩阵

| 上轮 FIX | 复审状态 | 当前证据 | 结论 |
| --- | --- | --- | --- |
| Round 1 FIX-001：配置隔离与本地兼容 | RESOLVED | 三个 Provider csproj 均无 `RunSettingsFilePath`；PostgreSQL/SQL Server 专属连接优先、`DefaultConnection` 本地回退和 SQL Server 多 Provider DI 注册均有直接测试。 | 默认门控 net8：MySQL 1 passed/54 skipped，PostgreSQL 3 passed/38 skipped，SQL Server 3 passed/5 skipped；外部 gate、reset 和连接变量在运行前清除。 |
| Round 1 FIX-002：Provider lane 合同与 CI 静态路由 | RESOLVED | runner 在预检首步拒绝 global gate，要求专属 gate、reset、专属连接与安全库名，并拒绝 DefaultConnection；AppVeyor 有 MySQL、PostgreSQL、SQL Server 三条明确 runner 调用路径。 | runner `-SelfTest` 通过；本轮三个 Provider 的安全占位 `-ValidateOnly` 均通过；DefaultConnection 反向预检按预期拒绝。未连接数据库。 |
| Round 1 FIX-003 / 上轮当前 FIX-001：外部 RC 证据闭环 | NOT_RESOLVED | 本任务目录仍仅有 plan/execution/review Markdown；未发现 current Provider non-skip TRX/JSON 或 FormalHost artifact。README、ReleaseNotes、治理和 traceability 文件也无任务关联记录。 | 维持 SHOULD_FIX。 |
| Round 1 FIX-004 / 上轮当前 FIX-002：Resolver 样式 | NOT_RESOLVED | `ProviderSettings` 构造函数仍有额外缩进，类尾仍有多余空行。 | 维持 OPTIONAL，不影响运行行为。 |

## 计划验收矩阵

| 计划项 | 状态 | 当前证据 |
| --- | --- | --- |
| RC27-P0-01 基线取证 | PARTIAL | execution.md 记录当前实现和本地验证摘要；计划要求的独立 progress、verification report、benchmark baseline 仍未见。 |
| RC27-P0-02 无密 runsettings | DEVIATED_OK | 原计划改写 runsettings 与用户配置保护要求冲突。当前通过移除自动加载隔离配置，八份受保护配置均未修改；凭据轮换和历史处置未验证。 |
| RC27-P0-03 Benchmark 可比较基线 | FAIL | FormalHost 类型存在，Dry smoke 仅证明可执行；未发现可比较的 current before/after、环境信息、case key、provenance 或 artifact hash。 |
| RC27-P1-01 gate/resolver/safety 测试 | PASS | `Bing.Test.Shared` net8.0/net6.0 各 51 passed，覆盖 global/provider OR、严格 `true`、非规范 PostgreSQL gate、连接优先级、安全库名与环境恢复。 |
| RC27-P1-02 Provider Startup 边界 | PASS | 默认路径不再自动加载用户 runsettings；PG/SQL Server 兼容 fallback 有直接测试；SQL Server 本地全局多 Provider 注册有直接 DI 测试。 |
| RC27-P1-03 核心回归 | PASS（沿用已审计证据） | 上轮已记录 Data.Sql net8/net6 各 1261 passed、Dapper Core net8 134 passed、SQLite integration net8 151 passed；本轮无相关代码改动。 |
| RC27-P2-01 Provider runner/TRX 门禁 | PASS（本地合同） | 本轮 self-test 通过；三个 Provider 安全占位预检均通过，DefaultConnection 反向路径被拒绝；真实 Provider TRX 尚不存在。 |
| RC27-P2-02 protected Provider CI lanes | PARTIAL | `appveyor.yml` 可静态定位三个 Provider runner 路径；无远端 secure variable、trusted-lane 边界或成功作业证据。 |
| RC27-P2-03 SDK/Linux | PARTIAL | 当前变更包含 SDK/Linux 配置更新；未取得远端 Linux 成功记录或 required-check 证据。 |
| RC27-P3 Provider 功能矩阵 | BLOCKED | 未获授权安全 MySQL/PostgreSQL/SQL Server 实例，未生成 current non-skip TRX；默认 Skip 不能视为通过。 |
| RC27-P5 Benchmark 边界 | PARTIAL | Raw 来源构造与渲染、诊断订阅与稳态查询已拆分；没有 FormalHost 对比，不能产生性能结论。 |
| RC27-P6 文档/发布验收 | PARTIAL | 两份集成测试文档与当前 gate、连接优先级、runner 路径一致；README、ReleaseNotes、治理和最终 traceability 未按计划闭环。 |

## Git、功能与契约 Review

当前 Diff 覆盖 CI、共享门控、三个 Provider 集成项目、PostgreSQL/SQL Server Startup、benchmark 和集成测试文档，整体属于任务范围。未见新增 SQL public API、生产 `InternalsVisibleTo`、第二套 Provider runner 或绕过数据库安全预检的路径。

三个 Provider csproj 已移除 `RunSettingsFilePath`，默认 `dotnet test` 不会自动导入用户 `integration.runsettings`。PostgreSQL 和 SQL Server Startup 均保持 Provider 专属键优先、`DefaultConnection` 本地兼容回退；受保护 runner 则在调用测试前拒绝 default fallback。此分层与两份集成测试文档一致。

SQL Server 仅在显式 `RUN_INTEGRATION_TESTS=true` 的本地兼容模式注册 MySQL、PostgreSQL 和 SQL Server 具名数据源。受保护 runner 的首个预检即拒绝该 global gate，故 Provider-specific lane 不会误走本地多 Provider 路径。

## 性能、测试与文档 Review

本轮实际执行：

| 验证 | 结果 |
| --- | --- |
| `git diff --check` | PASS。 |
| 八个受保护配置路径的 `git diff --quiet` | PASS；没有读取其内容。 |
| `Invoke-ProviderIntegrationTests.ps1 -SelfTest` | PASS。 |
| `Bing.Test.Shared` net8.0/net6.0 | 各 51 passed。 |
| MySQL 默认门控 net8.0 | 1 passed，54 skipped。 |
| PostgreSQL 默认门控 net8.0 | 3 passed，38 skipped。 |
| SQL Server 默认门控 net8.0 | 3 passed，5 skipped。 |
| MySQL/PostgreSQL/SQL Server `-ValidateOnly` | 安全占位连接下均通过预检；未执行数据库连接。 |
| PostgreSQL `DefaultConnection` 反向预检 | 按预期拒绝。 |

所有命令均在 `finally` 中恢复原始环境变量，未执行外部数据库 DDL、DML、reset 或真实 Provider 测试。默认 Provider 项目的 Skip 是门控行为证据，不是 Provider 执行通过证据。

两份集成测试文档准确区分了本地显式 settings、Provider 专属连接优先、受保护 CI 禁止 DefaultConnection、PostgreSQL 规范 gate 与 runner 的 TRX 规则。FormalHost benchmark 类型仍需在相同环境形成完整可比制品；Dry job 不能替代性能基线。

`review.md` 的唯一诊断是 MD041，原因是工作流强制三行机器元数据位于 H1 之前；这是预期且不应通过改变元数据协议消除。受审 C# 源码没有诊断错误。

## 问题清单

### MEDIUM

FIX-001

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 当前状态：OPEN
- 对应计划项：RC27-P0-01、RC27-P0-03、RC27-P2-02、RC27-P2-03、RC27-P3-01、RC27-P5-02、RC27-P6-01。
- 涉及范围：受保护 Provider 环境/CI、Provider TRX/JSON artifacts、FormalHost artifacts、README、ReleaseNotes、SQL 治理与 traceability 文档。
- 问题：RC 计划要求的 current Provider non-skip 执行、远端受保护 CI、FormalHost 可比较基线和发布/追溯资料没有可审计证据。
- 证据：本任务目录没有 current Provider TRX/JSON 或 FormalHost artifact；AppVeyor/Travis 仅为静态配置；README、ReleaseNotes、治理及 traceability 文件没有本任务记录。
- 影响：不能宣布三个 Provider RC 已实际通过、Linux CI 已成功或性能无回归；发布追溯未闭环。
- 修复目标：在受信且无密的环境中补齐 Provider、CI、benchmark 和文档追溯证据，并严格区分 Dry/Skip 与真实执行。
- 明确修复要求：维护者提供每个 Provider 独立安全测试库、专属 gate/连接/reset 授权和受保护 CI 配置后，运行 runner 的 net8.0 及代表性 net6.0，保留脱敏 TRX/JSON。以相同 SDK/runtime/GC/Job/case key 运行 current FormalHost before/after 并记录 artifact hash/provenance。同步 README、ReleaseNotes、治理和生产符号到直接测试方法的 traceability；不得将 Dry/Skip 记作 Provider 实跑通过。
- 修复后的验证方式：每 Provider 至少一份 current non-skip TRX 和 JSON 摘要；远端 AppVeyor/Linux job 链接或等价无密 artifact 可审计；FormalHost artifacts 能按完整 case key 比较；文档命令和变量与源码一致。

### LOW

FIX-002

- 严重程度：LOW
- 处理要求：OPTIONAL
- 当前状态：OPEN
- 对应计划项：RC27-P1-01。
- 涉及文件/符号：`framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestConnectionStringResolver.cs` 的 `ProviderSettings`。
- 问题：构造函数缩进异常，类尾部留有多余空行。
- 证据：当前源码；`git diff --check` 不报告该样式问题。
- 影响：仅可读性，不改变连接解析或安全边界。
- 修复目标：最小格式调整，不夹带无关重排。
- 明确修复要求：修正局部缩进和空行，保持既有行为。
- 修复后的验证方式：`Bing.Test.Shared` net8.0/net6.0 与 `git diff --check` 通过。

## 未完成、偏离与回归风险

- 原计划的已跟踪无密 runsettings 方案因用户明确的配置保护要求改为禁止自动加载用户 runsettings；该偏离已验证，不得通过清空或改写用户配置处理。
- 真实 Provider 运行、远端 CI、secret/branch protection、FormalHost 与发布追溯超出本地权限，仍应保持 BLOCKED/PARTIAL，不能由 runner 自检或默认 Skip 替代。
- runner 与 `IntegrationDatabaseSafetyValidator` 分别维护测试库名称规则；当前合同测试覆盖关键边界，后续变更仍需注意两者漂移。
- 本复审没有修改业务代码、测试代码、计划、执行报告、受保护配置、Git 提交或数据库；仅更新本 `review.md`。

## 最终验收 Checklist

- [x] 已读取 plan.md、当前 execution.md、上一轮 review.md、项目规则、当前 Diff 和实际代码。
- [x] 已优先逐项复验上一轮 FIX，并确认已解决 MUST_FIX 未回归。
- [x] 已验证 runsettings 自动加载隔离、本地连接兼容、SQL Server 多 Provider DI、runner 自检/预检与 AppVeyor 静态路由。
- [x] 已运行共享 gate、默认 Provider 门控、runner 自检与安全预检合同。
- [x] 已确认八个受保护配置未修改，且 `git diff --check` 通过。
- [ ] 三个 Provider 的 current non-skip TRX/JSON 和受保护远端 CI 证据。
- [ ] current FormalHost before/after、provenance 与 artifact hash。
- [ ] README、ReleaseNotes、治理及最终 traceability 的计划闭环。

存在未解决的 SHOULD_FIX，因此最终状态为 `NEEDS_FIX`。
