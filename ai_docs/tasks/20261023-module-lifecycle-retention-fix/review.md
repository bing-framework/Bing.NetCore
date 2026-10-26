<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261023-module-lifecycle-retention-fix
AI_REVIEWED_AT: 2026-10-23T18:29:39+08:00

# 模块生命周期引用保留修正：独立 Review2

Reviewer：本线程 GPT-6.1-Sol / high，使用 review-code。已读适用 AGENTS、GOAL_RULES、当前完整 plan/execution/traceability、Review1、修复前后测试源码及精准原始证据。本轮仅验收本子任务唯一 FIX-001 的原 AC；Luna worker做 TRX/command/exit/identity机械核对，行为验收由主Reviewer负责。

**结论：PASS，FIX-001 CLOSED，OPEN_ACTIONABLE=0。** 四个实际生命周期用例已经直接记录factory解析、scope身份和释放次数，并与私有helper单元契约分开。生产实现及受控触发点消除沿用Review1已接受结论；候选Release仍为 **PENDING_FINAL_CHECKPOINT**。

```text
REVIEW_ROUND: 2
IMPLEMENTATION_STATUS: PASS
DIRECT_LIFECYCLE_COVERAGE: PASS
TARGETED_TEST_STATUS: PASS_NET6_NET8
FIX_001: CLOSED
OPEN_ACTIONABLE: 0
MUST_FIX: 0
SHOULD_FIX: 0
PRODUCTION_AND_CONTROLLED_PROBE_REVIEW: REUSED_UNCHANGED_SCOPE
CURRENT_CANDIDATE_RELEASE: PENDING_FINAL_CHECKPOINT
HISTORICAL_FORMAL_RELEASE: FAIL_UNCHANGED
HISTORICAL_EXCLUSIVE_ROOT_CAUSE: NOT_DETERMINED
PRODUCT_PERSISTENT_LEAK: NOT_PROVEN
PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE_NO_CLAIM
RESOURCE_EVIDENCE_STATUS: NO_NEW_GC_EXPERIMENT
EXTERNAL_GATE_STATUS: NOT_REQUIRED_FOR_THIS_FINDING
GOAL_STATUS: STOPPED_LIMITATION_PENDING_MAIN_THREAD_CHECKPOINT
AUTO_CONTINUE: false
NEXT_ACTION: STOP_HAND_BACK_TO_MAIN_THREAD
```

Review1已按原字节归档至 [不可变副本](artifacts/review-round-2/review-round-1.immutable.md)，SHA-256 `3f6f4dc28ba3a4a08af780498e12a01172172669f848b4a89c39ce4c58861d08`。本轮不改生产/测试/执行报告、旧GC工具review/raw、Stage B或父路线图。

## 本轮范围与证据身份

[范围比较](artifacts/review-round-2/scope-comparison.json)确认自Review1以来，plan、BingModuleManager、原ModuleLifecycleTest（含Recorder helper与弱引用断言）、旧GC review未变；本轮相关修改为ModuleLifecycleRetentionTest及execution/traceability。暂存区相关diff为空；HEAD工作区差异仍包含旧修改，不能当成本轮差异。测试的真实修复差异见 [test-scope.diff](artifacts/review-round-2/test-scope.diff)。

当前测试使用LifecycleObservation反射构造真实internal registration/manager并设置模块描述符；随后调用真实IBingModuleManager生命周期接口。ObservedServiceProvider只在GetService(IServiceScopeFactory)时累加解析数；ObservedScopeFactory每次new一个scope并登记；ObservedScope.Dispose真实累加次数及释放序列。四个生命周期用例不手工Dispose返回scope，因此相关计数来自manager执行路径。

## FIX-001 原AC逐项验收

| 原AC / 路径 | 结论 | 当前直接证据 |
| --- | --- | --- |
| AC1：正常Initialize / Shutdown及重复关闭 | PASS | NormalLifecycle_ShouldOwnInitAndShutdownScopes_AndReuseFactory（:18–45）实际Initialize后断言解析1、仅1个scope且DisposeCount=1；Shutdown后解析仍1、创建2个且NotSame、各DisposeCount=1；再次Shutdown和manager.Dispose后解析/创建/释放计数及钩子序列不增。 |
| AC2：已进入初始化钩子后失败 | PASS | InitializationHookFailure_ShouldDisposeInitAndCleanupScopesOnce（:51–65）记录initialize后抛InvalidOperationException，断言原类型/消息hook failure；解析1、scope2、各释放1，事件initialize→shutdown，DisposeSequence精确为cleanup scope[1]→init scope[0]。不是初始化之前失败的空对照。 |
| AC2：进入后信号取消 | PASS | CancellationAfterEnteringInitialization_ShouldDisposeInitAndCleanupScopesOnce（:71–90）实际InitializeAsync，在Entered信号到达后Cancel；信号等待有5秒上限，无固定sleep。保留OCE及已取消有效token，解析1、scope2、各释放1，同样断言cleanup先于init释放和钩子顺序。 |
| AC2：已初始化后的provider Dispose路径 | PASS | ProviderDisposeAfterInitialization_ShouldNotCreateShutdownScope（:96–108）先Initialize，再调用真实manager的IDisposable.Dispose回调入口；解析仍1、scope仍1且仅释放1，shutdown钩子已执行。这里直接覆盖manager的provider-dispose分支；默认容器调用该回调的装配关系沿用未变生产源码和Review1已接受的实际provider Dispose回归，不冒称ObservedServiceProvider本身实现了Dispose。 |
| AC3：直接计数与helper边界、映射 | PASS | 计数留在实际入口断言中。PrivateHelperContract_...明确保留为手动helper单元契约，不再代替生命周期所有权；Recorder身份测试只证明本测试类的helper，原helper身份沿用既有事件断言。execution/traceability已区分真实生命周期、私有helper与旧Probe零事件。 |
| AC4：两TFM精准验证与候选关联 | PASS | 仅采信review-fix-round-1/l1-net6-final和l1-net8-final；各6项实际UnitTestResult均Passed，exit0，无NotExecuted。test-identity匹配当前测试源码与测试DLL，生产DLL未变。未要求重跑Probe/完整套件。 |

**FIX-001：CLOSED。** 原计划实施第4项的直接观测和第5项的证据分栏已补齐；加上Review1接受且本轮未变的实施1–3，计划实现项5/5已验收。该数量不包含最终Release门禁，不表示历史GC失败已被重新归因。

本轮没有发现需要最小反例运行的具体疑点，源码、计数断言和直接raw已足够。无需新增矩阵、加强原AC或重新要求已接受的并发/legacy/容器覆盖。

## 精准结果与范围复用

原始证据和当前identity核对见 [raw-evidence-audit.json](artifacts/review-round-2/raw-evidence-audit.json)。最终两份TRX共12 Passed / 0 Failed / 0 NotExecuted；过滤范围均为ModuleLifecycleRetentionTest，net6/net8 Release，未把完整项目或历史attempt混入。

中间compile/test失败及较早release目录按历史迭代保留，不冲销也不计入当前通过。traceability末尾概述仍有`l1-*-release`字样；当前主要映射、执行报告和validation的明确最终目录均为`l1-*-final`，实际TRX/源码/identity关联足够，不影响验收，不据此制造新的报告finding。

[复用边界核对](artifacts/review-round-2/evidence-reuse-boundary.json)确认生产Core DLL与原Probe batch一致，Probe源码差异及Probe DLL也仍匹配Review1接受的版本；**当前两份测试DLL已更新，不匹配旧Probe batch，这是预期变化。** 旧Probe四绿/零事件、完整Core各645及Bing.Tests各112，均仅按相关未变的生产/原测试范围复用；没有宣称Probe或完整Core曾针对本轮增强后的测试DLL重跑。本轮新的解析/创建/释放证据来自精准测试断言。

受控机制仍只证明一个足以导致两个原断言失败的真实DI后台引用保留机制，以及原候选输入下的触发点消除；历史自然失败的排他归因保持NOT_DETERMINED，永久产品泄漏保持NOT_PROVEN。不保证任意应用Dispose后立即回收。

## Implementation / Test / Release

| 维度 | 本轮结论 |
| --- | --- |
| Implementation | PASS；唯一finding CLOSED，OPEN_ACTIONABLE=0。生产实现按未变范围复用，不重开。 |
| 直接受影响测试 | PASS；当前两TFM各6项真实生命周期/helper职责回归，源码及DLL身份匹配。 |
| 受控因果 / 既有回归 | Review1已接受范围复用，未新增GC/Probe运行，不把工具PASS当产品证据。 |
| 新候选Release | PENDING_FINAL_CHECKPOINT；最终Release solution build及默认solution待主线程统一安排一次，本轮未运行，两项当前均尚待检查。 |
| 历史正式失败 | gc-capture-20261023-01的build PASS、solution/Release FAIL（12881/1/1114，net6 retained失败）保留；Stage B另一候选的net8 repeated失败也不改写。 |

## 停止与交回

| TODO分组 | 当前处理 |
| --- | --- |
| Completed | Review1原文归档；FIX-001四项AC验收；真实生命周期计数/清理顺序、精准TRX与映射核对。 |
| Open Actionable | 0；不继续自动Fix/Review。 |
| Blocked Approval | 无；本次授权充分，不重复询问。 |
| Blocked External | 无本轮finding依赖外部采证；历史根链缺口不转换成当前实现缺陷。 |
| Not Applicable | 重开旧GC工具/Stage B项；生产修复扩展、外库/压力/并发矩阵、SDK/TFM/依赖/API baseline/预算调整。 |
| Accepted Limitations | 旧Probe对应旧测试DLL；受控机制不等于历史唯一归因或永久泄漏证明。 |
| Verified Boundaries | 当前真实生命周期factory/scope/释放计数已直接断言，两TFM精准证据通过；原Probe与既有完整回归只按未变范围复用。 |
| Deferred | 主线程安排新候选一次最终Release构建/默认solution检查点，另存新结果，保留历史失败。 |

No-Progress：CHANGED（唯一直接回归缺口已闭环）。**Next Action：STOP，交回主线程执行最终检查点安排。** Reviewer本轮只做L0静态/差异/证据解析，新增L1–L5运行均0；不派发修复、不暂存/提交/推送、不发外部通知。

归档hash、严格机器头、UTF-8/控制字符、链接和限定受保护输入不变核对见 [review-validation.json](artifacts/review-round-2/review-validation.json)。
