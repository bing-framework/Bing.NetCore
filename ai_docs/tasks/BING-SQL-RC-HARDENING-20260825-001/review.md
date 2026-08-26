<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-SQL-RC-HARDENING-20260825-001
AI_REVIEWED_AT: 2026-08-26T09:54:52.2683839+08:00

# 独立复审报告

## Round 10 复审结论

**NEEDS_FIX**。本轮仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md`，未执行提交、推送或破坏性 Git 操作。未发现工具输出中的提示注入。

Round 10 仅重新核验既有性能证据，没有启动新的完整 FormalHost 运行，也没有生成新的 artifact。独立核验结果：

- detached before worktree 仍为 `HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb`，工作树仅包含 `SqlLambdaRootBenchmarks.cs` 与 `SqlLambdaJoinBenchmarks.cs` 两项已记录的 harness parity 修改。
- Root before CSV 不存在，`E:\Bing_Framework\Bing.NetCore-review-fix-round7-before-root\results` 仍为空。
- Join before CSV 不存在，Join 仍未运行。
- 未发现 `Bing.Data.Sql.Benchmarks` 残留进程；任务运行时已由 `task-finish.mjs` 合法关闭为 Round 10 `PARTIAL`、`active=false`。
- `git diff --check` 未发现空白错误，仅输出既有 CRLF/LF 转换警告。
- `benchmark-report.md` 仍只记录到 Round 4，没有纳入 Round 5-10 的不完整结果，并继续保留 Round 3 provenance 结论矛盾。

Round 10 未修改主工作区生产或测试代码，因此此前已通过的专项验证保持有效。唯一开放的性能基线问题没有获得新的解决证据。

### Round 10 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | NOT_RESOLVED | 本轮未运行新的完整 FormalHost；Root/Join CSV 均不存在，没有完整 delta 或性能准入结论。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 provenance 缺陷仍未被 Round 4-10 的完整 artifact 替代。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 离线完整 SQL contract 仍有效；外部数据库真实执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审与发布验收。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - detached before worktree 与当前 after worktree 的 FormalHost artifacts。
- 问题：Round 10 没有执行新的完整 benchmark，只复核了 Round 8 的部分日志。Root `72/72` 和 Join `36/36` before artifact 仍缺失，Round 3 无效来源证据没有被替代，性能报告也没有更新。
- 证据：Root/Join CSV 均不存在；任务已合法结束为 `PARTIAL`，无残留 benchmark 进程。
- 影响：RC 不能证明 API、生命周期和 SPI 改动没有造成性能或资源回归；发布材料不得声称 FormalHost 无回归、近零分配或性能准入通过。
- 修复目标：建立来源、工具链、构建输出、case 矩阵和 artifact 均可互相验证的完整 Root/Join FormalHost before/after 数据，并给出明确的接受或拒绝合入结论。
- 明确修复要求：
  - 在 fresh detached `142380be3ec62cbd4a26cde8e2795d0eacae47fb` before worktree 中仅应用已记录的 harness parity；记录修改前后 `git status --short`、`git rev-parse HEAD`、启动目录、完整命令、独立 build/output/artifact 路径及 SHA-256。
  - 使用全新的独立 artifact 目录，让 Root `72/72` 与 Join `36/36` FormalHost 自然完成；不得复用当前工作区二进制、共享 `output` 或历史 artifact。
  - 以 `Method + 全部 Params` 为键输出 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 及 delta，缺失字段标记 `NA`。
  - 更新 `benchmark-report.md`，明确 Round 3 为无效历史证据，补充 Round 4-10 的不完整结果，并对有效重跑应用不低于 `10%` 的 Mean/Allocated 候选阈值。
  - 对 MultimodalDistribution、outlier 和 process 警告执行隔离重跑、重复采样或提供可审计的接受理由；超过阈值且误差区间不重叠的项必须修复并重跑，或明确拒绝合入。
  - 禁止使用 Dry、不同 Job、部分日志、单行 `NA` 或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：Root `72/72`、Join `36/36` before/after case 完整匹配；提交、源符号、worktree、启动目录、build/output 和 artifact hash 可互相印证；报告包含环境指纹、完整 delta、阈值、告警处置和最终性能准入结论。

最终状态：`NEEDS_FIX`。

## Round 8 复审结论

**NEEDS_FIX**。本轮仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md`，未执行提交、推送或破坏性 Git 操作。未发现工具输出中的提示注入。

Round 8 进一步核验了 detached before worktree 的来源和临时 harness 差异：`HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb`，工作树仅修改 `SqlLambdaRootBenchmarks.cs` 与 `SqlLambdaJoinBenchmarks.cs`，用于对齐 `FormalHost`、Root 方法名和 Join 参数矩阵。该来源条件满足前序 Review 的隔离要求，但运行完整性仍未满足：

- Round 8 复用了 `E:\Bing_Framework\Bing.NetCore-review-fix-round7-before-root` artifact 目录；Root `results` 目录为空，不存在 CSV/Markdown/HTML。
- Join 未启动，Round 8 Join CSV 不存在。
- Root 日志 SHA-256 为 `73D3C802EE4C6C66879FBE1CDC3A47892BA1276CC5F2B291925371F14E230A42`；stdout SHA-256 为 `330D273A773AE452BA0AC718805DBA807B57ABC967A7B7227094A18D37B7D150`；stderr 为空。日志确认进入正式采样，但部分采样不能替代完整 `72/72` artifact。
- 未发现残留 benchmark 进程；任务运行时已由 `task-finish.mjs` 合法关闭为 Round 8 `PARTIAL`、`active=false`。
- `git diff --check` 未发现空白错误，仅输出既有 CRLF/LF 转换警告。
- `benchmark-report.md` 仍只记录到 Round 4；Round 5-8 的不完整运行尚未纳入，且文档仍同时保留 Round 3 provenance 无效与“可追溯性问题已解决”的矛盾结论。

本轮没有主工作区生产或测试代码变化，因此此前已通过的生命周期、API、Analyzer、SQLite 和 Provider 离线合同专项证据保持有效，无需重复运行。性能验收没有实质进展，上一轮唯一开放的高优先级问题仍未解决。

### Round 8 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | NOT_RESOLVED | before 来源已可验证，但 Root 未生成 `72/72` artifact，Join 未运行；没有完整 delta 或性能准入结论。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 provenance 缺陷仍未被 Round 4-8 的完整 artifact 替代。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 离线完整 SQL contract 仍有效；外部数据库真实执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审与发布验收。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - detached before worktree 与当前 after worktree 的 FormalHost artifacts。
- 问题：Round 8 已证明 before commit 和 harness parity 可追溯，但 Root FormalHost 没有自然完成并导出最终 artifact，Join 未运行；性能报告也未同步最新轮次。Round 3 无效来源证据仍没有被完整替代。
- 证据：Root/Join CSV 均不存在；Root 只有已哈希的部分日志/stdout；运行时已合法结束为 `PARTIAL`，无残留进程。
- 影响：RC 不能证明 API、生命周期和 SPI 改动没有造成性能或资源回归；发布材料不得声称 FormalHost 无回归、近零分配或性能准入通过。
- 修复目标：建立来源、工具链、构建输出、case 矩阵和 artifact 均可互相验证的完整 Root/Join FormalHost before/after 数据，并给出明确的接受或拒绝合入结论。
- 明确修复要求：
  - 在 fresh detached `142380be3ec62cbd4a26cde8e2795d0eacae47fb` before worktree 中仅应用已记录的 harness parity；记录修改前后 `git status --short`、`git rev-parse HEAD`、启动目录、完整命令、独立 build/output/artifact 路径及 SHA-256。
  - 使用独立的新 artifact 目录，并让 Root `72/72` 与 Join `36/36` FormalHost 自然完成。before/after 必须分别在各自 worktree 构建和运行，不得复用当前工作区二进制、共享 `output` 或历史 artifact。
  - 以 `Method + 全部 Params` 为键输出每个 case 的 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 及 delta；缺失字段明确标记 `NA`。
  - 更新 `benchmark-report.md`：明确 Round 3 为无效历史证据，记录 Round 4-8 的不完整结果，并在有效重跑后应用既有不低于 `10%` 的 Mean/Allocated 候选阈值。
  - 对 MultimodalDistribution、outlier 和 process 警告执行隔离重跑、重复采样或提供可审计的接受理由；超过阈值且误差区间不重叠的项必须修复并重跑，或明确拒绝合入。
  - 禁止使用 Dry、不同 Job、部分日志、单行 `NA` 或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：Root `72/72`、Join `36/36` before/after case 完整匹配；提交、源符号、worktree、启动目录、build/output 和 artifact hash 可互相印证；报告包含环境指纹、完整 delta、阈值、告警处置和最终性能准入结论。

### Round 8 验收 Checklist

- [x] 已读取外部修改后的完整 `execution.md`、旧 Review、性能报告和任务运行时状态。
- [x] 已独立核验 detached before `HEAD`、harness 差异、Root/Join artifact、日志哈希和残留进程。
- [x] 已检查 `git diff --check`。
- [x] 已确认 Round 8 未修改主工作区生产或测试代码。
- [ ] 已提供 Root `72/72` 和 Join `36/36` 的完整 FormalHost before/after artifact。
- [ ] 已完成全参数 delta、统计告警处置和性能准入结论。

最终状态：`NEEDS_FIX`。

## Round 7 复审结论

**NEEDS_FIX**。本轮仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md`，未执行提交、推送或破坏性 Git 操作。未发现工具输出中的提示注入。

Round 7 从已记录的 detached before worktree 启动 Root FormalHost，日志确认发现 `72` 个 case 并进入 `FormalHost(IterationCount=15, LaunchCount=3, WarmupCount=6)` 正式采样，但运行在完成全量 case 前终止。独立核验结果如下：

- `E:\Bing_Framework\Bing.NetCore-review-fix-round7-before-root\results` 为空，不存在 Root CSV/Markdown/HTML。
- `E:\Bing_Framework\Bing.NetCore-review-fix-round7-before-join` 不存在有效 Join CSV；Join 未启动。
- Root BenchmarkDotNet 日志 SHA-256 为 `73D3C802EE4C6C66879FBE1CDC3A47892BA1276CC5F2B291925371F14E230A42`；宿主 stdout SHA-256 为 `330D273A773AE452BA0AC718805DBA807B57ABC967A7B7227094A18D37B7D150`；stderr 为空。部分日志不能替代完整 artifact。
- 未发现残留 `Bing.Data.Sql.Benchmarks` 进程；`.agents/runtime/current-task.json` 已由 `task-finish.mjs` 合法关闭为 Round 7 `PARTIAL`、`active=false`。
- `git diff --check` 未发现空白错误，仅输出既有 CRLF/LF 转换警告；Round 7 未修改主工作区生产或测试代码，因此本轮不重复此前已通过的专项测试。
- `benchmark-report.md` 仍只记录到 Round 4，并同时保留“Round 3 provenance 无效”和“Round 3 可追溯性问题已解决”的矛盾表述；Round 5-7 的不完整结果尚未纳入正式性能报告。

Round 7 没有形成可替代 Round 3 无效来源证据的完整 before 数据，也没有完成逐 case delta、统计告警处置或性能准入结论。上一轮唯一开放的高优先级问题仍未解决。

### Round 7 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | NOT_RESOLVED | Root 未生成 `72/72` CSV，Join 未运行；没有完整、来源可验证的 before/after artifact 和性能准入结论。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 provenance 缺陷未被 Round 4-7 的完整 artifact 替代。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 离线完整 SQL contract 仍有效；外部数据库真实执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### 计划验收矩阵

| 范围 | 结论 | 实际证据 |
| --- | --- | --- |
| BINGSQL002 Analyzer | PASS | 现行 `SqlInterpolated(...)` 指引和 source span 测试已由前序专项验证。 |
| MultipleQuery 生命周期 | PASS | sync-over-async 已移除；组合失败、取消、重复释放和一次性清理由前序 SQL Server/SQLite 专项验证。 |
| Breaking API 删除 | PASS | `OperationId`、`Group(...)`、`SetRoots(...)` 和目标重复终结转发已删除并有负向契约。 |
| Runtime SPI | PASS | Executor 与 Builder Source 职责已分离，前序 API contract 通过。 |
| Fluent/Text 2-7 映射 | PASS | SQLite 真实执行和错误/取消路径的前序专项证据有效。 |
| Provider SQL contract | PASS | 五个 Provider 离线完整 SQL contract 已验证；外部真实数据库为合法 `GATE_SKIPPED`。 |
| FormalHost 性能基线 | FAIL | Round 7 Root/Join CSV 均不存在，Round 3 before provenance 仍无效。 |
| 性能报告与发布证据 | FAIL | 报告未纳入 Round 5-7，且 Round 3 来源结论自相矛盾，不能支撑 RC 性能验收。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审与发布验收。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - detached before worktree 与当前 after worktree 的 FormalHost artifacts。
- 问题：Round 7 Root FormalHost 仅完成部分采样，没有生成最终 CSV；Join 未运行。Round 3 before provenance 已被独立 Reviewer 判定无效，Round 4-7 均未形成完整替代 artifact。性能报告缺少最新轮次并含矛盾结论。
- 证据：Round 7 Root `results` 目录为空，Join CSV 不存在；仅有已哈希的部分日志/stdout。任务已合法结束为 `PARTIAL`，无残留 benchmark 进程。
- 影响：RC 不能证明 API、生命周期和 SPI 改动没有造成性能或资源回归；发布材料不得声称 FormalHost 无回归、近零分配或性能准入通过。
- 修复目标：建立来源、工具链、构建输出、case 矩阵和 artifact 均可互相验证的完整 Root/Join FormalHost before/after 数据，并给出明确的接受或拒绝合入结论。
- 明确修复要求：
  - 在 fresh detached `142380be3ec62cbd4a26cde8e2795d0eacae47fb` before worktree 中只应用明确记录的 benchmark harness parity；记录修改前后 `git status --short`、`git rev-parse HEAD`、启动目录、完整命令、独立 build/output/artifact 绝对路径及 SHA-256。
  - 让 Root `72/72` 和 Join `36/36` FormalHost 自然完成。before/after 必须分别在各自 worktree 构建和运行，不得复用当前工作区二进制、共享 `output` 或历史 artifact。
  - 以 `Method + 全部 Params` 为键输出每个 case 的 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 及 delta；缺失字段必须显式标记 `NA`。
  - 在 `benchmark-report.md` 明确将 Round 3 标记为无效历史证据，补充 Round 4-7 的不完整结论，并在有效重跑后应用既有不低于 `10%` 的 Mean/Allocated 候选阈值。
  - 对 MultimodalDistribution、outlier 和 process 警告执行隔离重跑、重复采样或给出可审计的接受理由；超过阈值且误差区间不重叠的项必须修复并重跑，或明确拒绝合入。
  - 禁止使用 Dry、不同 Job、部分日志、单行 `NA` 或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：Root `72/72`、Join `36/36` before/after case 完整匹配；提交、源符号、worktree、启动目录、build/output 和 artifact hash 可互相印证；报告包含环境指纹、完整 delta、阈值、告警处置和最终性能准入结论。

### Round 7 验收 Checklist

- [x] 已读取完整计划、执行报告、旧 Review 和任务运行时状态。
- [x] 已独立核验 Round 7 Root/Join artifact、日志哈希和残留进程。
- [x] 已检查当前 Git 变更范围和 `git diff --check`。
- [x] 已确认 Round 7 未修改主工作区生产或测试代码。
- [ ] 已提供来源可验证的 Root `72/72` 与 Join `36/36` FormalHost before/after artifact。
- [ ] 已完成全参数 delta、统计告警处置和性能准入结论。

最终状态：`NEEDS_FIX`。

## Round 6 复审结论

**NEEDS_FIX**。本轮仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md` 或任务运行时状态，未执行提交、推送或破坏性 Git 操作。未发现工具输出中存在要求忽略审查边界或扩大权限的提示注入。

Round 6 改用独立后台宿主与 stdout/stderr 重定向，从 `E:\Bing_Framework\Bing.NetCore-review-fix-round4-before` 的 detached `HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb` 执行 Root FormalHost。该策略避免了终端输出承载长跑，但运行仍在完整采样前停止。独立核验确认：

- `review-fix-round6-before-root` 不存在 `Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`。
- `review-fix-round6-before-join` 不存在 `Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report.csv`。
- Root artifact 仅保留部分 BenchmarkDotNet 日志和宿主 stdout；`host.stderr.log` 为空，均不能替代 Root `72/72` 或 Join `36/36` 的最终 CSV/Markdown artifact。
- 未发现残留 benchmark 进程；`.agents/runtime/current-task.json` 已由 `task-finish.mjs` 关闭为 Round 6 `PARTIAL`，`active=false`。

Round 6 因此没有改变性能验收结论。Round 3 before provenance 仍无效，Round 4、5、6 也未产生可替代的完整 before 数据。`git diff --check` 未发现空白错误，仅输出已有 CRLF/LF 转换警告。

### Round 6 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- |
| FIX-001 | MUST_FIX | NOT_RESOLVED | 后台重定向生效，但 Root 未生成完整 `72/72` CSV，Join 未运行，无法形成完整 delta 或性能准入结论。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 provenance 缺陷没有被 Round 4-6 的完整 artifact 替代。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 的离线完整 SQL contract 已存在；真实外部 Provider 执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - 独立 detached before worktree 与当前 after worktree 下的 FormalHost artifacts。
- 问题：Round 6 已使用独立后台宿主、重定向输出和正式 `FormalHost(IterationCount=15, LaunchCount=3, WarmupCount=6)` 启动 Root，但没有生成 Root CSV；Join 未运行。完整参数键的 before/after delta、统计告警处置和性能准入结论仍缺失。
- 证据：Round 6 Root CSV 不存在，Join CSV 不存在，只有部分日志和宿主 stdout/stderr；after Root/Join CSV 仍分别有 `72`、`36` 行。任务已合法结束为 `PARTIAL`，且无残留 benchmark 进程。
- 影响：RC 不能证明 API 和生命周期加固未造成性能或资源回归；发布材料不得声称 FormalHost 无回归或近零分配。
- 修复目标：建立来源、工具链、输出位置和输入矩阵均可验证的完整 Root/Join FormalHost before/after 数据，并按预设阈值给出可复核的准入结论。
- 明确修复要求：
  - 在 fresh detached `142380be` before worktree 中只应用已记录的 harness parity，并记录修改前后 `git status --short`、`git rev-parse HEAD`、绝对 worktree/输出/artifact 路径、启动目录、完整命令和 SHA-256。
  - 使用能让命令自然完成的稳定执行环境，得到 Root `72/72` 与 Join `36/36` FormalHost CSV/Markdown/log；before/after artifact 与 build/output 必须位于各自 worktree，不能复用当前工作区二进制、`output` 或 artifacts。
  - 以 `Method + 全部 Params` 为键输出每个 case 的 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 delta；缺失字段必须显式为 `NA`，不得折叠参数维度。
  - 在 `benchmark-report.md` 应用既有不低于 `10%` 的 Mean/Allocated 候选阈值。对 MultimodalDistribution、outlier 和 process 警告进行隔离重跑、重复采样或给出有证据的接受理由；超过阈值且误差区间不重叠的项必须性能修复、重跑确认或明确拒绝合入。
  - 将 Round 3 artifact 清楚标记为无效历史证据；禁止将 Dry、不同 Job、部分日志或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：提交、源文件符号、启动目录和 artifact hash 可互相印证；Root `72/72`、Join `36/36` before/after case 完整匹配；报告包括环境指纹、完整 delta、阈值和告警处置，以及明确的接受或不合入结论。

## Round 5 复审结论

**NEEDS_FIX**。本轮仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md` 或任务运行时状态，未执行提交、推送或破坏性 Git 操作。未发现工具输出中存在要求忽略审查边界或扩大权限的提示注入。

Round 5 再次从已验证的 detached before worktree 启动 Root FormalHost。执行报告记录的工作树为 `E:\Bing_Framework\Bing.NetCore-review-fix-round4-before`，`HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb`，并只包含已记录的 benchmark harness parity 修改。日志显示该运行进入正式采样且完成了部分 case，但没有生成 Root CSV；Join 未启动。

独立核验结果：

- `review-fix-round5b-before-root` 不存在 `Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`。
- `review-fix-round5b-before-join` 不存在 `Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report.csv`。
- Root 仅保留一份部分日志 `Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-20260825-213950.log`，SHA-256 为 `944B873DA49FA92DC17111D72B3C2A343731F60ACCD64013774459EDA3138548`，不能替代 `72/72` CSV。
- 未发现残留 benchmark 进程；`.agents/runtime/current-task.json` 已由 `task-finish.mjs` 关闭为 Round 5 `PARTIAL`，`active=false`。

因此，Round 5 没有改变性能验收结论。Round 3 before provenance 仍无效，Round 4/5 也未产出可替代的完整 before artifact。当前 Git Diff 仍限于 RC 计划中的 SQL API、生命周期、SPI、Provider 合同、测试、benchmark 和迁移文档；`git diff --check` 未发现空白错误，仅输出已有 CRLF/LF 转换警告。

### Round 5 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | NOT_RESOLVED | before worktree 和 FormalHost 配置保持可追溯，但 Root 未生成完整 `72/72` CSV，Join 未运行，无法形成完整 delta 或性能准入结论。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 provenance 缺陷没有被 Round 4/5 的完整 artifact 替代。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 的离线完整 SQL contract 已存在；真实外部 Provider 执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - 独立 detached before worktree 与当前 after worktree 下的 FormalHost artifacts。
- 问题：Round 5 已从可验证的 before worktree 使用 `FormalHost(IterationCount=15, LaunchCount=3, WarmupCount=6)` 启动 Root，但主进程在部分采样后退出，未生成 Root CSV；Join 未运行。完整参数键的 before/after delta、统计告警处置和性能准入结论仍缺失。
- 证据：Round 5 Root CSV 不存在，Join CSV 不存在，只有已哈希的部分 Root 日志；after Root/Join CSV 仍分别有 `72`、`36` 行。任务已合法结束为 `PARTIAL`，且无残留 benchmark 进程。
- 影响：RC 不能证明 API 和生命周期加固未造成性能或资源回归；发布材料不得声称 FormalHost 无回归或近零分配。
- 修复目标：建立来源、工具链、输出位置和输入矩阵均可验证的完整 Root/Join FormalHost before/after 数据，并按预设阈值给出可复核的准入结论。
- 明确修复要求：
  - 在 fresh detached `142380be` before worktree 中只应用已记录的 harness parity，并记录修改前后 `git status --short`、`git rev-parse HEAD`、绝对 worktree/输出/artifact 路径、启动目录、完整命令和 SHA-256。
  - 使用能让命令自然完成的稳定执行环境，得到 Root `72/72` 与 Join `36/36` FormalHost CSV/Markdown/log；before/after artifact 与 build/output 必须位于各自 worktree，不能复用当前工作区二进制、`output` 或 artifacts。
  - 以 `Method + 全部 Params` 为键输出每个 case 的 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 delta；缺失字段必须显式为 `NA`，不得折叠参数维度。
  - 在 `benchmark-report.md` 应用既有不低于 `10%` 的 Mean/Allocated 候选阈值。对 MultimodalDistribution、outlier 和 process 警告进行隔离重跑、重复采样或给出有证据的接受理由；超过阈值且误差区间不重叠的项必须性能修复、重跑确认或明确拒绝合入。
  - 将 Round 3 artifact 清楚标记为无效历史证据；禁止将 Dry、不同 Job、部分日志或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：提交、源文件符号、启动目录和 artifact hash 可互相印证；Root `72/72`、Join `36/36` before/after case 完整匹配；报告包括环境指纹、完整 delta、阈值和告警处置，以及明确的接受或不合入结论。

## Round 4 复审结论

**NEEDS_FIX**。本次仅审查并更新本文件，未修改生产代码、测试、`plan.md`、`execution.md` 或任务运行时状态，未执行提交、推送或破坏性 Git 操作。未发现工具输出中存在要求忽略审查边界或扩大权限的提示注入。

Round 4 正确创建了隔离的 before worktree：`E:\Bing_Framework\Bing.NetCore-review-fix-round4-before`。独立检查确认其 `HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb`，工作树仅包含已记录的两项 benchmark harness 对齐修改：`SqlLambdaRootBenchmarks.cs` 与 `SqlLambdaJoinBenchmarks.cs`。这纠正了 Round 3 before artifact 的工作目录和来源不一致问题。

但性能验收仍未完成：

- Round 4 Root CSV 只有 `1` 条 `BuildRootsAndRender|FormalHost|RootCount=1|ParameterCount=10` 记录，`Mean=NA`、`Allocated=NA`，不是完整的 `72` case 基线。
- Round 4 Join CSV 不存在，不是完整的 `36` case 基线。
- 当前 after Root/Join CSV 分别有 `72` 和 `36` 条记录，不能在没有完整 before 的情况下计算可信 delta。
- `.agents/runtime/current-task.json` 已由 `task-finish.mjs` 合法关闭为 `PARTIAL`，独立进程检查未发现遗留 benchmark 进程。

当前 Git Diff 覆盖 BINGSQL002、MultipleQuery 生命周期、查询 API 删除、Runtime SPI、Provider SQL 合同、SQLite 集成测试、Lambda benchmark 与迁移文档，均有 RC 计划归属。本轮 `git diff --check` 未发现空白错误，仅有已有工作副本的 CRLF/LF 转换警告。前序核心专项证据保持有效：SQL Server `ExecuteMultiple` net6/net8 `32/32`、SQLite `SqliteExecutionIntegrationTest` net6/net8 `252/252`、`SqlQueryApiContractTest` net6/net8 `30/30`、`BingSql002AnalyzerTest` net8 `10/10` 均通过；本轮未修改主工作区生产或测试代码，未重复运行这些专项。

### Round 4 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | PARTIAL | before worktree 的提交、路径和仅限 harness 的临时差异已可验证，但 Root 正式测量未完成且 Join 未启动，不能关闭性能验收。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite 2-7 Fluent/Text 真实执行和生命周期专项的前序独立证据仍有效。 |
| FIX-003 | MUST_FIX | PARTIAL | Round 3 provenance 缺陷尚未被完整替代 artifact 消除；Round 4 不完整 CSV 不能作为 before。 |
| FIX-004 | SHOULD_FIX | RESOLVED | Runtime Executor/Builder Source 职责分离及 API 契约专项证据仍有效。 |
| FIX-005 | SHOULD_FIX | RESOLVED | 五个 Provider 的离线完整 SQL contract 已存在；真实外部 Provider 执行保持环境门控。 |
| FIX-006 | SHOULD_FIX | RESOLVED | Analyzer source span 直接断言和专项结果仍有效。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：`RC-P5-01` 可重复性能基线、`RC-P5-04` 性能复审。
- 涉及文件/产物：
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - 独立 detached before worktree 与当前 after worktree 下的 FormalHost artifacts。
- 问题：Round 4 解决了 before worktree 的来源隔离，但 Root 只产出一个 `NA` 的未完成 CSV 行，Join before 从未运行。完整参数键的 before/after delta、统计告警处置和性能准入结论均缺失。
- 证据：before `HEAD` 为 `142380be3ec62cbd4a26cde8e2795d0eacae47fb`；Round 4 Root CSV 为 `1` 行、Join CSV 不存在；after Root/Join CSV 为 `72`、`36` 行。任务运行时为 `PARTIAL` 且无残留 benchmark 进程。
- 影响：RC 不能证明 API 和生命周期加固未造成性能或资源回归；发布材料不得声称 FormalHost 无回归或近零分配。
- 修复目标：建立来源、工具链、输出位置和输入矩阵均可验证的完整 Root/Join FormalHost before/after 数据，并按预设阈值给出可复核的准入结论。
- 明确修复要求：
  - 从 fresh detached `142380be` before worktree 运行，只应用已记录的 harness parity：`FormalHost` job、Root 方法名和 Root/Join 参数矩阵。记录修改前后 `git status --short`、`git rev-parse HEAD`、绝对 worktree/输出/artifact 路径、启动目录、完整命令和 SHA-256。
  - 让 Root `72/72` 和 Join `36/36` FormalHost 运行自然完成。before/after artifact 与 build/output 必须位于各自 worktree，不能复用当前工作区二进制、`output` 或 artifacts。
  - 以 `Method + 全部 Params` 为键输出每个 case 的 Mean、Median、Error、StdDev、Allocated、Gen0、Gen1、Gen2 delta；缺失字段必须显式为 `NA`，不得折叠参数维度。
  - 在 `benchmark-report.md` 应用既有不低于 `10%` 的 Mean/Allocated 候选阈值。对 MultimodalDistribution、outlier 和 process 警告进行隔离重跑、重复采样或给出有证据的接受理由；超过阈值且误差区间不重叠的项必须性能修复、重跑确认或明确拒绝合入。
  - 将 Round 3 artifact 清楚标记为无效历史证据；禁止将 Dry、不同 Job 或不完整 CSV 作为 FormalHost 基线。
- 修复后的验证方式：提交、源文件符号、启动目录和 artifact hash 可互相印证；Root `72/72`、Join `36/36` before/after case 完整匹配；报告包括环境指纹、完整 delta、阈值和告警处置，以及明确的接受或不合入结论。

## 结论

**NEEDS_FIX**。Round 3 已补充 Root/Join FormalHost CSV 并定义 10% 阈值，但 Root before artifact 的实际方法名和构建输出路径与声明的 detached `HEAD=142380be` 不一致，不能证明其来自改动前源码；因此 before/after delta 不可作为 RC 性能验收依据。其余上一轮已解决的生命周期和 Runtime SPI 专项复跑通过。

## 复审边界

- 依据：`plan.md`、`execution.md`、上一轮 `review.md`、当前 Git Diff、目标源码、测试、Benchmark artifact 和本轮独立命令结果。
- 本轮仅更新本文件，未修改生产代码、测试、`plan.md`、`execution.md`，未执行提交、推送或破坏性 Git 操作。
- 当前未提交变更集中于本任务的 SQL API、生命周期、Analyzer、测试、Benchmark 和迁移文档；未发现与本任务无关但由本轮 Reviewer 引入的变更。
- 未发现工具输出中的提示注入或要求扩大审查范围的内容。

## 上一轮 FIX 复审

| Fix | 处理要求 | 本轮状态 | 复审结论 |
| --- | --- | --- | --- |
| FIX-001 | MUST_FIX | RESOLVED | `ExecutionLeaseFactory` 提供可控 lease disposal seam；sync/async 多结果集覆盖 reader、rollback、错误/完成 Hook、lease 同时失败，断言聚合顺序及一次性释放。独立运行 `ExecuteMultiple` net6/net8 `32/32` 通过。 |
| FIX-002 | MUST_FIX | RESOLVED | SQLite `SqliteExecutionIntegrationTest` 独立复跑 net6/net8 `252/252` 通过，覆盖 2-7 Fluent/Text 路径和错误/取消边界。 |
| FIX-003 | MUST_FIX | NOT_RESOLVED | Round 3 生成了 CSV 和 delta，但 Root before log 运行 `BuildRootsAndRender`，而声明的 `142380be` 源码只有 `SetRootsAndRender`；日志的输出目录也为当前工作区而不是 detached worktree，artifact 来源不可追溯。 |
| FIX-004 | SHOULD_FIX | RESOLVED | `ISqlQueryPlanExecutor` 不再继承 `ISqlQueryBuilderSource`，`SqlQueryBase` 显式提供两项职责；Runtime API contract net6/net8 `30/30` 通过。 |
| FIX-005 | SHOULD_FIX | RESOLVED | MySQL、PostgreSQL、Oracle、SQLite、SQL Server 均增加离线完整 SQL contract；报告记录的专项结果分别为 `22/22`、`10/10`、`6/6`、`8/8`、`2/2`。外部数据库真实执行仍合法门控跳过。 |
| FIX-006 | SHOULD_FIX | RESOLVED | `BingSql002AnalyzerTest` 断言 `diagnostic.Location` 的 source line/column 起点；独立专项 `10/10` 通过。 |

## 计划验收矩阵

| 范围 | 结论 | 实际证据 |
| --- | --- | --- |
| BINGSQL002 Analyzer | PASS | 消息改为现有 `SqlInterpolated(...)`；source span 起点测试存在，专项 `10/10` 通过。 |
| MultipleQuery 生命周期 | PASS | `SqlMultipleQueryResult` 无 `.Result`、`.Wait()`、`.GetAwaiter().GetResult()`；组合失败、取消、跨 sync/async Dispose 和一次性释放由 SQL Server `32/32` 覆盖。 |
| Breaking API 删除 | PASS | `OperationId`、`Group(...)`、`SetRoots(...)`、目标重复终结转发已从源码/Public API 删除，反射负向契约仍存在。 |
| Runtime SPI | PASS | Executor/Builder Source 职责解除继承，QueryPlan 不公开 Builder、连接或事务；Runtime API contract net6/net8 `30/30` 通过。 |
| 1-10 根来源与 Join | PASS | Root/Join Benchmark 使用公开 Lambda 路径，Root 1/2/5/10 为类型化 `From<TEntity>()`，20/50 明确是原始表压力场景。 |
| Fluent/Text 2-7 映射 | PASS | SQLite 集成专项 net6/net8 `252/252` 通过。 |
| Provider SQL contract | PASS | 五个 Provider 的离线完整 SQL 合同已补齐；外部真实数据库 Gate 按计划声明为环境阻塞，而非伪造通过。 |
| FormalHost 性能基线 | FAIL | Root/Join CSV 具有相同 Job 和矩阵，但 Root before 与声明 before commit 的 benchmark 符号不一致，无法证明比较的是改动前源码；按 Method 与全部参数键比较，24 个超过 10% 的增长项中有 22 个具有不重叠 Error 区间，不能以采样误差关闭。 |
| 文档与任务证据 | PARTIAL | `benchmark-report.md` 已记录阈值和告警，但错误宣称 Root before 来自 `142380be` detached worktree，需在重新采样后更正。 |

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 当前状态：OPEN
- 对应计划项：RC-P5 性能基线与发布验收；上一轮 FIX-003。
- 涉及文件：
  - `ai_docs/tasks/BING-SQL-RC-HARDENING-20260825-001/benchmark-report.md`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs`
  - `framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaJoinBenchmarks.cs`
  - 可追溯的 FormalHost benchmark artifact。
- 问题：Round 3 的 Root before artifact 无法证明来自声明的改动前提交。`git show 142380be:framework/tests/Bing.Data.Sql.Benchmarks/SqlLambdaRootBenchmarks.cs` 显示基准方法为 `SetRootsAndRender` 且没有 `SimpleJob`；但 `review-fix-round3-before-root` 的 CSV/log 记录 `BuildRootsAndRender` 和 `FormalHost`。同一日志的 Benchmark 子进程工作目录是 `E:\Bing_Framework\Bing.NetCore\output\release\net8.0\...`，不是声明的 `..\Bing.NetCore-before-BING-SQL-RC-HARDENING-20260825-001` worktree。
- 证据：Root before/after CSV 的 Job、Runtime 和 case 集合表面一致，但 symbol/输出路径冲突使 before provenance 失效。以 `Method + 全部 Params` 为 key 的独立 CSV 比较显示 Root `18/72`、Join `6/36`，合计 24 个 Mean 增长超过 10%；其中 Root 18 个和 Join 4 个、合计 22 个 before/after Error 区间不重叠，不能以报告中的 MultimodalDistribution/outlier 告警单独接受。
- 影响：RC 无法证明 API/生命周期改动未引起性能回归，也无法对 10% 阈值作出可信的接受或拒绝结论。
- 修复目标：从可验证的改动前源码重新建立 Root/Join FormalHost before，维持同机、Runtime、BenchmarkDotNet、Job 和 case 集合一致，并对真实 delta 和统计告警给出结论。
- 明确修复要求：
  - 在 detached before worktree 内修改或临时应用仅用于测量的 Benchmark harness，使 before 和 after 使用完全相同的 `FormalHost` Job、方法名和 case 矩阵；不得在当前工作区或共享 `output` 目录构建 before。记录 `git rev-parse HEAD`、worktree 绝对路径、命令、启动目录和每个 artifact 的 SHA-256。
  - before/after 使用不同且位于各自 worktree 下的 artifacts/output 目录，运行前后分别记录 `git status --short`，确保 before worktree 除明确记录的 harness 变更外清洁；不要将当前修改后的二进制或 artifact 复制为 before。
  - 将 before/after CSV 或 Markdown artifact 写入任务可追溯位置；以 `Method + 全部 Params` 为 key 逐 case 计算 Mean、Median、Error、StdDev、Allocated、Gen0/1/2 delta，明确列出 `NA` case 而不是折叠参数维度。
  - 在 `benchmark-report.md` 定义并应用回归阈值；对 MultimodalDistribution、outlier 或 benchmark process 警告以隔离环境重跑、重复采样或有证据的接受理由处理。任何超过阈值且误差区间不重叠的项必须修复或明确不合入。
  - 禁止将 Dry 或不同 Job 的结果作为 FormalHost before。
- 修复后的验证方式：Root 和 Join FormalHost 均完成可复核、可哈希验证且符号/提交一致的 before/after artifact；报告包含环境、提交和 worktree 指纹、完整参数 key、delta、阈值、告警处理和最终性能结论。

## Git 与构建验证

- `git diff --check`：通过；仅输出 CRLF/LF 转换提示，无空白错误。
- `dotnet test framework/tests/Bing.Dapper.SqlServer.Tests/Bing.Dapper.SqlServer.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ExecuteMultiple" --nologo`：net6/net8 `32/32` 通过。
- `dotnet test ...Bing.Dapper.Sqlite.Tests.Integration... --filter "FullyQualifiedName~SqliteExecutionIntegrationTest"`：net6/net8 `252/252` 通过。
- `dotnet test framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SqlQueryApiContractTest" --nologo`：net6/net8 `30/30` 通过。
- `dotnet test ...Bing.Data.Sql.Analyzers.Tests... --filter "FullyQualifiedName~BingSql002AnalyzerTest"`：net8 `10/10` 通过。
- `dotnet build framework/src/Bing.Data.Sql/Bing.Data.Sql.csproj -c Release --no-restore`：通过，保留既有 `56` 个 RS0026/RS0027 Public API Analyzer 警告。
- `dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj -c Release --no-restore`：通过。

## 性能与资源 Review

Root 和 Join artifact 的 Job、Runtime 和参数矩阵已使用 FormalHost，且 CSV 可逐项匹配；这解决了上一轮的配置缺失，但不足以解决来源证明。Root before log 是 `BuildRootsAndRender`，而 `142380be` 的 Root 源码是 `SetRootsAndRender`。日志还显示 before Benchmark 子进程从当前 workspace 输出目录运行，因此它不是该 detached commit 的可验证测量。

以 `Method + 全部 Params` 比较实际 CSV，Root `18/72`、Join `6/36`，合计 24 个 Mean 增长超过 10%，其中 22 个 before/after Error 区间不重叠，包括 Root `CreateExecutionSnapshot` RootCount 50、ParameterCount 1000 `+48.07%`，以及 Join `WhereIfFalse` JoinCount 1 `+39.24%`。报告仅记录多峰/离群告警，未以可重复采样或接受证据处理这些项。由于 before 来源无效，本轮不将这些数值定性为代码回归，但它们足以阻止验收。

## 回归与兼容风险

- `Bing.Data.Sql` 仍有 56 个 RS0026/RS0027 Public API Analyzer 警告；本轮构建成功且执行报告认定为既有基线，未发现 Round 2 新增的编译失败。
- 外部 Provider 真实数据库执行未配置，保持 `GATE_SKIPPED`。离线 SQL contract 降低格式化回归风险，但不替代真实连接/执行链路验证。
- FormalHost before artifact 与声明提交不一致，且候选增长尚未经可复现采样处置，不能作为发布无回归证明。

## 验收 Checklist

- [x] 已复审 Round 2 的全部 FIX。
- [x] 已独立验证 MultipleQuery 生命周期专项 net6/net8 `32/32`。
- [x] 已独立验证 SQLite 2-7 映射专项 net6/net8 `252/252`。
- [x] 已独立验证 Runtime API contract net6/net8 `30/30`。
- [x] 已独立验证 Analyzer source span 专项 net8 `10/10`。
- [x] 已确认 Root 与 Join FormalHost after artifact。
- [x] 已确认核心项目构建通过。
- [ ] 已提供来源可验证的 FormalHost before artifact、完整参数 delta 和回归阈值处置。

最终状态：`NEEDS_FIX`。