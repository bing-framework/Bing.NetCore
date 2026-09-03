<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-SQL-RELEASE-HARDENING-20260831
AI_REVIEWED_AT: 2026-09-02T21:53:16.0925561+08:00

# BING-SQL-RELEASE-HARDENING-20260831 Review Fix 独立复审报告

## 验收结论

本次按当前 `plan.md`、`execution.md`、上一轮 `review.md`、实际源码、Git Diff、Public API 基线和独立命令结果复审 Review Fix。未依据 `execution.md` 的 `COMPLETED` 声明直接判定，也未修改业务代码、测试代码、`plan.md` 或 `execution.md`。

最终结论为 `NEEDS_FIX`。上一轮 `FIX-001` 已解决：Provider Profile 现在可以显式表达能力关闭来源，指定六类 Gate 均消费该来源，专项、全量、官方 Provider、Analyzer 和 Release Build 均通过。上一轮因 `fixScope=must` 延后的 `FIX-002`、`FIX-003` 仍未解决，且均为 `SHOULD_FIX`，因此不能判定 PASS 或 PASS_WITH_ISSUES。

原发布硬化任务仍应保持 `PARTIAL` / `BLOCKED`：外部 Provider 真实集成、Oracle/SQL Server 安全 fixture、完整 Procedure/Cancellation/资源复用矩阵、FormalHost 同 key Benchmark 和完整 Release Gate 均没有新增可发布证据。

## 上一轮 FIX 逐项复审

| 上一轮 FIX | 状态 | 独立证据 |
| --- | --- | --- |
| `FIX-001`：Profile 不能表达能力关闭来源 | `RESOLVED` | `SqlProviderProfile` 新增 nullable failure reason 元数据并由快照复制；Returning、Multiple Result、Stored Procedure、Output Parameters、Streaming、Cancellation Gate 均读取 Profile 显式原因，未发现按 Provider Key 或 `DatabaseType` 猜测来源。六类测试覆盖 `DatabaseUnsupported` 与 `ProviderImplementationGap`，并断言连接/命令未访问。 |
| `FIX-002`：`execution.md` 顶层终态事实矛盾 | `NOT_RESOLVED` | 当前顶部和后续 Round 声明 SDK `8.0.424`、核心测试与构建通过，但前半当前状态表仍声明恢复 `8.0.405`、Unit/Analyzer `NOT EXECUTED`、Build `BLOCKED`；这些内容没有全部限定为历史快照。 |
| `FIX-003`：Final Report 必须严格 18 节 | `NOT_RESOLVED` | UTF-8 实际统计二级标题为 `20`，仍包含独立 Round 补充章节，不满足严格 18 个二级标题的契约。 |

## FIX-001 源码与契约验证

- `SqlProviderMutationCapabilities` 新增 MultiRowValues、UpdateFrom、DeleteUsing、Returning 的可空失败原因。
- `SqlProviderExecutionCapabilities` 新增 MultipleResultSets、Streaming、Cancellation 的可空失败原因。
- `SqlProviderTransactionCapabilities` 新增 Transactions 的可空失败原因。
- `SqlProviderProcedureCapabilities` 新增 StoredProcedures、OutputParameters 的可空失败原因。
- `CreateSnapshot()` 深复制全部新增元数据；`PublicAPI.Unshipped.txt` 已登记 10 个属性的 getter/init 共 20 项。
- Gate 使用显式原因优先、历史默认分类兜底，保持未设置新字段的第三方 Provider 兼容行为。
- MySQL、SQLite、Oracle 对实际关闭能力显式声明 `DatabaseUnsupported`；未发现 Provider Key 字符串分支或数据库类型分支用于推断失败来源。
- Dapper 原生输出参数在命令物化后拒绝时继续携带 Profile 原因和 Provider Key，且测试断言命令未执行。

## 独立验证结果

| 验证 | 结果 |
| --- | --- |
| `dotnet --version` | `8.0.424`，与当前 `global.json` 一致 |
| Profile 专项 | Bing.Data.Sql.Tests net6.0 `12/12`；net8.0 `12/12` |
| Gate / Multiple Result 专项 | Bing.Dapper.Core.Tests net6.0 `33/33`；net8.0 `33/33` |
| Bing.Data.Sql.Tests 全量 | net6.0 `1276/1276`；net8.0 `1276/1276` |
| Bing.Dapper.Core.Tests 全量 | net6.0 `161/161`；net8.0 `161/161` |
| OfficialProviderInstanceTest | net6.0 `6/6`；net8.0 `6/6` |
| Analyzer Tests | net8.0 `32/32` |
| Bing.Data.Sql Release Build | PASS，`0 warning / 0 error`（本次增量构建输出） |
| Bing.Dapper.Core Release Build | PASS，`0 warning / 0 error` |
| `git diff --check` | PASS，仅 CRLF/LF 转换提示 |
| Final Report 标题统计 | FAIL，实际 `20`，要求严格 `18` |

说明：测试命令首次编译 Data.Sql 时仍输出既有 RS0026/RS0027 警告；随后独立 `dotnet build --no-restore` 为增量构建并显示 0 warning。`execution.md` 的终态描述必须区分实际完整编译警告与增量构建输出，不能据后者抹除前者。

## 计划与发布边界

| 范围 | 状态 | 结论 |
| --- | --- | --- |
| `REL-P6-02` capability reason model | `PASS` | 上一轮 MUST_FIX 已形成可由 Profile/Provider 验证的来源模型，六类要求场景直接覆盖。 |
| `REL-P0-01` / `REL-P10-02` 执行证据 | `FAIL` | `execution.md` 顶层当前状态仍互相矛盾。 |
| `REL-P13-02` Final Report | `FAIL` | 二级标题实际 20 个，不是严格 18 个。 |
| 外部 Provider Integration | `BLOCKED` | 无授权数据库、专用 Gate 和安全 reset 证据；不得以静态 Profile 或本地替身替代。 |
| FormalHost Benchmark | `NOT_VERIFIABLE` / `BLOCKED` | 无本任务同 key before/after 原始制品。 |
| 完整 Release Gate | `PARTIAL` / `BLOCKED` | 本地核心回归通过，但不构成全 Provider 发布放行。 |

## 问题分级

### BLOCKER

无新增 BLOCKER。

### HIGH

无开放 HIGH；上一轮 `FIX-001` 已验证解决。

### MEDIUM

- `execution.md` 的当前 SDK、Build、Unit 和 Analyzer 状态仍互相冲突，详见本轮 `FIX-001`。
- Final Report 仍为 20 个二级标题，详见本轮 `FIX-002`。

### LOW

无新增独立问题。

## 修复清单

### FIX-001

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 当前状态：`OPEN`
- 来源：上一轮 `FIX-002`，本轮复审为 `NOT_RESOLVED`
- 对应计划项：`REL-P0-01`、`REL-P10-02`、`REL-P13-02`
- 涉及文件：`ai_docs/tasks/BING-SQL-RELEASE-HARDENING-20260831/execution.md`
- 问题：顶层终态同时把 SDK、Unit、Analyzer 和 Build 描述为两套相冲突的当前事实。
- 证据：当前 `global.json` 与 `dotnet --version` 均为 `8.0.424`，独立测试和构建通过；文件前半仍以现在时声明 `8.0.405`、Unit/Analyzer `NOT EXECUTED`、Build `BLOCKED`。
- 影响：人工和机器消费者无法可靠识别最终执行事实，发布审计不可复现。
- 修复目标：所有顶层当前状态只呈现最终 `8.0.424` 事实；历史 SDK 阻塞必须明确标注历史 Round，不能与当前结果并列。
- 明确要求：统一执行结论、API/配置变化、测试结果、Build/Typecheck、已知问题和 Reviewer 注意事项；保留外部 Provider、Benchmark、完整 Release Gate 的真实阻塞；区分 Data.Sql 完整编译的既有警告与增量构建 0 warning 输出。
- 验证方式：全文核对 `8.0.405`、`NOT EXECUTED`、`BLOCKED`，确认仅用于明确历史或真实未执行范围；当前 SDK、测试计数、构建输出必须与独立命令一致。

### FIX-002

- 严重程度：`MEDIUM`
- 处理要求：`SHOULD_FIX`
- 当前状态：`OPEN`
- 来源：上一轮 `FIX-003`，本轮复审为 `NOT_RESOLVED`
- 对应计划项：`REL-P13-02 Final Report`
- 涉及文件：`artifacts/reports/BING-SQL-RELEASE-HARDENING-20260831-final-report.md`
- 问题：最终报告实际有 20 个二级标题，不满足严格 18 节契约。
- 证据：UTF-8 读取后按二级标题模式统计返回 `20`；Round 4、Round 5、Round 8 FIX 和 Round 8 补充仍作为独立章节存在。
- 影响：报告结构不符合计划要求，重复章节继续放大 SDK 与验证状态冲突。
- 修复目标：合并 Round 补充内容，使二级标题数量严格等于 18，同时保留当前 SDK、SQLite 证据、外部阻塞和 `PARTIAL` 发布结论。
- 明确要求：不得删除真实失败或阻塞，不得把静态/生成测试证据提升为 ReleaseEvidence，并同步消除过时的当前 SDK 叙述。
- 验证方式：UTF-8 统计二级标题恰好 18；核对 SDK、测试计数、Data.Sql 警告边界、SQLite `ReleaseReady=false`、外部 Provider 和 Benchmark 状态；运行 `git diff --check`。

## 最终 Checklist

- [x] 已按上一轮 `FIX-001/002/003` 顺序独立复审。
- [x] 已验证能力来源模型、六类 Gate、官方 Provider 声明和 Public API 基线。
- [x] 已运行专项、两个核心项目全量、官方 Provider、Analyzer 和 Release Build。
- [x] 已核对当前 SDK、Git Diff 边界、`git diff --check` 和 Final Report 标题数量。
- [x] 已确认上一轮 MUST_FIX 为 `RESOLVED`。
- [ ] `execution.md` 顶层终态事实一致。
- [ ] Final Report 严格 18 个二级标题。
- [ ] 外部 Provider、FormalHost Benchmark 和完整 Release Gate 按真实条件完成或继续明确保持 BLOCKED。
