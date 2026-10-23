<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261022-abp-phase-b-operation-uow
AI_REVIEWED_AT: 2026-10-23T11:03:23+08:00

# 阶段 B 独立复核 · Review Round 4（限定追加授权）

结论：**PASS（本次实现及受影响测试验收）**。唯一剩余 **FIX-005 CLOSED**，四项 AC 均满足；原8项现为 **8 CLOSED / 0 OPEN_ACTIONABLE**，无新增 MUST_FIX/SHOULD_FIX。上一轮真实 SQLite 反例在当前 DLL 下均于 SQL 前拒绝；额外双 Context 检查确认部分 observer 捕获后失败只清除失败 root，保留中间 root 保护，退出后记录归零。

**阶段 Release 尚未验收通过**：当前 Release solution build 和默认完整 L4 均为 **NOT_REFRESHED_AFTER_FIX**。按用户安排交主线程冻结候选并执行 plan §11.3 最后检查点；它们不作为本次代码 finding。历史 Release FAIL 和 GC 因果 NOT_DETERMINED 保留。

```text
REVIEW_ROUND: 4
AUTHORIZATION: ONE_ADDITIONAL_LIMITED_FIX_AND_REVIEW
REVIEW_SCOPE: FIX-005_AND_DIRECT_IMPACT
CANDIDATE: phase-b-review-fix-round-3-fix005-2026-10-23-v1
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS (本次直接行为及受影响项目范围)
DOCS_CONTENT_STATUS: PASS / REUSED_UNCHANGED_SCOPE
DOCS_CHECK_STATUS: PASS / REUSED_UNCHANGED_SCOPE
RELEASE_BUILD_STATUS: NOT_REFRESHED_AFTER_FIX
FINAL_DEFAULT_L4_STATUS: NOT_REFRESHED_AFTER_FIX
RELEASE_STATUS: NOT_REFRESHED_AFTER_FIX
HISTORICAL_ROUND1_RELEASE_STATUS: FAIL
GC_FAILURE_CAUSALITY: NOT_DETERMINED
EXT_001_STATUS: CLOSED / REUSED_UNCHANGED_SCOPE
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL (EXT-002)
THRESHOLD_STATUS: BLOCKED_APPROVAL
PERFORMANCE_EVIDENCE_STATUS: OBSERVED / CAPTURE_INCOMPLETE (复用)
RESOURCE_EVIDENCE_STATUS: OBSERVED (复用)
FINDINGS_TOTAL: 8
CLOSED: 8
OPEN_ACTIONABLE: 0
MUST_FIX: 0
SHOULD_FIX: 0
NO_PROGRESS_GUARD: CHANGED (唯一剩余finding关闭)
GOAL_STATUS: STOPPED_BLOCKED
NEXT_ACTION: HAND_BACK_TO_MAIN_THREAD_FOR_FINAL_CANDIDATE_CHECKPOINT
```

## 输入、基准与不可变记录

依据 [限定追加授权](limited-fix-authorization.md)、根 AGENTS、GOAL_RULES、review-code 技能、当前 plan/execution/final-report/traceability/api-delta 和原 Review 3，核对 OpenCode 交付的 startpoint、真实 diff、当前源码/测试、候选清单和原始验证结果。仅复核 FIX-005 及直接影响，不重开无新反证的7项 CLOSED、EXT-001或阶段A两个 CLOSED，不扩入 FreeSQL B04/C/D。

更新前保存 [Review 3 不可变副本](artifacts/review-round-4/review-round-3.md)，SHA256 `d0fb22fd3cd12d3f421967f20b4242f68c4baa27d6feb9b2aee4f1083bf624f3`。Review1/2及历史 artifacts不覆盖。初始归因仍是 B00 dirty 工作区；本轮对照 `artifacts/review-fix-round-3/startpoint/`，不以 HEAD 全差异归因本次修复。

[机械核对](artifacts/review-round-4/evidence-mechanical.json)确认：前序262路径中，本轮3生产+2测试文件匹配新清单，其余257项仍匹配Round2；5项起点与真实diff对应。候选是增量清单 `candidate-fix005-v1.json` 加前序262项，不误写为本轮新生成262项全量封存。API delta仍描述Round2的公开面；本轮3生产文件只改private/internal行为，没有公开签名、依赖/SDK/TFM/API baseline或预算变化，因此沿用公开面结论，不把历史候选段落当当前Release证明。

本轮只写 review及review-round-4 artifacts。机械结果核对委派给 luna_worker；实现、调用链判断及最终结论由本 Reviewer负责。[影响分析](artifacts/review-round-4/change-impact.json)限制新增执行为 artifact 内 net6 direct-DLL、唯一命名共享内存SQLite；不引用生产项目、不改生产/测试/Executor报告，无外库、probe、GC或全量重跑。主线程后置Release检查点保持原安排。

## 原8项状态

| Finding | 状态 | 本轮处理 |
| --- | --- | --- |
| FIX-001 | CLOSED | operations+旧标记共存、root内participant/root外legacy、注册及通知去重原验收保留；分派未改 |
| FIX-002 | CLOSED | Saving后复查、完成权/活动句柄、重入/跨流、主异常和清理协议原验收保留；本轮退出通知只清理registry保护，Uow两TFM完整回归通过 |
| FIX-003 | CLOSED | 同步Store实际物化取消与EF有效frame token原验收保留；相关测试在当前EF完整集通过 |
| FIX-004 | CLOSED | Dapper reader token租约、participant自身取消及无operation/独立SQL旧契约未改；当前SQLite完整回归保持 |
| FIX-005 | CLOSED | 四项AC逐项满足，见下表及Reviewer真实SQLite观察 |
| FIX-006 | CLOSED | 新scope executor解析顺序、四标量Trace副本和finally恢复原证据保留；当前Uow/EF成功对照通过 |
| FIX-007 | CLOSED | automatic真实组合链/返回状态/单消费、EF/宿主矩阵原验收保留；无新反证，不要求再跑代理或外库 |
| FIX-008 | CLOSED | 清单覆盖、逐结果计数及当前/历史分栏原验收保留；本轮5项覆盖与L2核对通过，产品文档未改 |

## FIX-005 最终验收

原严重程度 **HIGH / MUST_FIX**；本轮状态 **CLOSED**。原问题和四项AC以不可变Review3副本为准，不改写历史失败。修复目标是保留每个活动root继承的祖先pending/claim约束，并在退出或启动失败时只释放对应root记录。

| 原 AC | 判定 | 可复核行为和证据 |
| --- | --- | --- |
| 1. A pending→B→C→退出C后B的sync/async Complete、Save、Commit于I/O前拒绝，rollback-only/SQL0/commit0，A回滚后空行集 | PASS | `RequiresNewNested_WhenDeepestRootExits_ShouldKeepMiddleRootProtected` 6个case检查实际调用、命令/物理提交/SaveAfter增量0、B未完成及独立连接精确空行集。Reviewer沿用原direct-control及三条失守路径，当前DLL全部InvalidOperationException/SQL0/SaveAfter0/rows[] |
| 2. entity/action/event/claimed跨嵌套不迁移；action/event0，query-first claim不绕过；保留单层矩阵和写前准入 | PASS | `RequiresNewNested_WhenDeepestRootExits_ShouldRejectOuterAuxiliaryState` 8个case含真实SQL先claim、事务动作/领域事件，B handler前置拒绝，动作/事件0、无新增SQL/commit；event的操作前seed行精确保留。单层显式sync/async Save/Commit和query-first claim矩阵在当前235项EF完整集通过。provider/GetResource、ValidateContextAdmission、GetResourceForContext沿用同一按root查找的guard，均位于CreateResource/claim前；不是仅在最终Complete检查 |
| 3. 新root发布前observer/DetectChanges抛错，Current保持B；B保护不丢、失败root无残留，异常后写前拒绝 | PASS | 新observer回归检查Current=B和后续同步Save拒绝。Reviewer另用两个已解析Context：C先在第一个Context记录，再由第二个getter在DetectChanges抛错；直接观察记录由[B]→[B,C]→[B]，两个Context均只保留B，B SaveAsync拒绝并rollback-only；退出B后两集合均空、SQL0、rows[]。此补充实际覆盖部分捕获后的清理，不靠夹具名或单个绿结果 |
| 4. 未占用Context的合法inner-only及真新DI executor成功保留，子提交不受outer rollback撤销 | PASS | 新三层inner-only用例提交1次、独立连接精确一行；当前EF完整集保留`ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack`的Context/Query/root身份及独立子提交对照。Reviewer另有inner-only实际SQL1/SaveAfter1/完成成功、outer rollback后仅子行 |

### 调用链及保护生命周期

`UnitOfWorkBase` 以 `Dictionary<Guid, OperationRootProtection>` 保存root约束（字段:74，捕获:984，准入:1004，释放:1028附近）。捕获仍识别tracked entity、事务action、未分发event和活动mapped claim；更深root写入自己的key，不覆盖B。guard只查当前root key并标记rollback-only。正常clean Context在B开始时没有B key，所以B内产生的合法状态不会因C进入/退出而被永久禁止。

sync/async `Commit`均委托对应`SaveChanges`；所有Save重载、纯保存`SaveOperationChanges`、mapped资源准入和provider路径均在资源迁移/写入前调用guard。自动handler先检查整个已解析快照再保存任一Context，防止先保存其他Context。此次未删除/推迟这些入口的检查，仅替换它们查询的保护存储；既有单层职责证据可与新增嵌套矩阵合并判断，无需制造所有排列的重复测试。

`ResolvedEfCoreUnitOfWorkRegistry.OnRootStarting`按已解析快照捕获；`OnRootExited/OnRootStartFailed`仅Remove指定root ID，不执行DetectChanges、动作、事件或I/O。`UnitOfWorkScopeManager.BeginRoot`先调用观察器，成功后才发布frame；异常时清理所有观察器对失败root的记录并原样重抛，Current保持原frame。内部注册仍复用同一个scoped registry，未扫描或构造unused Context。

`Exit`先保留原LIFO校验并恢复父frame，仅root退出才通知清理，participant退出不删root保护。`UnitOfWorkScope.BeginDispose`仍先检查Saving/Completing/RollingBack及活动句柄、取得资源快照，再Exit；异步物理清理前恢复caller的原协议保持。新observer清理异常不替换资源释放/主异常；当前内部EF实现是锁内字典Remove，没有用户回调/I/O。完成、回滚、一次释放及主异常协议未改，当前Uow完整回归两TFM各70/0/0，未发现需要重开FIX-002的新证据。

### Reviewer 精准观察

[源码](artifacts/review-round-4/nested-root/Program.cs)、[项目](artifacts/review-round-4/nested-root/Repro.csproj)、[命令](artifacts/review-round-4/nested-root/command.json)、[stdout](artifacts/review-round-4/nested-root/stdout.txt)、[输入DLL](artifacts/review-round-4/nested-root/binary-inputs.json)、[逐项判定](artifacts/review-round-4/nested-root/verified-observations.json)保留。一次执行，exit0，6项行为检查满足；原4场景没有靠exit0判通过，而是逐项检查异常、完成状态、SQL/SaveAfter和精确行集。原三条B路径已从上一候选SQL1/永久行变为SQL0/rows[]。额外部分捕获检查直接证明清理新key并保留旧key。

该夹具只编译artifact自身，引用现有EF测试Release net6 DLL和既有SQLite native库；输出中的NETSDK1138是既有net6目标告警，不更改TFM。它不是全solution Release构建或性能probe。

## B00–B06 验收矩阵

主线10项为 **9 PASS、1 PARTIAL**；B04独立不入分母。B06剩余的是最终候选Release检查点，不是OPEN_ACTIONABLE实现缺陷；不宣称阶段完整10/10收口。

| 项 | 状态 | 当前判断 |
| --- | --- | --- |
| B00 | PASS | B00 dirty基准保留；本轮5项起点及前序262路径真实对应 |
| B01.1 | PASS | root/participant、alias/旧默认、Saving与取消原验收保留；新root清理受影响Uow回归通过 |
| B01.2 | PASS | 共存/注册/通知及automatic实际组合链已验收，未变范围复用 |
| B02.1 | PASS | 已解析registry/纯保存原证据保留；三层归属及Begin失败四AC通过，FIX-005关闭 |
| B02.2 | PASS | 纯保存、保存后资源快照、flush/事件/false/latepending/非事务/部分提交复用；当前完整EF回归通过 |
| B02.3 | PASS | Store同步边界、frame/reader/late-cancel原契约保持，当前EF/SQLite完整集通过 |
| B02.4 | PASS | 真新scope executor顺序/身份/恢复/独立子提交保持；同scope嵌套归属已修复验收 |
| B03 | PASS | 原真实HTTP/Worker/Hangfire、两storage和外部消费未受影响范围复用 |
| B04 | DEVIATED_OK | FreeSQL原生适配留独立计划，C/D不扩入 |
| B05 | PASS | 已接受产品文档/编译主例与checker复用；本轮公开面未变 |
| B06 | PARTIAL | §11.3最终build/L4待主线程；旧GC FAIL/预算/probe限制独立保留 |

## C01–C14 契约验收矩阵
PASS是相关范围证据验收，不等于当前完整solution Release PASS。

| 契约 | 状态 | 证据范围 / 剩余边界 |
| --- | --- | --- |
| C01 新API/注册共存 | PASS | 冻结旧接口/新companion、alias、自定义manager、注册顺序/重复及root内外共存复用，当前Uow回归通过、AOP未变范围复用 |
| C02 owner/participant失败 | PASS | rollback-only/Saving准入/重入与清理原直接证据，当前完整Uow保持70/0/0每TFM |
| C03 单消费/终端/整链取消 | PASS | 复用Round3已接受的新automatic真实返回/外层观察、原异常/token、Task及source-backedVT取消/成功；FIX-007关闭 |
| C04 scope/重解析 | PASS | executor真新scope原证据及当前EF完整回归保留；三层requiresNew和Begin失败保护经本轮直接SQLite复核，FIX-005关闭 |
| C05 tracked-only/未用实例 | PASS | registry只登记已解析实例、root前/中、引用去重与unused构造0的证据继续接受；跨root归属经本轮FIX-005四项AC验收 |
| C06 legacy/未映射 | PASS | 纯保存不进legacy物理Commit、unmapped脏/显式写拒绝原矩阵接受 |
| C07 手工flush/事件/false | PASS | flush后不重复事件/通知、false、late cross-context pending等已接受新模式证据复用 |
| C08 同库/跨库/失败 | PASS | EF/Dapper顺序、保存失败0commit、部分提交与非事务精确行集原证据保留 |
| C09 非事务/策略 | PASS | 原非事务0事务、事务动作/事件I/O前拒绝和participant选项策略保持 |
| C10 超时/取消 | PASS | 复用已接受的同步Store三种来源真实I/O前拒绝；async/frame/reader token及无operation对照原证据保留 |
| C11 通知/释放/主异常 | PASS | 同服务去重、主异常/OCE及清理错误、通知失败不重提原证据保留；最终CapTrace恢复TCS兼容 |
| C12 三入口 | PASS | 已接受两storage真实HTTP/Worker/Hangfire身份/取消/下一项恢复复用；EXT-001只复用未变范围 |
| C13 资源/缓存/上下文 | PASS | 未改SQL格式化/映射缓存/resource key，不扩benchmark；跨root归属修复已由C04/FIX-005验收 |
| C14 编译/API | PASS | 当前Uow契约/文档主例通过、AOP consumer未变范围复用；未改产品主例复用；solution最终build另待§11.3，不假称已刷新 |

## 原始验证核对与候选关联

只使用以下明确最终目录，失败/中间迭代不混拼：

| artifacts/review-fix-round-3/ 下最终目录 | 项目 / TFM | Passed / Failed / NotExecuted | 范围 |
| --- | --- | --- | --- |
| l1-green | EF精准 / net6 | 16/0/0 | 新增6+8+1+1案例，exit0 |
| l2/ef-net6-fix005 | EF完整 / net6 | 235/0/0 | 无filter、含原单层/成功矩阵，exit0 |
| l2/uow-net6-fix005、uow-net8-fix005 | Uow完整 / net6/net8 | 各70/0/0 | 无filter，各exit0 |
| l2/sqlite-net6-fix005、sqlite-net8-fix005 | SQLite integration完整 / net6/net8 | 各233/0/1 | 各发现234、exit0；provider contract gate跳过不计Pass |

逐`UnitTestResult`、命令、stdout/exit及TFM关联见[机械artifact](artifacts/review-round-4/evidence-mechanical.json)。SQLite TRX Counters的notExecuted=0与实际234条结果不一致，按真实233 Passed+1 NotExecuted及stdout记账。五组L2命令均无filter且没有--no-build；显式项目/TFM、输出路径、源码清单和构建时间支持当前候选关联。TRX没有内嵌所加载DLL哈希，当前DLL/hash观测不冒充受保护发布封存。

L1目录没有独立command.txt，精确selector/整条命令的单独归档缺口保留；其实际16条TRX及stdout可核对，完整EF重建235项亦包含这16项，Reviewer另有明确命令及当前DLL反例复核。因此该归档限制不阻断本次AC，不伪造原命令、不要求再跑追绿。

红证据 `l1-red` 是1 Passed/15 Failed，其中14条是目标三层反例，1条是observer夹具未触发DetectChanges。夹具改`PropertyAccessMode.Property`后，生产修复前 `l1-red-observer4` 已1/0/0；该项只支持回归控制，不能声称证明旧实现失败。其余中间迭代原样保留，不加进当前通过数。

未变AOP/EventBus、宿主和CAP只在已经接受的具体范围复用；这些L2或历史L3不拼成当前完整L4。文档未改，复用13项checker及编译主例。

## Release、外部与递延分栏

| ID / 门禁 | 状态 | 当前边界 |
| --- | --- | --- |
| 当前Release build / 默认完整L4 | NOT_REFRESHED_AFTER_FIX | 主线程冻结候选后按§11.3执行；不得写当前Release PASS |
| Round1 Release/L4 | 历史FAIL | 103TRX、12828/1/1114，唯一net6 Core GC弱引用失败；当时build exit0/170warnings/0errors只属旧候选 |
| EXT-001 | CLOSED / REUSED_UNCHANGED_SCOPE | 准确为7个新executor+1个operations-enabled旧手工partialcommit；本轮只改同DI嵌套保护，原外部三入口/消费相关范围未变，不重跑外库 |
| EXT-002 | BLOCKED_EXTERNAL | dirty-source受保护发布证据限制保留；原MySQL各53/0/6与runner exit1分列，不要求commit/clean |
| APR-001 | BLOCKED_APPROVAL | 无性能/资源预算，不制造性能PASS或追加probe |
| LIM-001 | ACCEPTED_LIMITATION | 原SQL Uow probe的$pid冲突及child捕获缺失保留，不补造或重跑 |
| LIM-002 | ACCEPTED_LIMITATION | 本轮L1无独立command.txt；TRX、完整L2及Reviewer明确命令足以核对本次行为，限制如上 |
| DEF-001 | DEFERRED | GC因果NOT_DETERMINED，最终L4若再失败照录，不隔离追绿/扩修 |
| DEF-002 | DEFERRED | FreeSQL B04/C/D及阶段A原OPTIONAL，A两CLOSED不重开 |

## 最后检查点 TODO 与停止

1. **主线程冻结当前候选**：以前序262项加本轮5项增量和实际源码为准；本次FIX-005实现验收已关闭。Reviewer不启动新的Fix/Review轮次。
2. **主线程执行plan §11.3**：一次同候选Release solution build成功后，再默认完整solution L4（无filter、无全局-f、无隐式runsettings；独立子进程关闭外部/probe gates，清除该子进程连接/broker变量，保留默认本地SQLite/xUnit并行）。保存候选关联、command、各项目/TFM发现数/逐结果/TRX/stdout/stderr/exit。当前尚无此结果，不能跳过该检查点。
3. **按结果收口**：未变文档复用已接受checker/编译例；历史FAIL不覆盖。若最终L4再次出现已知GC失败，保持该次Release FAIL与NOT_DETERMINED，不隔离追绿或把它无因果地变成本阶段实现MUST_FIX。受保护发布/预算批准独立处理。

Completed：8/8 findings CLOSED；本次唯一FIX-005四AC通过。Open Actionable：0（MUST_FIX=0、SHOULD_FIX=0）。Blocked Approval：预算。Blocked External：EXT-002。Not Applicable：未变Provider专属路径/SQL格式化缓存benchmark。Accepted Limitations：有序本地事务、同步协作取消、原probe及CAP子进程归档、本轮L1命令归档限制。Verified Boundaries：三层外层entity/action/event/claim拒绝、Begin部分捕获清理、合法inner-only与独立executor成功。Deferred：GC因果、FreeSQL/C/D。

Implementation及受影响Test验收PASS，主线计划9/10 PASS、B06 PARTIAL，Release NOT_REFRESHED_AFTER_FIX。追加限定独立复核完成即停止，交回主线程执行最后门禁，不自动开始下一轮。
