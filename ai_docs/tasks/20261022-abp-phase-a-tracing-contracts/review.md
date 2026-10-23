<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261022-abp-phase-a-tracing-contracts
AI_REVIEWED_AT: 2026-10-22T13:05:46Z

# 阶段 A 独立 Review 2

原 FIX-001、FIX-002 均为 CLOSED，关闭进度 2/2，OPEN_ACTIONABLE=0。本轮仅审查 Fix Round 1 及合理回归面，未发现需要继续修复的生产、测试或文档行为问题；一处测试命名空间表述列为 LOW / OPTIONAL / DEFERRED，不阻止本轮验收。阶段 A 两项修复验收通过，全局发布继续为 FAIL；不将两者合并为一个通过结论。

```text
AI_REVIEW_SCOPE: FIX-001 / FIX-002 / Fix Round 1
IMPLEMENTATION_STATUS: PASS
PHASE_A_FIX_ACCEPTANCE_STATUS: PASS
TEST_STATUS: PASS (本轮受影响 L1/L2)
CURRENT_FINAL_FULL_SUITE_STATUS: FAIL (沿用原 L4，非本轮新全量结果)
FAILURE_CAUSALITY: NOT_DETERMINED
DOCS_CHECK_STATUS: FAIL (沿用 UseSqlite 悬空符号结果)
PERFORMANCE_EVIDENCE_STATUS: OBSERVED (复用原 current-only probe)
RESOURCE_EVIDENCE_STATUS: NOT_APPLICABLE
THRESHOLD_STATUS: BLOCKED_APPROVAL (未设预算)
EXTERNAL_GATE_STATUS: BLOCKED (既有 clean-source 发布证据门禁未验收)
RELEASE_STATUS: FAIL
OPEN_ACTIONABLE: 0
GOAL_STATUS: STOPPED_BLOCKED
NEXT_ACTION: STOP
```

## 范围、规则与候选

仓库为 `H:/Bing_Framework/Bing.NetCore`；已读取根 AGENTS、GOAL_RULES、review-code 技能、原 Review 1、更新后的 execution/traceability，复核 plan 中与两项 Finding 有关的验收条件。本轮新增证据根目录为 [artifacts/review-fix-round-1](artifacts/review-fix-round-1/)，以下 L1/L1-L2 路径均相对此目录。原审查结论保留为两项历史问题，当前状态以本报告为准。

Fix Round 1 的相关变更为新增 `CapTraceUnitOfWorkCancellationTest.cs`、AOP.Tests 增加 test-only Bing.Events ProjectReference、EventBus 直接 Invoke 断言修订、Uow README 改为链接既有 Sample，以及任务报告更新。AOP 项目差异仅增加现有项目引用，没有新增 NuGet、生产依赖或 TFM 改动。CapTraceAttribute 与 Admin CapModule 的当前源码散列仍与原阶段 A manifest 一致；本轮没有生产修复。旧 manifest 中测试源码/测试 DLL 因本轮修改而变化，不能继续把旧清单写成“全部匹配当前候选”。

本轮只读解析原始 TRX、命令/退出码及构建输出；机械证据核对交给 luna_worker，主要调用链判断与最终结论由 Reviewer 完成。新增组合测试和 EventBus 直接测试的当前源文件另与两个 TFM 的 portable PDB 文档 SHA256 核对，均匹配；4 个 PDB 的 CodeView GUID 均匹配对应当前 DLL。核验的源 SHA256 为：

- CapTraceUnitOfWorkCancellationTest.cs：`32FF998D3A743B1F509F1CF1B19E1191CA13E2F590C139548014AD9F0D1FF432`。
- CapTraceAttributeTest.cs：`61EB9E2114F16B06FD0FAE42ED6BA33112C37C794D927FFE9FF30E2B37C9F2FF`。

这些核对将当前测试源码与当前编译产物关联起来。Round 1 未另存完整候选哈希清单；原始命令/TRX 指向相应 Release/TFM 输出路径，不将其提升为不可变的全仓发布候选证明。

## Finding 状态与关闭证据

| Finding | 原严重程度/要求 | Review 1 | Review 2 |
| --- | --- | --- | --- |
| FIX-001 真实代理取消与外层成功路径覆盖不足 | MEDIUM / SHOULD_FIX | OPEN_ACTIONABLE | CLOSED |
| FIX-002 SQL 文档主例缺少编译对应物 | MEDIUM / SHOULD_FIX | OPEN_ACTIONABLE | CLOSED |

### FIX-001：CLOSED

新增测试位于 `framework/tests/Bing.Aop.AspectCore.Tests/CapTraceUnitOfWorkCancellationTest.cs`，真实命名空间为 `Bing.Aop.AspectCore`。CreateHost（第 175 行）使用 UseServiceContext、EnableAop 与 EnableBingUnitOfWork，接口方法同时标记 CapTrace / UnitOfWork，解析出的服务通过实际代理执行。测试实际调用各返回形态的业务方法，已取代原来只遍历方法元数据的覆盖声称。

| 精确测试方法（类前缀 Bing.Aop.AspectCore.CapTraceUnitOfWorkCancellationTest） | 关闭条件核对 |
| --- | --- |
| Proxy_SynchronousCancellation_ShouldPreserveExceptionAndSkipSuccessPath | 同步业务抛原 OCE，调用方观察同一异常/token，目标 1 次。 |
| Proxy_PreCanceledAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath | Task、Task<T>、ValueTask、ValueTask<T> 四行理论数据，调用前取消。 |
| Proxy_SuspendedAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath | 四种异步返回形态，先等待 TargetEntered，再取消令牌；不靠延时碰运气。 |
| Proxy_Success_ShouldRunOuterPathAndCommitWorkUnit | 成功结果 42，目标、外层进入/成功、Commit、CommitAfter 各 1，证实代理和真实 Uow 拦截器确实参与。 |

公共断言（第 144 行）检查同一取消异常/token、目标恰好一次、业务看到独立 cap-trace 上下文、父引用与四字段恢复、外层成功数 0、IUnitOfWorkManager.CommitAsync 调用数 0 和 ICommitAfter 数 0。记录 manager 是 IUnitOfWorkManager 的测试替身；参与执行的 Uow 拦截器是实际 EnableBingUnitOfWork 注册的实现，没有把它替换成 fake delegate。ValueTask 使用 IValueTaskSource，并断言 GetResultCount=1；调用方只消费一次。

直接 `Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenCanceled_ShouldFaultReturnedPipelineTask` 在第 126 行显式检查 pipeline.IsFaulted，并检查同一 OCE/token。该断言与真实代理组合各司其职，现已满足原 Finding 的状态保护及真实分派要求。

首个有效组合失败尝试保留于 `L1/aop-net6-focused-attempt-2/`：7 Passed / 3 Failed，退出 1；诊断目录 `L1/aop-net6-diagnostic/` 为 2 Passed / 3 Failed，含 target=1、outerEntered=1、outerSucceeded=0、commits=0 及同步 NRE。它们不能作为最终通过结果，也没有证明阶段 A 生产代码新缺陷。最终 OuterSuccessPathInterceptor（第 460 行）原样返回 nextTask，只用 OnlyOnRanToCompletion continuation 记录成功，避免 async/await 观察器将 OCE 重新包装成 Canceled Task。该修正与原日志/Execution 的夹具干扰说明一致；无需回滚生产代码再造 before 红。

最终聚焦两 TFM 各 10/10、完整 AOP 各 148/148、EventBus 直接及项目回归通过，详见下表。原 FIX-001 的验收闭环，无需再改生产适配器。

### FIX-002：CLOSED

`framework/src/Bing.Uow/README.md:97` 已移除缺少编译对应物的 ReconcileOrders 内联长例，改为精确链接：

- `samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs#L70`：ExecuteAsync 中第 137 行 MultiDatabaseCommit 写 orders/reporting，第 145 行 CapMultiSourceCommit 将各库 Outbox 纳入相应流程。
- `samples/Bing.Samples.SqlUnitOfWork/Startup.cs#L86`：AddUnitOfWorkScopes；相邻代码明确注册两数据源，按 SQLite 与 CAP/MySQL 模式装配。
- Sample README 的 CAP 多源模式锚点存在，明确额外的 MySQL/RabbitMQ 配置。

链接均落到当前实际源码/章节，装配说明与源码一致。README 继续说明资源按登记顺序提交、后续失败可能部分提交、非事务 scope 不回滚已执行写入。已有最小旧提交/新 scope 编译测试未受影响，复用原 net6/net8 各 3/3 结果。

Sample csproj 仍仅 TargetFramework=net6.0，未为满足“两 TFM”文字扩改项目。原 Finding 的两 TFM 要求适用于现有双 TFM 测试项目；对原生 net6 Sample 按既有 TFM 编译即可。有效 build 的退出码 0、0 错误足以闭合这条文档编译链接路径，不等于本轮运行了 Sample 数据库或 broker 集成。原问题是“缺少编译对应物”，没有证据称旧内联样例实际编译失败；本轮也不作该声称。

## 原始验证证据

以下按指定 TRX 的 UnitTestResult 节点独立重计，均与原始命令、退出码和更新报告一致，没有用 execution 的汇总代替原始结果。

| 范围 | 精确证据路径 | 结果 |
| --- | --- | --- |
| 真实组合 net6 L1 | L1/aop-net6-focused-after-observer-fix/cap-trace-uow-net6-focused-after-observer-fix.trx | 10 Passed / 0 Failed，退出 0 |
| 真实组合 net8 L1 | L1-L2/aop-focused-net8/cap-trace-uow-net8-l1.trx | 10 Passed / 0 Failed，退出 0 |
| AOP net6 L2 | L1-L2/aop-full-net6/aop-net6-l2.trx | 148 Passed / 0 Failed，退出 0 |
| AOP net8 L2 | L1-L2/aop-full-net8/aop-net8-l2.trx | 148 Passed / 0 Failed，退出 0 |
| CapTrace 直接回归 net6/net8 | L1-L2/eventbus-captrace-net6/captrace-eventbus-net6-l1.trx；net8 对应目录/文件 | 各 24 Passed / 0 Failed，含 IsFaulted 方法，退出 0 |
| EventBus net6 L2 | L1-L2/eventbus-full-net6/eventbus-net6-l2.trx | 409 Passed / 0 Failed / 1 NotExecuted，退出 0 |
| EventBus net8 L2 | L1-L2/eventbus-full-net8/eventbus-net8-l2.trx | 409 Passed / 0 Failed / 1 NotExecuted，退出 0 |
| Sample net6 build | L1-L2/sample-sqluow-build-net6-valid/command.txt、exit-code.txt、stdout.txt | Release build 退出 0，3 个既有 TFM 生命周期警告、0 错误 |

每个目录有对应命令和退出码。Sample 早期 `sample-sqluow-build-net6/` 把 test 的 --logger 选项用于 build，退出 1，输出 MSB1007；它是无效命令尝试，不是 SQL 样例编译失败。后续有效命令使用 `--verbosity minimal`。默认 probe 的 NotExecuted 与正确性通过分别计数。

## NOTE-001：测试命名空间表述（不阻止验收）

严重程度 LOW；处理要求 OPTIONAL；状态 DEFERRED。traceability.md 中新增测试被写为 `Bing.Aop.AspectCore.Tests.CapTraceUnitOfWorkCancellationTest`，其中 .Tests 是项目名的一部分，实际代码及 TRX 类型名是 `Bing.Aop.AspectCore.CapTraceUnitOfWorkCancellationTest`。原始命令使用类名过滤，现有运行结果及查找不受影响；本报告已给出精确名称。建议下次维护阶段 A traceability 时统一该表述；不影响本轮两项验收，不构成发布阻塞，不触发新 Fix 循环。

## 证据复用与阶段/发布边界

本轮复用原阶段 A 已审查的 Admin 真实 CAP、Uow 最小示例、未变生产范围的 probe 与原 L4 失败证据。新增 test-only 引用的影响由 AOP 两 TFM 完整项目回归覆盖；EventBus 测试改动由两 TFM 直接/完整回归覆盖。没有生产行为变化，不要求再做 broker/数据库、全 Solution、GC 或 probe 实验。

原 L4 仍为 12,625 Passed / 2 Failed / 1,108 NotExecuted，103 份 TRX；两条 Core GC 失败因果保持 NOT_DETERMINED。单方法隔离各 1/1 Passed 只代表隔离观察，不覆盖全量结果。原 Review 1 对这些原始材料的核验结论复用，不重新运行或重新归责；本轮测试 DLL 已变化，旧 L4 不是本轮含新增测试候选的全量运行，也不通过叠加新增通过数生成当前“全量 PASS”。

原 docs checker 的 UseSqlite 悬空符号结果仍为 FAIL。GC 因果调查继续 DEFERRED 给 full-suite recovery 后续专项；UseSqlite 文档遗留继续 DEFERRED 给 G12/阶段 B 文档工作；二者影响全局发布，但不重新打开已关闭的 FIX-001/FIX-002。既有 clean-source 门禁与未设性能预算分别保留阻塞状态。A01 历史 before 红覆盖的限制沿用，用户明确不要求重建 before，本轮不把缺少历史材料转成新修复。

## 本轮汇总与停止点

Change Impact Analysis：被审查修改只涉及 AOP/EventBus 测试、test-only 项目引用和 README，PublicContracts/RuntimePaths/Providers/TFMs/生产依赖/BenchmarkHarness 无变化；受影响项目为 AOP.Tests 与 EventBus.Tests，合理范围为 L1/L2 加现有 net6 Sample 编译。Reviewer 自身只更新 review.md，ChangedDocsOnly=true，RiskLevel=LOW。

本轮 L0：限定文件 git diff --check 无输出；只读核对 TRX、命令、链接及当前测试源码/PDB/DLL 对应关系。L1/L2：复用 Fix Round 1 上表有效结果；L3：复用未变的 Admin 真实 CAP；L4：保留原 FAIL；L5：未运行。没有重复代码测试、全量、GC 或 before 实验。

- Completed：FIX-001、FIX-002 已独立关闭，2/2；Review 2 交付。
- Open Actionable：none，0。
- Blocked Approval：性能预算未设定。
- Blocked External：既有 clean-source 发布证据门禁未验收。
- Not Applicable：本阶段容量矩阵；未建立可比 before 的性能比较。
- Accepted Limitations：原显式 Trace 入口、CAP 8.0.1 边界、FreeSQL 未新增适配与 current-only probe 口径沿用。
- Verified Boundaries：none，GC 结果不是已确认资源边界。
- Deferred：NOTE-001 文档名称；原 Core GC 调查、UseSqlite 文档遗留及后续路线图能力。

Task：20261022-abp-phase-a-tracing-contracts；Round：独立 Review 2。Implementation Findings：2/2 CLOSED；Release Gates：未通过，原 L4/docs FAIL 及既有门禁仍在，不制造全局完成比例。No-Progress Check：CHANGED（两个原 Finding 已闭环）。Next Action：STOP。阶段 A 修复验收结束；全局发布状态另列，不自动进入下一轮 Fix 或阶段 B。
