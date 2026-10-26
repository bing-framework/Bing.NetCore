<!-- AI_EXECUTION_STATUS: IN_PROGRESS -->
AI_TASK_ID: 20261022-abp-infrastructure-roadmap
AI_EXECUTION_STARTED_AT: 2026-10-22T11:01:37.307Z

# 路线图执行状态

当前限定修复已收口：Astra/xhigh确认受控DI后台编译引用机制；OpenCode/deepseek-v4.1-flash落实生命周期修复与构建恢复；[Sol/high Review2](../20261023-module-lifecycle-retention-fix/review.md)及[构建恢复专项审查](../20261023-module-lifecycle-retention-fix/build-recovery-review.md)均PASS、OPEN_ACTIONABLE=0。主线程已读取实现线程完成结果，并独立解析新候选的103份TRX及命令/退出记录：Release构建exit0（106 warnings、0 errors），默认全量exit0，12,894 Passed / 0 Failed / 1,114 NotExecuted；6份空TRX不计通过。net6为6,722/0/593，net8为6,172/0/521；两个原GC回归及六个生命周期直接回归两TFM均通过。冻结/结束/当前12项范围输入身份一致。详见[最终报告](../20261023-module-lifecycle-retention-fix/final-report.md)与[主线程原始结果核验](../20261023-module-lifecycle-retention-fix/artifacts/main-thread-final-verification.json)。

当前候选的本地构建与默认全量门禁通过；不等于外部系统、clean-source发布证据或性能预算全部通过。本轮关闭外部门控，跳过项单列；未重跑已接受的受控GC实验及未变范围外库证据。旧final-candidate-checkpoint的CS2012 build FAIL/test NOT_STARTED、历史Stage B全量FAIL与Astra原始证据保留。历史锁持有者及GC自然失败的排他归因仍未确定，受控证据不表示已证明永久泄漏。阶段B实现和当前本地全量已通过，FreeSQL B04及阶段C/D仍需各自计划，不宣称G01–G12全部完成。

本轮状态：Completed＝GC实现/直接回归独立验收、构建恢复独立验收、最终本地build/test与原始结果核验；Open Actionable＝0；Blocked Approval＝既有APR-001性能预算；Blocked External＝既有EXT-002受保护发布证据，本轮外部系统未执行；Not Applicable＝无关性能矩阵与旧GC假设重跑；Accepted Limitations＝历史排他归因未定、无永久回收保证；Verified Boundaries＝受控引用机制及新候选两TFM回归通过；Deferred＝FreeSQL B04、阶段C/D。No-Progress＝CHANGED（新构建候选完成最终门禁）；Next Action＝STOP本次限定修复，不追加Fix/Review或全量重跑。路线图整体仍IN_PROGRESS。工具Review5的3/3 CLOSED保持，详见[工具审查](../20261023-module-lifecycle-gc/review.md)。

## 历史过程

以下记录保留各历史候选当时的状态与证据，当前结论以上方状态及新候选最终报告为准。

GC 第三轮复核已结束：NEEDS_FIX，FIX-001 CLOSED，FIX-002/003 仍为两个 SHOULD_FIX，无生产 MUST_FIX。剩余范围已收窄为三处报告事实冲突，以及诊断 runner 在目录/日志准备后缺最后截止准入、已启动状态记录不准确、PID 发布异常时缺自身进程 finally 清理。Reviewer 用一次纯虚拟控制复现，未运行真实产品测试。当前默认关闭的 hook 在已审范围可保留，但工具未完全验收；GC 根因仍 NOT_DETERMINED，问题 UNRESOLVED，不能把工具整改当根因进展。三轮边界已到，主线程未派发第四轮或新采证。若追加限定收尾，范围只包括这些报告/脚本修正与两条既有虚拟反例确认，不增加真实build/Core/solution/GC/dump运行；后续须有明确追加授权。

GC Fix2 已交付：OpenCode 报告修正 FIX-002 候选/历史证据标签及 FIX-003 截止控制，本轮仅静态检查与虚拟时钟验证，无真实构建/Core/solution/GC/压力/dump。Sol/high 第三轮独立复核已启动，按原两项 AC 检查报告与 runner，完成后停止交回。诊断修正仍不等于生产 GC 修复，原因继续 NOT_DETERMINED，当前正式 solution FAIL 与历史 Stage B FAIL 均保留。

GC Review 2 最新判定：FIX-001 CLOSED，FIX-002 当前/历史候选账本仍需修正，新增 FIX-003 为诊断 runner 跨截止后仍可启动 test、timeout 控制缺外层 watchdog。两项均 SHOULD_FIX，无生产 MUST_FIX；主线程已交 OpenCode 限定修正报告及工具，仅静态/纯虚拟时钟确认，不再执行真实构建、Core、全量、压力或 dump。Sol/high 独立分析确认：Shutdown 的发布信号早于异步尾部只在真实挂起/并发前提下形成窗口，两个实际失败夹具均同步且没有该前提证据，因此不支持将 H6 当实际 GC 根因或据此修生产。根因继续 NOT_DETERMINED，GC 问题未解决；完成限定 Fix2 后安排第三轮复核。

GC 最新交付：OpenCode 已报告完成 Review 1 的两项静态修正及一次正式采证，Sol/high 的 GC Review 2 已启动（与阶段 B 的 Review 编号独立）。主线程直接解析 `gc-capture-20261023-01` 的 103 份 TRX：net6 为 6,715/1/593，net8 为 6,166/0/521，6 份零结果另列；总 12,881 Passed / 1 Failed / 1,114 NotExecuted。原目标测试两 TFM 均通过，无目标 ready/dump；新观察到 net6 `RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` 的 provider 弱引用仍存活。人为控制 dump 只验证工具，不是生产失败现场。实现者提出 Shutdown 完成信号先于异步清理退出的候选原因，尚无引用根证明，根因继续 NOT_DETERMINED。Reviewer 将核对该假设是否可由确定性信号控制回归证伪；不追加全量或压力重跑。

GC 调查最新进展：Sol/high 已完成 `20261023-module-lifecycle-gc` 的 Review 1，诊断首失败条件通过三个有限对照，报告存在两个 SHOULD_FIX（结论范围、证据来源/实验偏差/文本准确性），没有生产修复要求。主线程已交 OpenCode 静态修正这两项，并采用 Reviewer 的一次性活进程采证方案：先验证默认关闭的失败握手及监视器，再在一个固定候选上执行一次有绝对截止的构建/默认全量，失败时最多一份 dump。正式批次上限15分钟、失败等待30秒、collector20秒；不续增、不重跑同假设。采集/分析工具缺失或未复现均如实停止，根因仍 NOT_DETERMINED。Reviewer 原始结论与历史 Stage B 结果保留。

最新后续：用户于 2026-10-23 明确要求“继续排查 GC 问题”。原 [OpenCode / deepseek-v4.1-flash 线程](codex://threads/146cb27d-d5e7-4a10-8eb0-2e6b4ddcb905)已交付独立调查 `20261023-module-lifecycle-gc`：未复现历史失败，根因仍 `NOT_DETERMINED`，未修改生产，仅增强弱引用失败诊断。原 Sol/high 已启动该新任务的独立复核，检查诊断是否保留原场景、额外观察是否影响生命周期，以及报告中的排除结论、重复实验偏差和候选归属。当前局部/压力通过不能证明历史 GC 问题解决；原 DEF-001 为已授权调查，历史失败和阶段 B 验收结论不改写。主线程未继续重复测试，后续采证以独立复核的可执行结论为依据。

按用户已批准路线图分阶段推进。阶段 A 已完成实现与独立验收；阶段 B 经用户于 2026-10-23 追加限定授权，由 OpenCode / deepseek-v4.1-flash 修复、Sol/high 完成 Review 4，FIX-005 已关闭，原八项全部 CLOSED、OPEN_ACTIONABLE=0。阶段 B 实现及受影响测试验收 PASS；最后检查点已执行并由主线程核对：Release 构建 PASS，默认全量 12,881 Passed / 1 Failed / 1,114 NotExecuted，唯一失败仍为既有 Core GC 弱引用断言，因果未定。停止本次限定修复循环，不宣称阶段 Release 或 G01–G12 全部完成。

| 阶段 | 当时状态 | 当时交付/下一步 |
| --- | --- | --- |
| A：G01/G12 | 实现验收通过，Review 为 PASS_WITH_ISSUES，OPEN_ACTIONABLE=0 | [交付状态](../20261022-abp-phase-a-tracing-contracts/final-report.md)；停止阶段内 Fix |
| B：G02/G03/G09 | 实现/受影响测试 PASS；8/8 CLOSED；最终构建 PASS，全量 12,881/1/1,114 | 本次限定任务停止；B06 PARTIAL，GC 原因独立递延，FreeSQL B04 待单独计划 |
| C：G04/G05/G06/G07 | 待后续子计划 | 服务验证、领域规则、授权、审计、租户数据边界 |
| D：G08/G10/G11 | 待后续子计划 | 事件整合、分层设置与功能检查 |

[阶段 B 设计任务](codex://threads/01a163c0-ca62-72b3-ab1e-595f7ffa2584)已交付[实施计划](../20261022-abp-phase-b-operation-uow/plan.md)。主线程已核对四项修订：恢复父路线图规定的阶段最终 Release 构建与默认全量检查点；纳入 G12 的最小 UseSqlite 示例说明及文档检查；覆盖真正的 LegacyMarkerConsumer 编译；区分 Provider runner 与普通 VSTest 的显式 runsettings 入口。规划阶段未运行代码测试。

[阶段 B 实施任务](codex://threads/01a163e8-2977-77d3-940a-dc5a8417c2e5)已交付[执行报告](../20261022-abp-phase-b-operation-uow/execution.md)与[候选报告](../20261022-abp-phase-b-operation-uow/final-report.md)，报告主线 10/10。主线程已读取完成结果，并直接解析最终 L4-v2 的 103 份 TRX：12,715 Passed / 1 Failed / 1,112 NotExecuted；唯一失败是 net8 Core GC 弱引用断言，因果仍未确定。CAP 集成 TRX 为 22 Passed；最终 docs checker 原始输出为 13 项通过、退出码 0。这些数量不替代功能覆盖验收。

[阶段 B 独立审查任务](codex://threads/01a16475-db5c-7be2-b521-a9d71ae38afa)已交付[Review 1](../20261022-abp-phase-b-operation-uow/review.md)，结论 NEEDS_FIX：8 个 OPEN_ACTIONABLE（5 MUST_FIX、3 SHOULD_FIX）。审查发现旧标记绕过 participant、Saving 后 rollback-only 未阻止提交、EF/participant 取消未进入真实 I/O、同 DI requiresNew 提前保存外层 pending Context、executor Trace 引用污染，以及新模式测试和文档交付不足。严格任务验收为 1 PASS / 3 PARTIAL / 6 FAIL，原实施者 10/10 自评不作为收口结论。

原 Luna/max 实施任务已交付 Fix 1，报告完成 FIX-001–008 的实现和新模式直接回归。当前候选为 `phase-b-review-fix-round-1-2026-10-23-v1`，主线程已读取完成结果并启动原 Sol/high 的 Review 2，要求逐项按原验收条件判断，保留 Review 1。最多三轮，只有明确可执行问题才继续。

主线程已直接解析 Fix 1 最终全量原始 TRX：103 份，12,828 Passed / 1 Failed / 1,114 NotExecuted；唯一失败仍为 Core GC 弱引用测试，本次发生于 net6，原因未定。docs/direct-python 原始输出为 13 项通过，exit.txt 为 0。旧 22/22 仍只代表其已运行路径。

Review 2 已关闭 FIX-001/002/004/006；剩余 FIX-003 为同步 Store 预取消仍发 SQL，FIX-005 为同 DI requiresNew 显式 Save/Commit/claim 绕过外层归属保护及误拒绝合法 Context，FIX-007 仅剩新 automatic 外层观察器和取消返回矩阵，FIX-008 为报告 skip 数量和候选清单完整性。Fix 2 限定处理这四项；已关闭行为不无因重做。先精准 L1→受影响 L2/SQLite，第三轮复审后安排原计划所需最终候选检查点，不因修正报告再次跑全量。

Fix 2 候选为 `phase-b-review-fix-round-2-2026-10-23-v1`，报告已补齐 262 项候选清单和原 14 组 L2 核对。主线程读取交付并逐结果检查了本轮 TRX：最终 AOP net6/net8 各 178/0/0，EF net6 219/0/0，SQLite 两 TFM 各 233/0/1，Uow 各 70/0/0，EventBus 最终各 409/0/1；其初次各 408/1/1 的失败迭代保留，不拼入当前通过。原 Sol/high 的 Review 3 已关闭 FIX-003、007、008，维持既有四项关闭，确认候选清单与原始证据；唯一剩余 FIX-005 已用当前 DLL 和真实 SQLite 复现。

FIX-005 的反例为：同一 DI scope 内，root A 留有 pending 实体；进入独立 root B，再进入 C，C 覆盖 EF Context 的单槽保护标识；退出 C 后，B 的自动 Complete、显式 SaveChanges 或 Commit 均可能保存并提交 A 的实体，A 随后回滚也无法撤销。直接单层对照会正确拒绝，三种嵌套反例均已产生持久业务行。证据保存在阶段 B 的 `artifacts/review-round-3/nested-root/`，详见 [Review 3](../20261022-abp-phase-b-operation-uow/review.md) 的 FIX-005。

限定追加建议：Luna/max 将 EF 归属保护改为可保留每层活动 root 约束的机制，正确处理进入、退出与 Begin 失败；补充实体、action、event、claimed 状态的嵌套回归，覆盖自动完成和同步/异步 Save、Commit、query-first claim 的写入前拒绝，并保留合法 inner-only 和新 DI scope executor 的成功对照。先运行精准回归与受影响 Uow、EF、SQLite 测试，再由 Sol/high 独立核验这一项；通过后执行既定最终候选构建与全量检查点。此建议尚未执行，不重开已关闭项、不扩大到 FreeSQL/C/D。

原停止依据是阶段 B [计划 §13](../20261022-abp-phase-b-operation-uow/plan.md) 的“Review→Fix→Review最多3轮”。用户随后明确追加一次限定授权并调整实现模型，见[授权记录](../20261022-abp-phase-b-operation-uow/limited-fix-authorization.md)。[OpenCode 实现线程](codex://threads/146cb27d-d5e7-4a10-8eb0-2e6b4ddcb905)已启动，交付后沿用原 Sol/high 独立复核；不重新开启无限循环。上述 Luna/max 为先前建议，本次以用户新指定的模型执行。

OpenCode 已交付候选 `phase-b-review-fix-round-3-fix005-2026-10-23-v1`，报告将单槽 EF 保护改为逐 root 记录，并在 root 退出/启动失败时清理相应记录。主线程读取线程结果与原始 TRX，确认精准 L1 最终 16/0/0；完整 EF net6 235/0/0、Uow 两 TFM 各 70/0/0、SQLite 两 TFM 各 233/0/1，退出码均 0。红证据中 14 项为目标三层反例；observer 初始夹具未触发 DetectChanges，修正后旧生产版本该项通过，因此不将它记为已证明的旧生产失败。上述结果不替代独立验收。证据位于阶段 B 的 `artifacts/review-fix-round-3/`。

原 Sol/high 线程发送时因归档拒绝且明确未启动；恢复归档后已成功提交本次追加独立复核，并注册完成监听。Reviewer 仅检查 FIX-005 及直接影响，保留原七项关闭和未变外部证据，不自动启动下一轮修复。

Review 4 已完成，独立 SQLite 观察确认原三条中间 root 失守路径均在 SQL 前拒绝；双 Context 的部分捕获后 Begin 失败只清除失败 root，保留中间 root 保护，正常退出后保护记录归零；合法 inner-only 与独立 executor 对照通过。四项 AC 均满足，FIX-005 CLOSED；主线为 9 PASS、B06 1 PARTIAL，不开启新的修复循环。

原 OpenCode 线程已完成 plan §11.3 最后门禁，主线程读取 completed 结果并独立解析 103 份原始 TRX（6 份零结果单列）：net6 为 6,716 Passed / 0 Failed / 593 NotExecuted；net8 为 6,165 / 1 / 521。Release 构建退出 0，199 warnings、0 errors；完整测试退出 1。唯一失败是 `Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`（net8），断言 `references.All(r => !r.IsAlive)` 为 False，沿用 `GC_FAILURE_CAUSALITY=NOT_DETERMINED`，不隔离追绿或扩修。

最后候选为 `phase-b-review-fix-round-3-fix005-2026-10-23-v1`；执行者记录冻结前后 262/262 路径匹配。独立子进程关闭外部/probe gates、清理连接变量，保留默认 SQLite 与并行设置；命令、原始输出、子进程退出码、逐项结果见[最终证据](../20261022-abp-phase-b-operation-uow/artifacts/final-candidate-after-fix005/checkpoint-summary.json)。构建与测试各只执行一次。外围 PowerShell `Start-Process -Wait` 曾继续等待 MSBuild node-reuse 后代并超时，实际 build/test 早已结束；[wrapper-note](../20261022-abp-phase-b-operation-uow/artifacts/final-candidate-after-fix005/wrapper-note.md)保留该限制，不能把外围超时等同于测试超时。产品文档未变，复用已接受 checker/编译例。

本轮收口：Implementation PASS；受影响 Test PASS；Release build PASS；默认全量及 Release FAIL；OPEN_ACTIONABLE=0，不开启额外 Fix/Review。EXT-002 受保护发布证据、APR-001 预算与 GC 原因分别记录；FreeSQL B04、C/D 仍按原路线图后续独立规划。task-finish 的 COMPLETED 只表示执行流程结束，不代表全量或发布 PASS。

Review 2 按真实代码、进程、精确行集和 journal 关闭 EXT-001。独立 consumer 证据中七项经过新 executor；第八项部分提交仍是已开启 operations 的旧手工入口，其 root 进入新保存/完成协议。不得称八项全经过新 executor，也不虚构未归档的子进程退出码。完整 L2 的门控跳过由本轮复审纠正：Sample 每 TFM 26 Passed / 0 Failed / 18 NotExecuted，SQLite 233 / 0 / 1；不把 TRX 汇总 Counters 的错误 0skip 当逐结果事实。

阶段 B 的已核实事实与代理取消经验见 [设计输入](phase-b-design-inputs.md)。维持旧默认行为，新能力 opt-in，不替换 AspectCore 或引入 ABP 包。

全局发布仍未通过：A 历史 L4 的两项 Core GC 失败及 B Fix 1 同方法失败保留；当前 Fix 3 实现已通过独立验收，最后全量再次在 net8 出现同方法失败，原因仍未定，clean-source 发布证据门禁仍未验收。B 的 UseSqlite 文档调整后 checker 已通过，A 历史 docs FAIL 不改写。MySQL Provider 测试 TRX 虽无失败，受保护 runner 因 dirty source 退出 1；SQL Uow probe 的子进程退出码/stdout/stderr 未捕获限制也保留。不得把局部通过拼成全局发布 PASS，不通过重复全量试跑或清理工作区取得通过。

未暂存、提交或推送 Git；SDK、TFM、依赖版本、API baseline 与性能预算保持既定边界。
