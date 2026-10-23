<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261021-sql-multidb-unit-of-work
AI_REVIEWED_AT: 2026-10-21T09:57:16+00:00

# Review Round 3 独立验收

REVIEW_MODEL: GPT-6.1-Sol
REASONING_EFFORT: high
REVIEW_ROUND: 3
FIX_ROUNDS_COMPLETED: 2
OPEN_ACTIONABLE: 0
CLOSED_COUNT: 9
MUST_FIX_COUNT: 0
SHOULD_FIX_COUNT: 0
OPTIONAL_COUNT: 0
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS
PERFORMANCE_EVIDENCE_STATUS: PASS
RESOURCE_EVIDENCE_STATUS: PASS
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
THRESHOLD_STATUS: BLOCKED_APPROVAL
RELEASE_STATUS: BLOCKED
GOAL_STATUS: STOPPED_BLOCKED

独立结论为 **PASS**。FIX-004 与 FIX-009 已按真实调用链、维护测试和本轮独立 Release 验证闭环；原七项 CLOSED 保持。当前九项 Finding 全部 CLOSED，**没有 OPEN_ACTIONABLE**，完整计划 T01–T08 验收 8/8（T07 的本轮测试配置差异按下文 DEVIATED_OK 明确记录）。外部服务和正式预算仍阻止发布门禁全通过，不触发下一轮修复。

## 输入、历史与候选

- 已读 AGENTS.md、C:/Users/jianx/.codex/GOAL_RULES.md、.agents/skills/review-code/SKILL.md。完整 PLAN_READY_REVISED 沿用本会话独立审查依据；本轮重新对照更新 execution 的 ReviewFix2、migration/traceability、review-fix-round-2 的 index/CIA/starting-state、选定原始日志/TRX 与真实源码/差异。
- 候选为 HEAD `5065c38ced05a95088e17d89d96c9a1610e37159` 加保留的 F008 共存工作区及 Fix Round 2，并非独立 commit。按原 initial-worktree-status/candidate-manifest 保护 F008、merged audit 与所有先存变更，没有将旧变更误归本次或要求回退。
- 更新当前报告前已逐字节保存 [Review 2 原件](artifacts/review-round-3/review-round-2.md)，SHA-256 `C47F0B40CFDAE2C5E618571A680ED41505EE10450B9FB150CA6B71EA5E69CA65`，与执行者 review-sha256.txt 一致。Review 1 快照及旧失败/性能产物均保留。
- GPT-6.1-Sol/high 在独立上下文审查本轮两个修复及直接回归，详细源位置、断言、CIA、原始命令和验证见 [独立 Sol 报告](artifacts/review-round-3/sol-review.md)。协调审查侧核对原始证据、候选及配置，见 [证据范围说明](artifacts/review-round-3/evidence-audit.md) / [逐格解析](artifacts/review-round-3/evidence-audit.json)。
- 本轮只写 review.md 和 artifacts/review-round-3。没有改生产、测试、plan、execution/migration/traceability、API baseline 或版本，没有暂存/提交/推送/PR、外部通知。无条件全矩阵、旧 repro/probe 和历史 baseline 均未重跑。

## 本轮两项复验

### FIX-004 — CLOSED

- 实现：SqlQueryBase.UnitOfWork.cs 的 `AttachUnitOfWorkResourceForExecution` 只包装实际资源 attach/provider 准入，两个首次/重试 attach 调用点均经过它。root 已绑定且活动时标记 rollback-only，随后裸 `throw` 保留原异常；已有资源的 AcquireExecutionLease、提前描述/参数验证、显式独立分支在该 catch 外，不被笼统毒化。
- 维护回归：`QueryFirst_MappedAdmissionFailureAfterGoodWrite_ShouldPoisonRoot(asynchronous=false/true)` 使用两个真实 SQLite 文件，good 先写→bad mapped Query-first 物理身份拒绝→业务 catch→root rollback-only→Complete 拒绝→外部两文件均0；错误 context connection 保持 Closed。最新完整 EF165 和本轮 fresh Release 两例均实际通过。
- 独立边界：新最小诊断进一步核对原异常实例保持、租约冲突不误标 rollback-only；具体输出、源位置和边界覆盖范围见 Sol 报告。未以“抛异常即可”替代失败后的实际事务行集判断。
- 结论：原准入拒绝和剩余执行失败传播均闭环，无剩余验收缺口。

### FIX-009 — CLOSED

- 实现：SqlExecutorBase.ExecuteMutationBatch/Async 先获取本实例/root资源租约；参与时使用本 Executor 的冻结配置与资源，内部命令走“已持租约”私有路径避免重复获取，成功不创建/Commit内部独立 scope。批次整体 finally 释放租约，命令/取消/受影响行数失败使参与 root rollback-only。Insert/Update/Delete 及 UseTransaction 选项共用该路径。
- 维护回归：`MutationBatch_ShouldUseAmbientRootTransaction` 的24组合覆盖 Insert/Update/Delete × sync/async × UseTransaction true/false × Complete/Rollback；断言根完成前外部不可见、回滚保持原始行集、完成后正确行集、同源 Query 共享和实际 Begin/Commit/Rollback次数。错误、执行中/预取消、受影响行数失败均有 catch 后禁止 Complete 的直接测试。
- mapped EF：`MappedEfExecutorBatch_ShouldUseFrozenRootResourceAndCommitOnce` 的 sync/async × Complete/Rollback 四例，以同一映射 context/冻结源及同源 Dapper Query 断言唯一物理事务、外部可见性和关闭/释放计数。本轮 fresh Release 四例实际通过。
- 旧边界：无 Uow batch 继续原自有事务；显式 Owned 和调用方主动 SQL transaction scope 保持独立。非事务 root 不能满足 UseTransaction=true，执行前明确拒绝且 rollback-only；独立诊断确认该边界。UseTransaction=false 在事务 root 内继续参与，不能绕过 root。
- 结论：默认批量不再逃逸 root 回滚，root 为唯一物理提交者；无须改变明确独立所有权或新增公共 API。

## 全部 Finding 当前分类

| ID | 状态 | 本轮依据/复用边界 |
| --- | --- | --- |
| FIX-001 | CLOSED | 保持 Review2 默认 standalone 拒绝、显式独立保留；本轮新增 batch 同样经过既有亲和入口。 |
| FIX-002 | CLOSED | 保持 Review2 全入口预取消语义；本轮 batch 取消/预取消回归验证失败保护，不改变无关入口。 |
| FIX-003 | CLOSED | mapped EF 终态首次保存与 late Commit 既有关闭保持；EF当前完整项目涵盖原回归。 |
| FIX-004 | CLOSED | 本轮 attach 失败传播、实际双文件回滚及原异常/边界验证闭环。 |
| FIX-005 | CLOSED | 内部可信 opaque 借用与真正外部校验未改，按原已确认职责复用。 |
| FIX-006 | CLOSED | 映射注册、双向冲突/自定义注册未改，按原已确认职责复用。 |
| FIX-007 | CLOSED | Provider/EF consumer 职责代码未改；当前 Core/SQLite/EF验证本轮共享路径影响，其余按 scope 复用。 |
| FIX-008 | CLOSED | 严格复用 Review2 probe-confirmation-001 的直接退出码0/0与16cells；本轮无probe运行。 |
| FIX-009 | CLOSED | 本轮默认批量同步/异步、选项、失败与 mapped owner/非事务/独立边界闭环。 |

本轮未新增 OPTIONAL 或其他可执行 Finding。没有重开 native FreeSQL/CAP、远端服务、跨库非原子或旧 stream parser。

## T01–T08 完整验收

| Task | 状态 | 本轮结论 |
| --- | --- | --- |
| T01 | PASS | 候选/起始脏状态与历史快照明确，先存 F008/merged audit 保留。 |
| T02 | PASS | 无变化 companion/frame/状态机/资源和注册职责沿用已确认独立证据。 |
| T03 | PASS | 原资源键/owner/opaque准入保持；004关联失败完整传播，补齐剩余缺口。 |
| T04 | PASS | 默认batch所有公共路由参与、单一物理提交者和失败清理闭环，其他已确认入口未扩审。 |
| T05 | PASS | 两真实SQLite文件、两TFM当前执行结果与受控Provider原证据成立；不冒充远端异构执行。 |
| T06 | PASS | mapped Query-first失败和mapped batch sync/async由当前EF165及fresh6例验证，原ORM/consumer边界保持。 |
| T07 | DEVIATED_OK | 本轮Core/SQLite完整与31例原始日志为Debug；EF165为Release。独立Release精准SQLite35/EF6重新构建验证本轮修改。按局部修复CIA复用未变矩阵/consumer及probe，不声称新完整Release全矩阵。 |
| T08 | PASS | 更新交付已对照实际符号/最新TRX，本报告记录准确配置与当前独立状态，历史不覆盖。 |

实施与本地验收 8/8 闭环；DEVIATED_OK 是本轮测试配置/范围证据的显式接受，不是把Debug改称Release或免除行为验证。

## 原始验证及有效范围

- Fix2最新选定完整结果：Core net6/net8 各179；SQLite net6/net8 各229 passed + 1 gate skip，日志均Debug。EF net6最新 `full-ef-net6-003/trx/full-ef-net6-003.trx` 为Release165；旧162/163仅历史，不混用。
- Fix2精准结果：SQLite两TFM各31（Debug）；admission sync/async 2（Release）；mapped batch sync/async × Complete/Rollback 4（Release）。九格均exit0；Round2精确追溯八个方法均在所选TRX存在。Core/SQLite六格未保留独立 command.txt，未虚称完整原始Release命令。
- Reviewer fresh：先CIA，再显式 `dotnet test ... -c Release`，重新构建当前源码；SQLite net8 **35/35**、EF net6 **6/6**，均非零匹配、0skip/0failed、直接exit0。原始命令/日志/TRX见 [SQLite](artifacts/review-round-3/sol-targeted-001/sqlite/command.json)、[EF](artifacts/review-round-3/sol-targeted-001/ef/command.json)。另有 [最小边界诊断](artifacts/review-round-3/sol-boundary-001/run-stdout.txt) **6/6**，直接exit0，覆盖原异常实例、租约冲突不poison、非事务root要求事务拒绝各sync/async；首次临时harness命名空间编译失败原件保留，补using后build002通过，仅一次行为运行。未改被审源码测试。
- 本轮所选EF及fresh验证日志确认生产Dapper/EF重新构建；其余未变consumer/API/依赖图的既有构建证据按scope复用，未宣称四项build本轮重跑。
- FIX008复用 [probe-confirmation-001](artifacts/review-round-2/probe-confirmation-001/watchdog-result.json)：直接host/child0、16cells、60秒未超时、连接1554/物理事务314/open0/peak2。1,200.7183ms为上轮候选历史实测；batch不在该工作量内，本轮成功attach增加的异常保护不改变资源算法。没有本轮性能新测或阈值承诺。
- 外部数据库/CAP/Redis仍门控；性能阈值BLOCKED_APPROVAL。跨库本地顺序提交不原子的已定边界不属于剩余实现问题。

```powershell
python ai_docs/tasks/20261021-sql-multidb-unit-of-work/artifacts/review-round-3/audit-evidence.py
git diff --check
git diff --cached --quiet
```

实际测试完整参数保留在上述JSON及Sol诊断artifact；没有重跑旧实验目录、无关全套、probe或baseline。

## 收口与停止

| 分类 | 当前项 | 动作 |
| --- | --- | --- |
| Completed | FIX-001–009全部CLOSED；T01–T08验收8/8 | 交父会话交付。 |
| Open Actionable | 无（0） | STOP，不进入Fix3。 |
| Blocked Approval | 正式性能/资源预算 | 只保留门禁，不自行批准。 |
| Blocked External | 远端Provider/CAP/Redis互操作 | 专用受控环境后验证，不用本地结果冒充。 |
| Not Applicable | 新Uow历史等价before、生产schema migration、旧stream parser本任务新增归因 | 不重基线、不扩展。 |
| Accepted Limitations | 无跨库原子性；nativeFreeSQL/CAP自管动作/未映射Shared/Independent不自动桥接；Complete不自动SaveChanges；同资源不并发/不重入 | 保留迁移约定，不启动修复。 |
| Verified Boundaries | 当前精准异常/租约/非事务batch边界；上轮有界probe观测与watchdog smoke | 仅按实测范围使用。 |
| Deferred | 本轮无新增延期项 | 无额外动作。 |

- Task/Round：20261021-sql-multidb-unit-of-work / Review3；Fix已2轮，最多3轮。
- Implementation：8/8（7 PASS + 1 DEVIATED_OK）；Finding关闭9/9。
- Release Gates：5/7闭环：本地正确性/职责、生产编译及未变consumer、双SQLite执行、EF桥接、probe证据；外部服务与正式预算分别BLOCKED_EXTERNAL/BLOCKED_APPROVAL。
- Changed Scope：仅review.md与新审查artifacts；受审代码/测试及交付正文不改，先存工作区保留。
- Validation Run：L0差异/协议/UTF8；L1 fresh Release SQLite35/EF6及最小边界诊断6/6；L2/L3复用本轮明确完整结果与未变consumer证据；L4不重跑；L5不运行，复用上轮probe。
- Reused Evidence：REUSED_UNCHANGED_SCOPE；原七项关闭及未变Provider/consumer/性能工作量；变化的004/009不靠旧成功关闭。
- No-Progress Check：CHANGED，两项剩余根因均修复并获得当前验证；无OPEN_ACTIONABLE，不以外部/审批继续循环。
- Next Action：STOP，交父会话交付收口；不执行下一轮Fix。
- Goal Status：STOPPED_BLOCKED（实现/本地验收PASS；仅发布外部及预算门禁未闭环）。
