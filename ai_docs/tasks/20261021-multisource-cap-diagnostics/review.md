<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261021-multisource-cap-diagnostics
AI_REVIEWED_AT: 2026-10-22T00:40:08.063429+00:00

# T07 独立专项复验 · Round 4

结论：**PASS。FIX-002 CLOSED；全部9项 CLOSED，OPEN_ACTIONABLE=0。** 当前候选已消除原构造失败清理的同步等待死锁；独立真实MySQL探针和永久回归均确认公开Publish先返回未完成Task，异步释放由同owner线程正常pump完成，无Task.Run或异线程救援。失败初始化被缓存、原始/清理异常保留，root/host不重复释放；未发现本次改动的直接回归。

本轮依据用户**追加一次限定修复及原Sol/high专项验收**的明确授权执行，不受此前三轮停止结论阻断；授权范围仅FIX-002及直接回归，不重审全部功能、不开放下一循环。其余八项按未变范围复用已验收证据。

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS（限定修复及直接回归）
PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
RESOURCE_EVIDENCE_STATUS: NOT_APPLICABLE
EXTERNAL_GATE_STATUS: PARTIAL（旧SQLite证据探针、Dashboard目标仍未验证）
RELEASE_STATUS: PARTIAL（实现验收通过；上述未验证项仍单列，不冒充全环境PASS）
OPEN_ACTIONABLE: 0
BLOCKED_APPROVAL: 0
BLOCKED_EXTERNAL: 0
GOAL_STATUS: STOPPED_LIMITATION
NEXT_ACTION: STOP（唯一专项复验已完成，不再进入Fix）
```

## 输入、归档与候选

遵守AGENTS、review-code、GOAL_RULES及原批准完整plan；已读取Round3 review、当前execution的Round3限定修复、traceability、delivery-status、fix-round-3/validation/CIA/最终TRX和真实源码/差异。执行方称COMPLETED没有作为关闭依据。delivery-status的“待独立复验”是本报告写入前状态，本轮按只审查边界保留该文件，当前独立结论以本review为准。

先验证 [review-round-3/review.md](artifacts/review-round-3/review.md) 与起始review及fix-round-3/review-input.md原字节一致，未覆盖历史；SHA256 `c4027ee77e58ea07d1df3ae67fcf17d02d931c5c0fa6faed140fc76fbcd73991`，见 [archive-integrity.json](artifacts/review-round-4/archive-integrity.json)。HEAD仍为`5065c38ced05a95088e17d89d96c9a1610e37159`加当前未提交工作区，起始状态/差异/index保存于review-round-4。既有F008、SQL Uow、SQLite修改未还原或混作本轮新增。

[CIA](artifacts/review-round-4/cia.md) 在验证前限定了影响面。[candidate.json](artifacts/review-round-4/candidate.json) 绑定当前生产/永久测试/计划与报告文件，独立probe实际加载当前Sample Bing.Events程序集SHA256 `7d04d781d971b635dea1b3c09efa7f74bb947980a4ccb8230a94a29d963ef0e1`。未用Round3旧程序集证明修复通过。

## FIX-002 CLOSED 的实现与实证

责任位置：[CapUnitOfWorkRuntimeManager.cs](../../../framework/src/Bing.Events/Cap/CapUnitOfWorkRuntimeManager.cs) `GetOperationPublisherAsync` 第81行、`CreateOperationScope` 第288行、`CapOperationScopeResource` 第333行起；[CapUnitOfWorkMessagePublisher.cs](../../../framework/src/Bing.Events/Cap/CapUnitOfWorkMessagePublisher.cs) 第103行公开调用链。

root现在先登记不解析publisher的scope resource，再取得单一`Lazy<Task<ICapPublisher>>`。正常同步解析可返回已完成Task；构造错误被缓存为失败Task，失败路径**await**一次性DisposeAsync。清理成功重抛原始异常对象；清理也失败则保留两者。公开Publish等待它，错误仍令root rollback-only；原物理资源lease包围整个初始化和发布。

原构造失败路径的GetResult已经移除。同步`Dispose()`仍可等待已有未完成的释放Task，但不在此失败调用链上：Publish pending时root的active lease检查拒绝并发释放/完成；Publish返回后清理Task已完成，后续root/host不重放。没有以fire-and-forget、Task.Run或线程转移掩盖症状。

资源顺序仍为本源物理资源先登记/ready、操作scope随后登记，scope Commit/Rollback为空，root按原序提交、逆序释放，物理事务唯一owner未变。原生dispatcher/recovery/retry、最终Provider/物理库校验、borrowed buffer及提交后flush逻辑未改。

独立 [probe/Program.cs](artifacts/review-round-4/probe/Program.cs) 经公开注册/公开Publish和真实ready MySQL资源，分别测试cleanup成功与抛错；同owner正常pump，无异线程救援。[原始probe.log](artifacts/review-round-4/probe.log) 直接记录：

- 调用者先拿到未完成Task；两个分支分别owner11/10，continuation仍在同owner和同context。
- 原始异常引用保留；cleanup失败时第二异常引用也保留；root rollback-only。
- 同active root重复公开Publish，使用同一失败异常对象，publisher工厂仅1次；sync/async依赖各释放1次。root/host后仍各1次，失败Task和释放均未重建。
- 独立连接读取orders/reporting Outbox均为0。真实operation为`r4-530dc462c2ba4025a94d9b2805b7a952`和`r4-8ad27c4e296a44648258f2809e0c1720`。

[probe-result.json](artifacts/review-round-4/probe-result.json) 为自有PID81544、exit0、未超时；独立临时项目build 0errors，未编译生产引用、未停止5088。

## 永久回归与最终结果真实性

新永久方法`OperationScope_WhenAsyncPublisherCleanupCapturesSingleThreadContext_ShouldReturnAwaitableFailure`真实调用公开Publish，依赖DisposeAsync实际await Task.Yield；先断言未完成Task，再同线程pump，检查same context/owner、两个异常、rollback-only、拒绝Complete、创建/释放exact1、root/host不重放、两库独立Outbox0。原构造失败方法同时保留无上下文清理成功/失败分支。不是用方法名或立即完成的ValueTask代替原问题。

子dotnet test监督有固定完整方法筛选、当前TFM、45秒截止；超时和非0子退出均使父测试失败，只终止其自有进程树。指定原件中已核对实际子方法执行行、回归body/SQL事实及子runner **1/1、exit0**；没有把无测试或只返回0当成通过。

[trx-audit.md](artifacts/review-round-4/trx-audit.md) 逐UnitTestResult核对最终两份：

| 最终原件（fix-round-3/trx） | Passed | Failed | NotExecuted/Skipped |
| --- | ---: | ---: | ---: |
| fix-round-3-lifecycle-net8-output-check-attempt2.trx | 6 | 0 | 0 |
| fix-round-3-lifecycle-net6-output-check.trx | 6 | 0 | 0 |

六个实际职责方法为：新context清理、原构造失败清理、root/participant/独立root scoped复用、真实发布提交前不可见、成功跨await/nested ambient恢复、native发布错误恢复。配套build均0errors；内嵌child1/1不额外累加，早期失败尝试也未累计。

原console未打印门控环境值，执行记录有显式RUN_CAP_MYSQL_TESTS=1；核对当前CapMySqlFact门控、Passed而非Skip以及实际DB输出，确认测试已执行。Reviewer另独立显式设置该变量，no-build/no-restore精准复跑新永久方法：outer1 Passed/0fail/0skip、child PID86100实际1/1和exit0、真实event `b6eb9006dce54c32a4352692d35fe76e:captured-async-cleanup`两库Outbox0。见 [独立TRX](artifacts/review-round-4/trx/review-r4-context-net8.trx)、[命令/环境/退出码](artifacts/review-round-4/permanent-result.json)。该复跑与交付矩阵重叠，不再累计为额外覆盖。

嵌套子stdout中文标签有Windows mojibake，原始保留；ASCII方法/回归事实/退出与计数可核对，未据此指控源码损坏或重跑测试凑结果。详细harness边界见 [validation.md](artifacts/review-round-4/validation.md)。

## 全部稳定 Finding 状态

| FIX | 最终状态 | 本轮依据 |
| --- | --- | --- |
| FIX-001 最终Provider/物理库 | **CLOSED** | 校验实现未改，沿用Round3默认resolver错库拒绝及原真实事务匹配证据。 |
| FIX-002 操作scope生命周期 | **CLOSED** | 本轮真实ready资源、awaitable失败、same-owner异步清理、异常/缓存/exact-once及永久测试闭环。 |
| FIX-003 ready/not-ready取消 | **CLOSED** | 预取消分支在新初始化await之前，相关代码未改；四组合断言沿用。 |
| FIX-004 真message-first | **CLOSED** | 共同业务顺序未改，双Dapper/EF真实时序/行数证据有效。 |
| FIX-005 localbus/三入口scope | **CLOSED** | OnCompleted和三入口实现未改；成功/失败/重复Complete和scoped断言复用。 |
| FIX-006 全ID/JobId/Consumer身份 | **CLOSED** | 诊断投影、API/UI未改，真实SQL/浏览器证据复用。 |
| FIX-007 journal/逐源结果/retry事实 | **CLOSED** | journal、callback失败和数据库事实边界未改；既有持久证据复用。 |
| FIX-008 职责级永久契约测试 | **CLOSED** | 新await调用点纳入当前成功/异常ambient和scope回归；buffer/启动清理未变。 |
| FIX-009 最小Consumer注册/启动 | **CLOSED** | 注册与Consumer算法未改；当前修复也未改source runtime启动；已验收真实双进程重启/exact rows保持有效。 |

没有因相同文件出现编辑就无条件作废全部历史，也未将历史完整E2E等同于本轮新跑。旧830/882/2398矩阵仅按未变范围复用，不与本轮12条专项结果相加；历史Sample final2选择与SQLite skip边界维持。

## T01–T07 接受情况

| Task | 状态 | 依据 |
| --- | --- | --- |
| T01 起点/CAP协议 | PASS | 原生8.0.1协议和所有权未变，既有验收有效。 |
| T02 Uow资源/增量契约 | PASS | 物理资源、Dapper/EF路径未改，未重开兼容性。 |
| T03 多源CAP runtime/借用 | PASS | 唯一剩余FIX-002闭环，scope顺序/成功发布/ambient直接回归通过。 |
| T04 Sample/Consumer/三入口 | PASS | 本次不改消费/三入口算法，既有真实重启与exact rows有效。 |
| T05 诊断页面 | PASS | 既有真实交互/SQL投影验收保持；Dashboard目标未验证单列。 |
| T06 验证/文档 | PASS | 最新追溯映射、最终net6/net8精准TRX真实；永久回归和独立反证闭环。 |
| T07 独立验收 | PASS | 获授权专项完成，原9项全部关闭，未自动修复。 |

Implementation Progress：**9/9 CLOSED**；T01–T07 **7/7 PASS**。沿用八组已批准代码验收视角 **8/8通过**；外部未验证仍单列，所以本报告不把Release状态写成全环境完成。

## 边界、分组与停止

```text
Task: 20261021-multisource-cap-diagnostics
Round: 4（用户仅授权的追加专项复验）
Changed Scope: review.md + artifacts/review-round-4；历史Round3报告仅验证原字节一致
Validation Run:
  L0: 当前源码/真实差异/断言/归档/候选和最终两TRX审计
  L1: 独立单线程context真实MySQL probe，两分支；新永久回归net8独立1/1
  L2: 复用指定net6/net8各6/6职责集，不重跑全集
  L3: probe/永久测试中的真实MySQL及独立两库查询；未重复Consumer/E2E
  L4: 未运行，无扩大依据
  L5: NOT_APPLICABLE
Reused Evidence: 其余8个CLOSED及旧完整矩阵的未变职责；Consumer重启/UI/outage/幂等
Completed: FIX-001–009全部CLOSED；T01–T07接受；独立报告及原始证据
Open Actionable: none
Blocked Approval: none
Blocked External: none（未验证项未被伪装成环境失败）
Not Applicable: 本次benchmark、版本/API baseline/性能预算修改
Accepted Limitations: 顺序跨库非原子；内存Hangfire；双MySQL不等于异构验证
Verified Boundaries: 旧候选死锁已确认但不再属于当前候选；无新增容量/性能结论
Deferred: none
External Not Verified: Dashboard目标未配置；历史两个SQLite证据probe未执行，均不计PASS
No-Progress Check: CHANGED（相关候选/永久测试改变且独立闭环）
Next Action: STOP，无剩余可执行问题，不进入其他修复轮
Goal Status: STOPPED_LIMITATION（实现及专项测试PASS，外部未验证/批准边界保留）
```

任务Compose MySQL/RabbitMQ仍healthy；5088默认SQLite仍PID55756；自有probe/test进程均结束，历史数据保留。见 [end-integrity.json](artifacts/review-round-4/end-integrity.json)。未改生产、已有测试、execution、plan、traceability、delivery-status、包/API baseline/预算；未暂存/提交/推送/PR或发通知。
