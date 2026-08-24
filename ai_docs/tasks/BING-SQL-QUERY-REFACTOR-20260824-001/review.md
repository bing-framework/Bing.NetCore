<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: BING-SQL-QUERY-REFACTOR-20260824-001
AI_REVIEWED_AT: 2026-08-24T16:57:29.3754969+08:00

# Review Fix Round 5 复审报告

## 验收摘要

**结论：PASS_WITH_ISSUES。** 上一轮 Round 4 的 `FIX-001` 已通过本轮再次独立复核。二元显式 alias 的列投影、DTO 投影、`SelectSubquery`、`Where`、`GroupBy`、`OrderBy` 与 `Having` 已统一通过 `ResolveTwoSources<TFirst,TSecond>` 解析来源；当两个参数解析为同一 `TableSource` 时，在进入 Core mutation 前抛出。七个重复 alias 直接 Unit、不同 alias 的跨 Clause 完整 SQL 正向测试及本轮跨项目回归全部通过。

本轮未发现新的 BLOCKER、HIGH、MEDIUM 或未解决的 MUST_FIX。原计划仍有大文件职责拆分、外部 Provider Integration Gate 和同机前后 Benchmark 基线三项非阻断遗留，因此不判定为无条件 `PASS`。

本轮未修改业务代码、测试代码、`plan.md` 或 `execution.md`；仅更新本报告。

## Review 边界

- 已读取 `plan.md`、当前 `execution.md`、上一轮 `review.md`、Git 工作区差异、相关生产源码、直接测试、Public API/追溯文档和仓库规范。
- 当前工作区有 45 个状态项，未暂存差异涉及 40 个文件、约 2106 行新增和 799 行删除；整体属于本任务长期 API 收敛、Runtime、Provider、测试、Benchmark 与文档范围。本轮 Round 4 的直接修改集中在非泛型 façade、生命周期测试和追溯文档。
- 未发现 Round 4 引入计划外行为变化；本轮没有新增生产或测试差异，复核证据来自当前源码、当前测试和当前构建。
- `git diff --check` 无 whitespace error；CRLF/LF 输出为工作树转换提示。

## 上一轮 FIX 复核

| 上一轮问题 | 本轮状态 | 独立证据 |
| --- | --- | --- |
| `FIX-001`：七个二元显式 alias 入口可能将两个 Lambda 参数绑定到同一 `TableSource` | RESOLVED | 七个入口均调用 `ResolveTwoSources<TFirst,TSecond>`；源码搜索未发现旧的独立解析旁路。根表来源来自 `FromClause.Sources`，正常类型化 Join 在提交时保存稳定 `JoinItem.Source`，克隆也保留来源对象语义；`ReferenceEquals` 能识别同一物理来源。七个重复 alias 测试、完整 SQL 正向测试和本轮专项 `134/134`、Data.Sql `2514/2514` 均通过。 |

## 计划验收矩阵

| Phase / Task | 状态 | 本轮证据 |
| --- | --- | --- |
| P2-T03 显式 alias 与来源解析 | PASS | 同类型多来源要求显式定位；七个二元入口统一拒绝重复来源，不依赖 Lambda 参数名或来源插入顺序。 |
| P4-T02 1～10 表 Unit SQL 矩阵 | PASS | Data.Sql Unit 在 net6.0/net8.0 合计 `2514/2514` 通过；Round 4 新增完整 SQL 正向与七个原子失败测试。 |
| P4-T03 SQLite 真实执行矩阵 | PASS | SQLite Unit `222/222`，SQLite Integration `284/284`。 |
| P3-T02 大文件职责拆分 | PARTIAL | `SqlLambdaQuery.NonGeneric.cs` 等职责密集文件仍未完成计划中的全部物理拆分；未发现由此导致的新正确性缺陷。 |
| P4-T04 外部 Provider 共享集成 | NOT_VERIFIABLE | 未提供受控 MySQL/PostgreSQL/SQL Server/Oracle 外部数据库 Gate；本轮未猜测凭据或连接生产数据库。 |
| P5-T02 Benchmark 与数据驱动优化 | PARTIAL | 缺少同机、同环境、可比较的旧/新统计基线，无法确认数据驱动性能优化目标全部完成。 |

## 功能与真实接入 Review

- `ResolveTwoSources<TFirst,TSecond>` 先调用现有严格 alias 解析，再比较两个来源对象身份；异常发生在 `SelectCore`、`SelectTypedCore`、`SelectSubqueryCore`、`WhereCore`、`GroupByCore`、`OrderByCore` 或 `HavingCore` 之前。
- 接入范围完整覆盖上一轮列出的七个公开二元 alias 入口，源码中共有七个调用点和一个共享方法定义；未发现继续独立解析 `firstAlias/secondAlias` 的同类公开入口。
- `SqlLambdaQueryCore.GetBoundSources` 仅复制来源集合，集合元素仍是查询图中的原对象；根表来源和正常类型化 Join 来源均保持稳定对象身份。同一 alias 两次解析会返回同一引用，不同 From/Join 来源则保持不同引用，当前对象身份检查与物理来源契约一致。
- 失败测试断言完整 SQL、参数、缓存值、缓存版本和 `ShapeVersion` 不变。来源解析本身是只读路径，且失败发生在任何 Core/Builder mutation 前，源码证据补足了来源图不变性。

## API 与契约 Review

- 本轮只新增私有共享解析方法，没有扩大 Public API 表面，也未引入第二套来源解析契约。
- 正向测试使用任意 Lambda 参数名和不同显式 alias，证明来源绑定不依赖参数名称。
- 重复 alias 现在明确失败，行为与无 alias 的 `ResolveSources`、条件组的已选来源排除规则一致。
- Analyzer 回归 `25/25` 通过，未发现 Public API 基线退化。

## 架构与维护性 Review

- 来源选择继续由非泛型 façade 负责，Core 只消费已验证的 `TableSource` 列表，符合计划中的职责边界。
- 将七个入口收敛到单一共享不变量，降低平行 API 再次漂移的风险。
- `SqlLambdaQuery.NonGeneric.cs` 仍较大，P3-T02 保持 `PARTIAL`；该遗留不构成本轮新修复项，避免复审扩大到非必要重构。

## 性能与资源 Review

- 新路径每次二元调用只增加一次引用比较和固定两元素来源集合，没有引入 IO、全局缓存或资源持有。
- 本轮改动不在 SQL 热路径引入额外全图扫描；单来源解析沿用既有实现。
- 未运行 Benchmark。当前缺少同机旧/新可比较基线，P5-T02 保持 `PARTIAL`，不能据此宣称性能优化完成。

## 测试 Review

| 验证 | 独立结果 |
| --- | --- |
| `SqlQueryLifecycleTest` 专项 | PASS；包含 Round 4 新增测试，构建和执行成功。 |
| `Bing.Data.Sql.Tests` Release | PASS，net6.0/net8.0 合计 `2514/2514`。 |
| `Bing.Data.Sql.Analyzers.Tests` Release | PASS，`25/25`。 |
| `Bing.Dapper.Sqlite.Tests` Release | PASS，net6.0/net8.0 合计 `222/222`。 |
| `Bing.Dapper.Sqlite.Tests.Integration` Release | PASS，net6.0/net8.0 合计 `284/284`。 |
| `Bing.Dapper.SqlServer.Tests` Release | PASS，net6.0/net8.0 合计 `564/564`。 |
| `dotnet build .\Bing.All.sln -c Release -nologo -v minimal` | PASS，0 error，83 warnings。 |
| `git diff --check` | PASS，无 whitespace error。 |

测试矩阵覆盖正常、边界和负例：不同 alias 生成完整 SQL；七个入口传重复 alias 均 fail-fast；现有无 alias 歧义、Provider SQL、参数元数据与 SQLite 真实执行回归未退化。

## 文档 Review

- `ai_docs/sql-metadata-test-traceability.md` 已增加 `ResolveTwoSources<TFirst,TSecond>` 到直接测试方法的映射，并记录七个重复 alias 负例。
- 追溯内容与当前生产符号、测试方法名称一致。
- 本轮没有新增公开 API，因此无需额外迁移文档或 Release Notes 条目。

## 问题分级

### BLOCKER / HIGH / MEDIUM

无。本轮没有新的 MUST_FIX，不生成新的 `FIX-xxx`。

### LOW

- 全方案 Release 构建仍有 83 条既有警告，其中 Data.Sql 相关构建包含 RS0026/RS0027 optional-overload 警告。未发现由 Round 4 私有解析方法新增，也未通过关闭 Analyzer 规避。
- 多个工作树文件存在 CRLF/LF 转换提示；`git diff --check` 未报告格式错误。
- `get_errors` 对本报告报告 MD041，是因为 Review 机器协议要求前三行必须是 HTML 状态标记、任务标识和时间戳；该协议优先于 Markdown 首行标题规则，不影响源码和测试验收。

## 未完成与偏离项

- P3-T02：大文件职责拆分仅部分完成。
- P4-T04/P6-T03：外部 Provider Integration 因缺少授权 Gate 不可验证。
- P5-T02：缺少同机前后 Benchmark 基线，数据驱动优化目标仅部分完成。

上述项目均为原计划已记录遗留，没有证据表明它们由 Round 4 修复引入或形成当前正确性阻断。

## 回归与兼容风险

- 依赖重复 alias 将两个 Lambda 参数绑定到同一来源的调用现在会抛出异常；这是批准计划要求的 fail-fast Breaking Change，不应恢复静默绑定。
- 外部数据库 Provider 的真实连接执行仍未在本轮验证；SQLite 本地集成和 SQL Server Unit 不能替代外部 Gate。
- 工作区仍是大规模未提交任务差异，提交前应保持当前全方案构建与测试证据，并单独处理既有警告治理。

## 最终 Checklist

- [x] 已读取计划、执行报告、上一轮 Review、Git Diff、源码、测试、追溯文档和仓库规范。
- [x] 已优先验证上一轮 `FIX-001`，结论为 `RESOLVED`。
- [x] 七个二元显式 alias 入口均接入共享解析且无同类旁路。
- [x] 重复来源在 mutation 前失败，SQL、参数、缓存与 `ShapeVersion` 保持不变。
- [x] 已独立通过专项 `134/134`、Data.Sql `2514/2514`、Analyzer `25/25`、SQLite Unit `222/222`、SQLite Integration `284/284`、SQL Server Unit `564/564` 和全方案 Release Build。
- [x] 已执行 `git diff --check`，无 whitespace error。
- [x] 追溯文档与生产符号、直接测试一致。
- [x] 未修改业务代码、测试代码、`plan.md` 或 `execution.md`；未执行提交、推送或破坏性 Git 操作。
- [ ] 外部 Provider Integration Gate 已验证。
- [ ] 大文件全部完成职责拆分。
- [ ] 已建立同机前后 Benchmark 基线并完成数据驱动优化结论。

上一轮 `FIX-001` 已关闭，当前没有新的 MUST_FIX。最终结论为 `PASS_WITH_ISSUES`。
