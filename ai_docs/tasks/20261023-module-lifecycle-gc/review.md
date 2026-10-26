<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261023-module-lifecycle-gc
AI_REVIEWED_AT: 2026-10-23T17:48:41+08:00

# GC 诊断工具限定独立 Review 5

Reviewer：本线程 GPT-6.1-Sol / high，使用 review-code。依据[本次授权](continuation-authorization.md)，只验收 Review4 唯一剩余 FIX-003 的原 AC。已读适用 AGENTS、GOAL_RULES、当前 plan/execution/diagnosis/traceability、Review4、当前工具源码及含补交的交付证据；窄范围虚拟证据机械核对由 Luna worker 完成，控制流判断由主 Reviewer 负责。

**结论：FIX-003 CLOSED；本轮工具范围 OPEN_ACTIONABLE=0。FIX-001/002 保持 CLOSED。** 当前三个变更文件与 timeout-control 补交完成了最后准入、所有权异常清理和共享有界清理预算。此 PASS 只表示限定工具 AC 闭环，不能解释为生产 GC 修复或 Release 通过。

```text
REVIEW_ROUND: 5_LIMITED_USER_AUTHORIZED_TOOL_ONLY
IMPLEMENTATION_STATUS: PASS_DIAGNOSTIC_TOOL_SCOPE
TEST_STATUS: STATIC_AND_REUSED_FINITE_VIRTUAL_EVIDENCE_ACCEPTED
TOOL_FINDINGS_CLOSED: 3/3
FIX_001: CLOSED_REUSED
FIX_002: CLOSED_REUSED
FIX_003: CLOSED
OPEN_ACTIONABLE: 0
MUST_FIX: 0
SHOULD_FIX: 0
ROOT_CAUSE: NOT_DETERMINED
GC_PROBLEM: UNRESOLVED
FORMAL_BUILD_STATUS: PASS_REUSED
FORMAL_SOLUTION_STATUS: FAIL_REUSED
RELEASE_STATUS: FAIL
PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE_TO_TOOL_REVIEW
RESOURCE_EVIDENCE_STATUS: NO_NEW_PRODUCT_EVIDENCE
EXTERNAL_GATE_STATUS: NOT_RUN_IN_THIS_SCOPE
GOAL_STATUS: STOPPED_LIMITATION_TOOL_REVIEW_SCOPE
AUTO_CONTINUE: false
NEXT_ACTION: STOP_HAND_BACK_TO_MAIN_THREAD
```

## 范围与候选

更新前已按原字节保存 [Review4 不可变副本](artifacts/review-round-5/review-round-4.immutable.md)，SHA-256 `503bc7b3943332254dc606962f136214355edb92fbb6efbb48628c4082bd3789`。

审查的是当前 `orchestrate_capture.py`、`run_timeout_control.py`、`virtual_deadline_confirm.py`，以及 `review-fix-round-4/supplement/` 的最终补交；第一版遗漏 timeout-control 的交付不是最终候选。当前补交 manifest 七项的 hash/字节数均吻合，见 [候选关联核对](artifacts/review-round-5/current-candidate-association.json)。工具源码的 AST/实际 subprocess 调用见 [静态审计](artifacts/review-round-5/source-static-audit.json)。

[与 Review4 的范围比较](artifacts/review-round-5/review4-scope-comparison.json)确认 monitor/collector 未变，复用已接受的 collector 准入/清理合同；plan/diagnosis/traceability 未变，execution 的工具修正内容已重新阅读。限定三文件的工作区/暂存 Git diff 无输出，不能据此认定文件没有变化，实际以归档 hash 对照和当前真实源码判断。没有把整个 dirty 工作区或 HEAD 差异归成本轮修改。

本轮不审查、读取或冻结并行 Astra 调查的动态产物、新夹具或生产候选，不依据其进行中的进展更新 GC 状态。Stage B、父路线图和旧 raw 不改。

## FIX-003 原 AC 逐项验收

| 原 AC | 判定 | 当前调用链与直接依据 |
| --- | --- | --- |
| 1. build 准备完成后，实际 Popen 前最后准入 | PASS | `run_process:79–82`先打开日志，再执行 `before_start`，随后 Popen；`build_before_start:288–290`检查同一冻结 deadline，非正抛 DeadlineReached，main 记 TIMEOUT_NOT_STARTED、buildStarted=false。既有 build-preparation-overrun 虚拟场景对应零创建。 |
| 1. 创建后按实际剩余等待 | PASS | `run_process:84–89`先登记实际 PID/process，再由 timeout_provider 重取剩余；不再沿用日志准备前预算。若创建时已耗尽则 wait(0)进入超时清理，不获得新的等待许可。build/hash 越界后仍阻止 monitor/test 启动。 |
| 2. monitor 创建后的目录/日志准备异常覆盖完整所有权路径 | PASS（静态） | `:357–360`登记 monitor；`:364`开始的 try 包含 trx.mkdir、test 两个日志打开、准入/Popen及发布/等待；`:446–454`异常交 handle_error，finally 调 cleanup_created 并关闭流。目录或日志 OSError 因而清理已创建 monitor，保留原异常与实际 PID/started。不是只覆盖 test Popen 后的异常。 |
| 2. build 普通 wait 异常、正常退出 | PASS | `run_process:95–97`清理后原样重抛；main 的 on_build_started 已保存 buildProc/PID，`:302–303`走 handle_error，cleanup_created 包含 build。helper/poll 判断已退出后不重复 taskkill。既有 build-wait-error 验证 main 所有权路径；实际 run_process 内部分支另由源码核对，不将 fake_build 当该函数的直接运行证据。 |
| 2. test/PID 发布、准确状态、主异常 | PASS / REUSED | 创建后先登记再发布 PID；同一 try/finally 覆盖发布和普通 wait 异常。已有 monitor、尚无 test 时为 MONITOR_STARTED_TEST_NOT_STARTED；test 已创建再超时为 TEST_STARTED_TIMEOUT；零 monitor/test 为 TIMEOUT_NOT_STARTED，另列 buildStarted。注入 OSError 的虚拟证据仍保留原类型/消息。 |
| 3. 所有实际 Python taskkill 明确有界 | PASS | AST 确认 orchestrator 的唯一实际 subprocess.run（`:145–147`）和 timeout-control 的唯一实际 subprocess.run（`:41–43`）均传 timeout=正剩余；无预算不创建清理命令。以当前补交为准，旧无界版本已被替换。 |
| 3. 多个 owned PID 共用独立5秒清理剩余 | PASS | orchestrator main 共享同一 CleanupBudget；首次必要清理冻结一次5秒截止，run_process/helper/main 不重新授予预算；各 taskkill 后确认 wait 再取剩余。后续 PID 无余额记录 skipped-no-budget，不再启动命令。timeout-control 的45秒观察超时后单独创建5秒清理预算。 |
| 3. 清理失败/超时不覆盖原异常，不伪造确认退出 | PASS | terminate_process / terminate_created_process 捕获清理命令错误/超时；记录 cleanup-error/cleanup-timeout、commandIssued 与独立 exitConfirmed。确认依 process.poll，不由 issued 推定；预算耗尽如实记 skipped-no-budget。handle_error 最后 bare raise 保留原异常；standalone 仍 timedOut=true、exitCode=null。虚拟超时场景未假报已退出。 |
| 4. 固定合同及已通过边界保留 | PASS / REUSED | hook30秒、collector20秒、formal15分钟、timeout-control45秒未增加；独立5秒仅用于退出清理，不延长采证许可。monitor/collector 未改，已接受路径复用，不重做真实采集或新矩阵。 |

**FIX-003 状态由 OPEN_ACTIONABLE 转 CLOSED。** 关闭的是原诊断工具准入/所有权/清理 AC，没有新增生产结论或放宽正式 GC 断言。FIX-001/002 无本轮相关反证，保持 CLOSED；无新增 finding。

## 验证证据及限制

复用六个主工具假进程场景与两个 timeout-control 补充场景，两份原始 exit 均为0，驱动均设15秒外 watchdog、声明真实进程0。分别检查了驱动、stdout、confirm 和当前源码，不单凭 `contractSatisfied=true`。详见 [虚拟证据审计](artifacts/review-round-5/virtual-evidence-audit.json)。

- 主工具：build/hash越界、build准备越界、monitor后准备越界、PID发布错误、build等待错误、共享清理预算耗尽。
- 补交：45秒观察超时后成功清理确认，以及清理命令超时但原超时结果保留。

这些是模拟控制流证据，**不证明真实进程树已退出或物理墙钟严格耗时**。主工具和补交的假 taskkill 在 timeout=5 时故意推进虚拟时钟6秒，再抛 TimeoutExpired；它证明超时/预算耗尽记录与后续零许可，不能称为实测5秒清理。补交51秒同样是45+6的模拟值。build 的 fake 替换了整个 run_process，因此该函数的最后准入与内部异常分支以静态调用链为依据。目录/日志 OSError 的所有权覆盖也由当前 try/finally 直接核实，不伪称存在独立实测场景。

原 AC 允许最小静态调用链结合既有有限虚拟证据；当前证据已足够，不为上述已知模拟边界补新矩阵。本 Reviewer 仅执行文本/AST/hash/编码/报告检查，**新虚拟运行0、真实 build/Core/solution/GC/压力/dump运行0**。L1–L5均未启动。

## 工具交付与 GC / Release 分列

| 维度 | 当前结论 |
| --- | --- |
| 本次限定 Implementation | PASS；FIX-003 CLOSED，工具 finding 累计3/3关闭；本轮剩余可执行项0。 |
| 本次工具验证 | 静态与既有有限虚拟证据足够关闭原 AC；没有实际 Windows 清理耗时/退出保证的新运行证据。 |
| 既有正式候选 | `gc-capture-20261023-01` build PASS：exit0、201 warnings、0 errors；solution/Release FAIL：12881 Passed / 1 Failed / 1114 NotExecuted。103份TRX/6空；net6为6715/1/593，net8为6166/0/521。保留既有候选归属。 |
| 目标与相关正式失败 | 原 RepeatedClosedApplications 两TFM通过、无 ready/dump，仅为未复现；net6 RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection 的 provider弱引用失败仍未解决。后续精准绿不能冲销。 |
| GC 根因/问题 | ROOT_CAUSE=NOT_DETERMINED；GC_PROBLEM=UNRESOLVED。这是本轮沿用的既有已审状态，不评价 Astra 尚未交付的调查。H6对同步失败夹具不构成已证原因。 |
| Release Gate | 既有 build/solution 两项中1项通过、1项失败；整体 Release FAIL。未在工具修正后刷新，不把工具 PASS 当正式门禁 PASS。 |

## 收口与停止边界

| TODO 分组 | 本轮结果 |
| --- | --- |
| Completed | Review4 原文归档；按当前补交核对四组原 AC；FIX-003 CLOSED。 |
| Open Actionable | 0（本次限定工具范围）；不派发修复。 |
| Blocked Approval | 无；本次明确追加授权已覆盖停止条件，不重复询问。 |
| Blocked External | 无本轮外部采证要求；不以反复本地绿替代 GC 根证据。 |
| Not Applicable | 评判进行中的 Astra 调查；重开 Stage B八项/EXT-001；Uow/CAP/FreeSQL/C/D；SDK/TFM/依赖/版本/API baseline/预算变更。 |
| Accepted Limitations | 既有事后起点/source-at-build限制沿用；不补造历史身份或因行号/hash不足循环造证据。假进程结果不扩大为真实 OS 耗时保证。 |
| Verified Boundaries | 仅既有虚拟场景的准入、状态、异常保留及预算耗尽行为；固定30s/20s/15min/45s合同保持。 |
| Deferred | GC根因与后续生产候选由主线程另行验收 Astra 的独立交付，本轮不决定实验或修复。 |

No-Progress：CHANGED（唯一剩余工具 AC 已闭环；GC因果未由本轮推进）。**Next Action：STOP，交回主线程。** 不自行Fix/Review，不暂存/提交/推送，不调用外部通知。

写入后机器头、UTF-8、控制字符、链接、归档 hash 及本轮限定受保护输入核对见 [review-validation.json](artifacts/review-round-5/review-validation.json)。没有冻结 Astra 动态产物。
