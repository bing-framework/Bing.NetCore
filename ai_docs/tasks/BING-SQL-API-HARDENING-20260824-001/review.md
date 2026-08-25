<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: BING-SQL-API-HARDENING-20260824-001
AI_REVIEWED_AT: 2026-08-25T08:50:06.4976274+08:00

# 独立复审报告

## 验收摘要

最终结论：`PASS_WITH_ISSUES`。

上一轮两个 `MUST_FIX` 已完成并通过独立构建与测试验证。重复 `SqlTextQuery` 实现已删除，默认 Release Build、Analyzer、Data.Sql Unit、SQLite Unit 和 SQLite Integration 全部通过；关键移动文件均存在、非空并参与真实构建。未发现 Review Fix 引入新的 BLOCKER/HIGH 回归。

上一轮 `FIX-003` 为 `SHOULD_FIX`，本轮未处理且问题仍存在。原计划中的 Runtime Bridge 深度拆分、完整 Benchmark 前后基线、外部 Provider Integration 和部分测试职责拆分仍保持 `PARTIAL`，与 execution.md 的边界声明一致，不构成本轮 MUST_FIX。

## 上轮 FIX 复审

| FIX | 上轮要求 | 复审状态 | 独立证据 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | RESOLVED | `SqlTextQuery.NonGeneric.cs` 已删除；源码仅有一个 `SqlTextQuery` 定义；Data.Sql/Dapper Core 默认 Release Build 和全部指定测试通过。 |
| FIX-002 | MUST_FIX | RESOLVED | 10 个关键新增生产文件全部存在且非空，旧/新路径配对完整，并参与默认构建和测试。文件仍为未跟踪状态，最终提交时必须人工确保纳入；任务规则禁止 Reviewer/Executor 自动 `git add`，因此不继续作为代码修复阻断。 |
| FIX-003 | SHOULD_FIX | NOT_RESOLVED | 20/50 Join 仍调用 `AddRawJoinsThrough()`，直接使用 internal Builder；Benchmark 报告仍笼统表述为 20/50 Join 覆盖。 |

## 主要发现

### MEDIUM

1. 20/50 Join Benchmark 仍不能代表公开 Lambda Join API 的高元数构建成本。
   - 证据：`SqlLambdaJoinBenchmarks.BuildQuery()` 的 20/50 分支调用 `AddRawJoinsThrough()`；该方法直接执行 `query.GetBuilder().Join(...).AppendOn(...)`。
   - 影响：结果只能作为底层 Builder/渲染冒烟，不能作为公开 Lambda Join 的表达式解析、来源解析和原子提交性能结论。
   - 状态：上一轮 `FIX-003` 未处理，保持 `SHOULD_FIX`；当前 Benchmark 总体已明确标记 `PARTIAL`，不升级为 MUST_FIX。

### LOW

1. 关键归位文件和任务文档仍处于 Git 未跟踪状态。
   - 证据：`git status --short --untracked-files=all` 对 `SqlLambdaQuery.cs`、`SqlLambdaQueryCore.cs`、`Runtime/Abstractions/*`、`Runtime/Plans/*` 和任务文档返回 `??`。
   - 影响：本地 SDK 默认 glob 会编译这些文件，但最终提交或 PR 必须显式包含它们，否则提交内容无法重建当前工作区。
   - 裁决：修复执行器遵守禁止自动 `git add` 的操作边界，并已在 execution.md 给出完整交付清单；该风险转为提交前人工 Checklist，不再作为实现缺陷。

## 计划验收矩阵

| 计划范围 | 状态 | 复审证据 |
| --- | --- | --- |
| P1 动态编译与异常语义 | PASS | Analyzer 27/27；目标异常实现和直接测试保持通过。 |
| P2 非泛型多映射与 Advanced 删除 | PASS | 唯一 `SqlTextQuery` 实现；旧 Root/泛型描述负向契约通过；SQLite 多映射真实执行通过。 |
| P3-T01 查询文件归位 | PARTIAL | 重复文件问题已解决；Lambda 文件归位完成；计划中的测试大文件职责拆分仍未完成。 |
| P3-T02 Runtime Abstractions/Plans | PASS | 新文件均存在、非空，默认 Data.Sql 和 Dapper Core 构建证明真实接入；namespace 保持稳定。 |
| P3-T03 Runtime Bridge 深拆 | PARTIAL | execution.md 已明确未完成，本轮未扩大范围。 |
| P4 API/Unit/SQLite 门禁 | PASS | Analyzer 27、Data.Sql 2518、SQLite Unit 222、SQLite Integration 284，全部 0 failed。 |
| P4-T04 外部 Provider 合同 | PARTIAL | 外部数据库未授权执行，未伪装为通过。 |
| P5 Benchmark | PARTIAL | Dry 高元数冒烟存在；无正式前后基线，20/50 Join 代表性仍有限。 |
| P6 文档与迁移 | PASS_WITH_ISSUES | 主 API、迁移和 Release Notes 已同步；Benchmark 报告应进一步收窄 20/50 Join 描述。 |
| P7 Review Fix 回归 | PASS | 默认核心 Release Build、指定核心测试和 `git diff --check` 均通过。 |

## Git 变更分析

- `SqlTextQuery.NonGeneric.cs` 当前为删除状态，`SqlTextQuery.cs` 为唯一实现。
- 关键新增 Runtime/Lambda 文件共 10 个，均存在、非空并被 SDK 默认 Compile glob 编译。
- 关键移动配对核对结果：旧路径不存在、新路径存在。
- `git diff --check` 通过，仅有既有 CRLF/LF 转换警告。
- 当前工作区包含上一任务延续修改，无法仅按 HEAD 精确归属；本复审仅确认当前任务目标和 Review Fix 的真实状态。
- 未执行 `git add`、`commit`、`push`、reset、clean 或 PR 操作。

## 功能与 API Review

- `SqlAdvancedQueryExtensions`、Root 泛型入口和四类泛型查询描述未恢复。
- 非泛型 `SqlFluentQuery`、`SqlTextQuery` 的 2～7 同步/异步多映射继续通过 Analyzer 和 SQLite Integration。
- Dapper Core 能在默认 Release 配置下引用 Runtime SPI 并成功构建。
- 未发现 Review Fix 引入第二套实现、兼容入口或新的 public API 漂移。

## 架构与维护性 Review

- 删除重复 `.NonGeneric` 文件后，`SqlTextQuery` 文件名与主类型职责一致。
- Runtime SPI 物理拆分已真实接入，但 `SqlBuilderRuntimeBridge`/`SqlQueryBase` 深度职责拆分仍是原计划剩余项。
- `Runtime/Plans/ISqlOutputParameterAccessor.cs` 仍聚合访问器、快照和转换器，属于已知维护性问题；本轮修复未触及该范围。
- 未发现新增生产 `InternalsVisibleTo`。

## 性能与资源 Review

- 本轮 Review Fix 仅删除重复源文件和核对交付清单，没有运行时行为或性能路径改动。
- Benchmark Dry 证据仍不能形成正式性能结论。
- `SqlMultipleQueryResult.Dispose()` 的同步等待异步回调仍是原计划未完成的资源审计项。

## 测试 Review

复审实际执行：

- `dotnet build framework/src/Bing.Data.Sql/Bing.Data.Sql.csproj -c Release`：PASS。
- `dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj -c Release`：PASS。
- `Bing.Data.Sql.Analyzers.Tests`：27/27 PASS。
- `Bing.Data.Sql.Tests`：2518/2518 PASS。
- `Bing.Dapper.Sqlite.Tests`：222/222 PASS。
- `Bing.Dapper.Sqlite.Tests.Integration`：284/284 PASS。
- `git diff --check`：PASS。

未执行外部 MySQL/PostgreSQL/SQL Server/Oracle Integration，原因仍为缺少授权安全测试库和 Gate 配置。

## 文档 Review

- execution.md 已追加 Round 1 Review Fix 记录，两个 MUST_FIX 的根因、修改和验证结果可追溯。
- execution.md 顶部状态为 `COMPLETED`，表示本轮 Review Fix 完成；正文保留原计划整体 `PARTIAL` 说明，语义可以区分但阅读上存在轻微张力。
- Benchmark 报告仍应将 20/50 Join 明确描述为底层 Builder/渲染冒烟，而非公开 Lambda Join 性能基线。

## 剩余问题

### FIX-003

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 当前状态：`OPEN`
- 对应计划项：P5-T01、P5-T03、P7-T03
- 涉及文件/符号：`SqlLambdaJoinBenchmarks.BuildQuery()`、`AddRawJoinsThrough()`、`benchmark-report.md`
- 问题：20/50 Join 场景绕过公开 Lambda Join API，报告描述未充分区分测量边界。
- 证据：20/50 分支直接调用 internal Builder；2/5/10 分支使用公开 `Join<TLeft,TRight>()`。
- 影响：不能据此判断公开 Lambda Join 高元数构建性能。
- 修复目标：使 Benchmark 调用链、场景命名和报告结论一致。
- 明确修复要求：增加经过公开 API 的代表性场景，或明确重命名并将报告限定为底层 Builder/渲染冒烟。
- 修复后的验证方式：审阅调用链并运行对应 Dry 冒烟；正式性能结论仍需同机前后 Job。

## 回归与兼容风险

- Breaking Change 仍要求最终 NuGet/PR 交付包含所有未跟踪新增文件和迁移文档。
- RS0026/RS0027 可选参数重载警告仍待后续 API 设计评估。
- 外部 Provider Integration 未执行。
- 完整 Benchmark 矩阵和同机旧/新基线未完成。

## 最终验收 Checklist

- [x] 优先复审上一轮全部 FIX。
- [x] FIX-001 已独立验证为 RESOLVED。
- [x] FIX-002 已核对文件完整性并通过真实构建，判定 RESOLVED。
- [ ] FIX-003 已解决；当前仍为 SHOULD_FIX。
- [x] 默认 Data.Sql/Dapper Core Release Build 通过。
- [x] Analyzer、Data.Sql、SQLite Unit/Integration 通过。
- [x] `git diff --check` 通过。
- [x] 未发现新的 BLOCKER/HIGH 或修复回归。
- [x] 外部未验证项和原计划剩余项已明确保留。
- [ ] 最终提交已包含所有当前 `??` 新增文件；需在人工提交/PR 阶段确认。

当前 Review Fix 可以结束。剩余 SHOULD_FIX 和原计划 PARTIAL 项由后续迭代决定。
