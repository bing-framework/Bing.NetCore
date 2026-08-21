<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: sql-lambda-query-api-v4
AI_REVIEWED_AT: 2026-08-21T01:47:28.8055015Z

# Round 7 独立复审报告

## 1. 验收摘要

最终结论：**PASS**。

本轮按复审规则优先验证上一轮 FIX。`FIX-011` 的实体类型化 Join 第三方参数管理器原子提交仍保持有效；`FIX-012` 已将类型化派生表 Join 的子查询参数渲染、参数冲突重命名、On 谓词解析和失败恢复统一到候选状态，成功后一次提交。普通与增强第三方参数管理器专项测试覆盖了已有参数编号、至少两个待提交参数、失败后完整状态不变和同一 Builder 按原编号重试。

未发现新的 BLOCKER/HIGH、与本轮修复直接相关的回归或需要新增的 MUST_FIX。外部 MySQL/PostgreSQL/SQL Server/Oracle 数据库未运行，继续保持 `NOT_RUN_EXTERNAL_GATE_MISSING`，不影响本地 Review 结论。

## 2. 上一轮 FIX 验收矩阵

| FIX | 结果 | 实际证据 |
| --- | --- | --- |
| FIX-011 | `RESOLVED` | 实体 `Join<TEntity>` 在候选参数管理器上完成谓词解析，通过 `ReplaceParameterManager()` 一次替换共享状态；失败只恢复旧引用，不对第三方管理器执行 `Clear/Add`。普通/增强管理器故障、提交后故障和重试测试仍存在；SQL Core 全量 `2394/2394` 通过。 |
| FIX-012 | `RESOLVED` | 类型化派生表 `Join<TProjection>` 使用候选参数管理器和候选 `_subqueryParameterNames`；`RenderSubquery`、谓词解析和重命名均写入候选状态，成功后一次替换参数与映射。本轮专项双 TFM `14/14` 通过，SQL Core 全量 `2394/2394` 通过。 |

## 3. Review 边界与 Git 变更

- 计划范围覆盖 Lambda API 收敛、From/Join 原子状态、Runtime API 治理、1～10 来源测试、SQLite 执行、Provider 验证、Benchmark 和文档；当前 diff 中的相关源码、测试、文档和生成产物均与该计划或既有 FIX 记录相符。
- 本轮 FIX-012 相关生产差异集中在 `SqlBuilderBase` 和 `JoinClause`；测试差异集中在 `SqlBuilderTest.Join.cs`。未发现新增公共 API 或 Public API Analyzer 基线绕过。
- 当前已跟踪差异：`36 files changed, 2143 insertions(+), 2002 deletions(-)`；另有计划文档、生成 arity 文件、Benchmark 和工具等未跟踪任务产物，属于当前任务工作区产物。
- 未执行 `git add`、commit、push、reset、clean 或 PR。
- `git diff --check` 通过；输出的 CRLF/LF 信息是 Git 换行提示，不是 whitespace error。

## 4. 计划逐项验收

| Phase | 结论 | 实际证据 |
| --- | --- | --- |
| Phase 0 基线 | `PASS` | execution.md 保留了基线、回归、Git 和外部 Gate 记录；本轮独立验证补充了当前专项和 SQL Core 结果。 |
| Phase 1 结果物化 API | `PASS` | API Contract、Analyzer、Dapper Core 证据保留，未发现本轮回归；未新增兼容转发或公共结果类型入口。 |
| Phase 2 From/Join 原子状态图 | `PASS` | 实体和派生表 Join 均在候选参数状态完成解析与渲染，成功后一次提交；失败恢复参数、映射、Alias、Operation、Join 和来源图。 |
| Phase 3 Runtime 公共 API 治理 | `PASS` | execution.md 记录了跨程序集 SPI 和内部化 Runtime Bridge；本轮无相关回归。 |
| Phase 4 高元数生成 | `PASS` | 2～10 arity 生成产物和生成器保留，10 元上限及生成稳定性证据未受本轮修改影响。 |
| Phase 5 1～10 成功 Unit | `PASS` | SQL Core、SQLite Unit 和既有 arity SQL/参数矩阵证据通过。 |
| Phase 6 原子失败 Unit | `PASS` | FIX-011/FIX-012 均有普通/增强第三方管理器失败测试；派生表专项独立复跑 `8/8` 通过。 |
| Phase 7 SQLite 真实执行 | `PASS` | execution.md 记录 SQLite Integration `266/266`；本轮未修改 SQLite 执行链。 |
| Phase 8 Provider 验证 | `PASS` | Custom Provider `38/38`；SQLite、MySQL、PostgreSQL、SQL Server、Oracle Unit 既有证据通过；外部 Gate 诚实记录缺失。 |
| Phase 9 Benchmark | `PASS` | Benchmark 产物和同环境基线记录保留；本轮未引入性能优化或改变 Benchmark 合同。 |
| Phase 10 文档与验收 | `PASS` | execution.md 已记录 Round 4 FIX-012、最终构建警告和 diff 统计；review.md 本轮结论为 PASS。 |

## 5. 功能与契约 Review

### 5.1 FIX-012 主流程

- [SqlBuilderBase.cs](../../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs) 的候选渲染入口接收目标参数管理器和目标重命名映射；默认 `RenderSubquery` 仍使用当前 Builder 状态，CTE/Union/普通子查询调用路径未被删除。
- [JoinClause.cs](../../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs) 的类型化派生表 Join 先克隆参数和重命名映射，再渲染子查询和解析 On 谓词；真实参数管理器仅在候选阶段全部成功后通过 `ReplaceParameterManager()` 替换。
- 失败路径恢复旧参数管理器引用、旧子查询重命名映射、别名和 Operation，并移除未完成提交的 Join 项；没有对故障第三方管理器执行 `Clear/Add` 回放。
- Join 项的提交标记位于别名注册完成之后，别名注册异常不会留下连接项或来源图。

### 5.2 测试有效性

- `TypedSubqueryJoin_WhenCustomParameterRenderFails_ShouldKeepStateUnchangedAndAllowRetry` 使用普通 `IParameterManager`，已有 `@_p_0`，子查询含两个参数且 On 谓词含常量；验证 SQL、参数、Alias、Operation、来源图、Join 状态和原编号重试。
- `TypedSubqueryJoin_WhenCustomAdvancedParameterRenderFails_ShouldKeepMetadataStateUnchangedAndAllowRetry` 使用增强管理器，额外验证失败前后增强参数元数据及成功后的元数据数量。
- 失败测试没有删除异常、降低关键断言或用 Mock 替代真实参数管理器行为；测试管理器仅实现公开接口并在第二次 Add 时确定性抛出。
- 既有 `TypedSubqueryJoin_WhenPredicateReferencesUnprojectedMember_ShouldRollbackRenderStateAndAllowRetry`、别名冲突和别名格式化失败测试继续覆盖默认管理器路径。

## 6. 架构、维护性与安全 Review

- `ParameterManagerState` 和候选映射 API 均为 `internal`，未扩大普通用户公共 API；没有具体参数管理器类型分支。
- 派生表 Join 复用了实体 Join 已建立的候选参数/一次替换语义，没有新增第二套真实管理器补偿实现。
- 子查询参数名称映射使用深复制，成功后才写回 Builder；失败候选不会污染后续重试的名称分配。
- 候选 Clone 增加构建期分配，但计划 Phase 9 已有 Join Benchmark，且本轮未做未经证据支持的热路径优化。
- 未发现 OWASP Top 10 风险、原始 SQL 注入边界扩大、凭据处理变化或安全控制绕过。

## 7. 独立验证

| 命令/检查 | 结果 |
| --- | --- |
| FIX-012 派生表专项、谓词失败、别名失败双 TFM | `14/14`，失败 `0`，跳过 `0` |
| SQL Core 全量双 TFM | `2394/2394`，失败 `0`，跳过 `0` |
| `get_errors`：SQL Core 源码和 SQL Core 测试 | 无错误 |
| `git diff --check` | 通过；仅 CRLF/LF 提示 |
| Release 全解构建 | `0 errors / 133 warnings`，execution.md 有记录 |
| Custom Provider | `38/38`，execution.md 有记录 |
| SQLite Unit / Integration | `218/218`、`266/266`，execution.md 有记录 |
| Analyzer / Dapper Core | `19/19`、`262/262`，execution.md 有记录 |
| MySQL / PostgreSQL / SQL Server / Oracle Unit | `354/354`、`268/268`、`550/550`、`180/180`，execution.md 有记录 |

既有警告包括 `RS0027`、net6.0 EOL、依赖包 TFM 支持、成员隐藏、过时 API、nullable 注释和 SQLite RID 提示；未发现本轮 FIX-012 新增的错误或 Analyzer 绕过。外部数据库 Gate 仍为 `NOT_RUN_EXTERNAL_GATE_MISSING`。

## 8. 问题分级

- BLOCKER：无。
- HIGH：无。
- MEDIUM：无新增问题。
- LOW：无新增问题。

## 9. 复审状态

- `FIX-011`：`RESOLVED`。
- `FIX-012`：`RESOLVED`。
- 未新增 FIX-xxx。
- execution.md 终态为 `COMPLETED`，与本轮 PASS 结论一致。
- 当前 Reviewer 结论：`PASS`。

## 10. 最终 Checklist

- [x] 上一轮 `FIX-011` 已重新核对并保持解决。
- [x] `FIX-012` 的普通第三方参数管理器派生表路径已验证。
- [x] `FIX-012` 的增强第三方参数管理器及元数据路径已验证。
- [x] 子查询参数、重命名映射和 On 谓词均在候选状态处理。
- [x] 失败后参数、SQL、Alias、Operation、Join 和来源图保持不变并可重试。
- [x] SQL Core 全量测试通过。
- [x] 全解构建、错误检查和 `git diff --check` 已验证。
- [x] 外部数据库 Gate 缺失已明确记录。
- [x] 未修改业务代码或测试代码；未执行 commit/push。
