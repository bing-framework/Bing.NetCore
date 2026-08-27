<!-- AI_REVIEW_STATUS: BLOCKED -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260826-002
AI_REVIEWED_AT: 2026-08-26T22:27:38.2074298+08:00

# Review Fix Round 8 独立复审报告

## 验收摘要

最终结论：`BLOCKED`。

Round 8 没有新增业务或测试实现，执行器完成了两项证据收口：按 AppVeyor 当前配置运行本地等价快速/E2E smoke，并审计历史 `before` 目录。Reviewer 独立核对确认快速 smoke 为 1 个 Dry case，SQLite/Dapper E2E 为 42 个唯一 Dry case；Benchmark build、Data.Sql `1261/1261`、Analyzer `30/30` 和 `git diff --check` 通过。历史 Root/Join 报告使用旧 72/36 参数矩阵，缺少旧源码工作树、dirty diff、源码 hash 和独立构建 provenance，不能作为当前 before。

当前没有可由本地代码修改继续完成的开放问题。剩余验收分别依赖旧源码身份或明确发布决策、AppVeyor 远程权限/作业，以及外部数据库 Gate、专用连接和重置授权。继续重复 `/fix-review` 不能产生新的有效证据，因此本轮从 `NEEDS_FIX` 调整为 `BLOCKED`。

## 上一轮 FIX 复审

| FIX | 复审状态 | 结论 |
| --- | --- | --- |
| `FIX-003` | `RESOLVED` | Join API、schema SQL、裸 `null` 和旧 API 负向合同保持有效；Analyzer/Data.Sql 回归通过。 |
| `FIX-004` | `BLOCKED` | 本地 E2E 正确性和历史 before 审计已完成；有效 FormalHost before/after 需要旧源码身份或发布接受决策。 |
| `FIX-005` | `BLOCKED` | 本地 CI 等价执行已完成；远程 AppVeyor 制品和外部 Provider 需要外部权限及安全环境。 |

## 阻断项

### BLOCKED-001 - FormalHost before/after 缺少可审计旧源码身份

- 对应原 FIX：`FIX-004`
- 对应计划项：`RC26-P0-03`、`RC26-P4-03`、`RC26-P5-03`
- 当前完成：Benchmark 矩阵已重构；IN 和 SQLite/Dapper E2E 已覆盖；当前源码 Dry/FormalHost 单版本结果和制品 hash 已记录；历史 before 目录已审计。
- 证据：`review-fix-round3-before-root` 为旧 RootCount × ParameterCount 的 72-case 矩阵；`review-fix-round3-before-join` 为旧 `JoinCount` 36-case 矩阵。二者缺少旧源码工作树、dirty diff hash、benchmark source hash 和与当前矩阵匹配的独立构建 provenance。
- 阻断原因：仓库与当前工作区没有可恢复、可审计且与当前代表矩阵匹配的旧源码身份；Reviewer 和 Executor 均不能从不完整历史 artifact 反推源码。
- 解除条件：提供可审计旧源码 commit/worktree/patch 及其源码 hash，允许使用相同 FormalHost/Params/Runtime 独立构建并运行 before；或由发布负责人明确接受“本 RC 不具备 before/after 性能准入”，并禁止声明性能收益或无回归。
- 解除后验证：before/after case key 完全匹配，独立构建和 artifact hash 完整，无 process failure、意外 NA 或中止；或提供可审计的发布接受决策。

### BLOCKED-002 - 远程 CI 与外部 Provider 环境不可用

- 对应原 FIX：`FIX-005`
- 对应计划项：`RC26-P3-02`、`RC26-P3-03`、`RC26-P6-03`
- 当前完成：AppVeyor 已使用 Visual Studio 2022 和 SDK 10.0.300，配置 build/test、TRX、Cobertura、PublicAPI、快速 Dry smoke、42-case E2E Dry smoke及独立 artifact 路径；本地等价执行通过。
- 证据：快速 CSV 1 行、Job=`Dry`，hash=`1DB4917ECCF9E2249F3197E4F6B84CE03310B2F51559B5DDAB015AC3C0E918AD`；E2E CSV 42 行、14 方法、`RowCount=1/100/1000`、Job=`Dry`、无重复键，hash=`E0BB529CD3D19222EF2409BF842D7BA0894FCEF14396B3EFC9876C412710D572`。
- 阻断原因：当前环境没有 AppVeyor 远程 job 权限/URL/日志/下载制品；也没有 `RUN_*_INTEGRATION_TESTS`、`ConnectionStrings__*`、`ALLOW_DATABASE_RESET_FOR_TESTS`。
- 解除条件：提供可执行 AppVeyor 或等价远程 CI 权限并运行一次；为需要验证的 Provider 提供专用测试库、对应 Gate 和重置授权。所有秘密只能由 CI/终端安全注入，不得通过聊天传递。
- 解除后验证：远程作业成功；TRX、Cobertura、PublicAPI、快速 smoke 和 E2E smoke 制品可下载；Provider 逐项记录 passed 或具体 blocked，日志不泄露连接信息。

## 计划逐项验收矩阵

| Phase | 结论 | 当前证据 |
| --- | --- | --- |
| Phase 0 基线与消费者矩阵 | `PASS` | 历史无效 FormalHost 已隔离，当前源码、hash 和阻断边界明确。 |
| Phase 1 Fluent cache 与多结果集 | `PASS` | 前轮职责测试通过；Round 8 未修改相关生产逻辑，核心回归无退化。 |
| Phase 2 Breaking API 与 Runtime SPI | `PASS` | API 收敛保持有效，Analyzer `30/30`、Data.Sql `1261/1261`。 |
| Phase 3 测试与 CI | `PARTIAL` | 本地单元/Analyzer/SQLite E2E 与 CI 等价 smoke 通过；远程 CI 和外部 Provider blocked。 |
| Phase 4 Benchmark | `PARTIAL` | 矩阵与当前 E2E 证据有效；独立 FormalHost before/after blocked。 |
| Phase 5 性能优化 | `NOT_VERIFIABLE` | 无有效 before，未进行或声明数据驱动优化收益。 |
| Phase 6 发布准备 | `PARTIAL` | 文档、追溯、SDK pin 和 CI 配置已同步；发布性能决策与远程制品 blocked。 |

## Git 变更分析

- 当前工作树改动集中在计划范围内的 Data.Sql、Dapper Core、Provider/SQLite 测试、Analyzer、Benchmark、CI、文档和任务过程文件。
- Round 8 仅更新过程报告并生成 Benchmark 制品；没有新增业务行为、测试实现或公共 API。
- 未发现恢复高层 `FromTable`、高层 `ClearSelect`、旧多 string Join、`.As<TResult>()` 或新增 production IVT。
- `execution.md` 为合法 `PARTIAL` 终态，与外部阻断一致；Reviewer 已读取其最新内容。
- `git diff --check` 通过，仅有 CRLF/LF 转换提示；Reviewer 未修改业务代码、测试代码、`plan.md` 或 `execution.md`。

## 功能、API 与架构 Review

- 本轮没有新增生产代码，因此未引入新的行为回归、重复实现或公开 API。
- `SqliteDapperE2EBenchmarkBase` 的 GlobalSetup validation 继续强制验证 14 条路径；快速/E2E smoke 入口仍通过独立 `[DryJob]` 类型限定。
- AppVeyor 的普通链显式关闭外部 Provider Gate，避免无凭据环境误连数据库；外部 Provider 应由受保护作业启用。
- 当前本地实现与配置层目标已经完成，剩余项无法通过继续修改本地代码替代。

## 性能与资源 Review

- Round 8 E2E Dry 报告为单次迭代，`Error=NA` 属于统计口径限制，只能证明当前版本可执行性和路径契约。
- `QueryMultipleDisposeEarly` 的某个 Dry 单样本约 25 ms，但 N=1，不能据此判定回归；FormalHost before/after 不存在，因此不作性能结论。
- 历史 Root/Join FormalHost 报告虽有完整数据行，但参数矩阵和 provenance 与当前目标不匹配；将其用于 delta 会违反计划明确约束。
- 当前过程报告未宣称性能收益、0 GC 或 RC 性能准入，表述正确。

## 独立验证

| 项目 | 结果 |
| --- | --- |
| `Bing.Data.Sql.Benchmarks` Release build | PASS，0 errors |
| Round 8 快速 CI 等价制品 | PASS，1 个 Dry case，无重复键 |
| Round 8 E2E CI 等价制品 | PASS，42 个 Dry case，14 方法 × 3 RowCount，无重复键 |
| `Bing.Data.Sql.Tests` net8.0 | PASS，1261 passed，0 failed，0 skipped |
| `Bing.Data.Sql.Analyzers.Tests` net8.0 | PASS，30 passed，0 failed，0 skipped |
| 外部 Provider Gate 环境 | 未配置，BLOCKED |
| AppVeyor 远程作业 | 无权限/作业证据，BLOCKED |
| `git diff --check` | PASS，仅换行转换提示 |

本轮未重复执行 Dapper Core、SQLite Unit/Integration 和完整解决方案测试；Round 8 未修改对应生产逻辑，前序直接验证仍适用。

## 文档与追溯 Review

- `benchmark-baseline.md` 已明确历史 before 无效的具体原因，不再仅以“未找到”概括。
- `verification-report.md`、`progress.md` 和 `execution.md` 已记录 Round 8 本地等价 CI 结果、hash、环境 Gate 缺失及远程阻断。
- 报告没有将本地等价命令描述为 AppVeyor 远程成功，也没有将未配置 Provider 计为 pass。

## 回归与兼容风险

- 当前最大残余风险是未取得跨 Provider 真实执行和性能 before/after，而非已发现的本地功能缺陷。
- Join Options 属 Breaking API；现有消费者和编译合同已迁移，但仓库外消费者仍需按 ReleaseNotes 更新。
- 当前工作树较大且未提交，后续操作应继续避免 reset、clean、checkout 或覆盖用户改动。

## 最终验收 Checklist

- [x] 已读取当前 plan、最新 execution、上一轮 review、过程报告、源码、Git Diff、Benchmark 和 CI 配置。
- [x] 已复审上一轮 `FIX-003`、`FIX-004`、`FIX-005`。
- [x] 最新 SQLite/Dapper E2E 与 CI 等价 smoke 证据可复核。
- [x] 历史 before artifact 已完成来源和矩阵有效性裁决。
- [x] Benchmark build、Data.Sql/Analyzer 回归和 `git diff --check` 通过。
- [ ] 已提供可审计旧源码或明确发布接受决策。
- [ ] 已提供 AppVeyor 远程作业/制品权限和外部 Provider 安全环境。

复审结论为 `BLOCKED`。解除上述外部阻断后再进行 Review；不要在条件未变化时重复执行 `/fix-review`。
