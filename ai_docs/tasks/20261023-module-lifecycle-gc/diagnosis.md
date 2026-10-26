# Module Lifecycle GC 诊断

## 结论

- `INVESTIGATION_DELIVERABLE = 已交付`：源码分析、受控运行、失败采证 hook/监视器/collector 有限对照和一次有界 full-solution 采证均已交付。
- `TARGET_TEST_REPRODUCTION = NOT_REPRODUCED`：正式 full-solution 中目标测试 net6/net8 均通过，没有触发目标 hook/dump。
- `RELATED_WEAK_REFERENCE_FAILURE = OBSERVED`：同一正式运行 net6 出现 `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` 的 provider 弱引用失败；这是相关 GC 现象，但不是目标测试的 dump/根链。
- `ROOT_CAUSE = NOT_DETERMINED`：没有目标失败 dump/引用根；本报告只给出一个待验证的 shutdown 完成时序候选，不宣称确认。
- `GC_PROBLEM = 未解决`：历史 Stage B 最后 L4 仍为 **12881 Passed / 1 Failed / 1114 NotExecuted**，唯一失败仍是 net8 的 `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`；失败证据、FAIL 状态和因果 `NOT_DETERMINED` 均保持不变。
- 未修改 Bing.Core 生产代码；仅增强/新增测试失败诊断并修正本任务报告/注释。

## 假设 → 证据 → 当前判断

这里的“判断”只表示本轮证据的支持程度，不是对假设的永久排除。

| 假设 | 本轮证据 | 当前判断 |
| --- | --- | --- |
| H1 测试/JIT/断言局部生命周期 | `CreateClosedApplication` 保持 `NoInlining`；本次单测试、同类测试和完整 Core 的最终运行均通过；5,000 次单进程循环在 net6/net8 均通过；未观察到本地字段/闭包长期持有 target | 未获得支持证据；不能据此排除特定运行时的局部/JIT 时序 |
| H2 框架静态缓存/事件/单例 | 源码检查 registration/manager/descriptor/loader/ServiceLocator/StartupLogger：正常非 legacy 路径未见静态实例缓存；`BingLoader` 使用 `ConditionalWeakTable`；GC 测试某时点记录 `ServiceLocator enabled before GC: False` | 未发现可见静态根；这是有限观察，不构成对全部进程状态/调度下静态根的排除 |
| H3 DI/manager/registration 释放 | `ReleaseAsync` 清空 owned/released/modules、清除 descriptor 实例、移除已拥有模块描述符；`ProviderDispose_ReleasesOwnedModulesOnce` 和完整 Core 通过；5,000 循环和 12 个独立进程并发未出现残留 | 正常关闭路径未显示确定性泄漏；不能证明其它失败调度下没有临时/特定根 |
| H4 并行/其它测试静态污染 | `ModuleStaticCompatibility` 集合 `DisableParallelization=true`；完整 Core 多轮通过；6/12 个独立进程并发通过；但独立进程不共享托管静态字段，不能证明进程内污染已排除 | 未发现污染源；独立进程并发不是进程内静态污染的有效排除证明 |
| H5 GC/运行时/观察时序 | 历史 full-solution 目标失败约 41ms，36 个弱引用无类型/根信息；正式采证中目标测试 net6/net8 均通过，但相关 `RetainedServiceCollection` 测试 net6 失败且无目标 dump | 未确认；不是唯一剩余假设，也不能仅凭正式目标通过归因于 JIT/GC |
| H6 shutdown 异步完成协议（新增候选） | `BingModuleManager.ShutdownCore` 325 行 fire-and-forget；`CompleteShutdownAsync` 344–352 行先完成 TCS 后进入 finally。只有 Cleanup 真正挂起或存在并发 Shutdown 时才出现公开完成早于私有尾部的窗口；两个实际同步夹具没有该前提 | 未确认；不能解释两个同步夹具的实际失败，不支持生产修复 |

## 已观察事实与未确认边界

已观察到：

1. 本次受控的单次创建/关闭、完整 Core 多轮、6/12 个独立进程并发、5,000 次循环均通过；这些运行没有复现历史失败。
2. 在本次 GC 测试运行的某时点，`ServiceLocator` 绑定为 `False`。
3. `ProviderDispose_ReleasesOwnedModulesOnce` 等生命周期测试通过。
4. 历史失败 TRX 只有原始 Shouldly 断言，没有 provider/module/recorder 类型或引用链。

未确认：

1. H1–H4 是否在所有调度/输入下成立；本轮运行和源码检查不足以作全面排除。
2. 历史 full-solution 失败时的真实存活对象类型和根类别。
3. 是否存在只在完整 solution 的进程级 GC/CPU/内存压力或测试运行器/JIT/thread-pool/finalizer 时序下出现的临时强引用。
4. 测试增强本身带来的分配/JIT/观察时序变化是否改变与旧版的 GC 时序等价性；不能据此证明历史故障已解决。

因此本轮只报告“未复现”和“已检查路径未发现可确认持有”，不报告“已排除”或“唯一剩余”。永久强引用/根类别必须由引用根证据判断，不能由额外有界 GC 窗口推断。

## 测试诊断增强（本轮唯一代码改动）

文件：`framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs`

- `TrackedReference` 在构造时缓存 appId/role/完整类型字符串，并保存 `WeakReference`；`Describe()` 只读缓存的类型字符串，失败路径不读取 `WeakReference.Target`。
- 记录 provider、manager、recorder 和每个已解析 `module:<类型名>`；12 次应用共 84 个弱引用（旧版 36 个）。
- 标准 3 轮 GC 后若有存活，输出实际类型；再执行一轮**有界** 3 轮诊断 GC，仅记录第二个观察窗口是否仍存活，不推断永久性。
- 原断言 `alive.ShouldBeEmpty()` 仍以标准循环结果为准；额外循环不会把失败改绿。
- 该增强会改变分配/JIT/观察时序，未证明与旧版完全等价；不能把增强后的通过反推历史失败已修复。

## 原始证据与来源边界

- `artifacts/t00-startpoint/`：**运行结束后的收口捕获**，不是任务开始前快照。`capturedAt` 晚于首批 TRX；只能作为相对所捕获 HEAD 的文件比较。无可靠任务开始 HEAD/完整 dirty 起点记录。
- `artifacts/run-summary.json`：49 份 TRX 的逐 `UnitTestResult` 汇总；其中包含有效通过和 `full-net8-01` 的 0-test 无效运行，但**不包含**无 TRX 的 `probe-full-net8-02` 编译失败；该编译失败在独立目录/账本中记录，不能把 49 份都当作有效通过。
- 首轮 `final-target-net8`、`final2-full-net8`、`final2-full-net6` 是**历史候选**；当前正式候选是 `gc-capture-20261023-01`。当前候选的 build/test 命令、DLL/PDB hash 和时间关联可核对，但没有 build 前同步 source hash，故不称完整 source-at-build 冻结。中间 loop/prod/并发/5000 循环只是调查历史，不拼成当前候选。
- 历史 Stage B 最后 L4 失败证据和 Report/Review 不改写。

## 原下一步（历史，已执行）

1. 按 `artifacts/review-round-1/next-capture-plan.md` 准备默认关闭的失败采证 hook、外部监视器和 SDK DiagnosticsClient collector；已于本轮执行一次有界候选 full-solution 采证，不再重复。
2. 失败时以新 TRX 类型化首快照和活进程 dump 定位具体 appId/role/target 的真实强根；没有根链则保持 `CAPTURE_INCOMPLETE`/`NOT_DETERMINED`。
3. 全部通过只写本次候选未复现，不关闭历史 GC 问题，不追加压力或猜测性生产修复。该分支已执行；**停止重复采证**，后续只做报告/工具准确性修正。

## Step 2：一次有界 full-solution 失败采证

### 工具链有限对照

- 默认关闭的 `GcCaptureHandshake` 在首快照非空时发布 ready；控制失败测试验证了 monitor/collector 握手。
- `gc-control-ack-12`：monitor 验证 PID/StartTime/进程树后，collector 在 0.59 秒内写出 Full dump（163,458,799 bytes，SHA256 已保存），ack=`captured`；控制断言仍为 Failed，原失败未变绿。
- 无 monitor 的 `gc-control-timeout-01`：**父命令 elapsed 30.909 秒**；TRX 测试方法约 30.017 秒。ready 存在、ack 不存在、hook-error 存在，测试 exit1；原失败保留。不能把 30.909 秒称为 hook 精确计时。
- 强持有/释放后成功/首快照不被额外 GC 冲销，复用 Review 已接受的 `diagnostic-controls` 对照，不重复 Core/压力。
- 早期控制分类：`gc-control-ack-01` 仅有 deadline 元数据、缺测试/monitor 结果；02–05/10 为 `invalid-target`；06–09/11 为 `monitor-error`；12 是人工控制成功（dump 已保存）。不声称 01 有并不存在的失败日志；这些均不作为产品证据。

### 正式运行（仅一次）

- `gc-capture-20261023-01`；Release solution build exit 0，201 warnings / 0 errors，52.5 秒；绝对 deadline 15 分钟，无递增。
- `dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx`；exit 1，103 TRX、6 空 TRX；12,881 Passed / 1 Failed / 1,114 NotExecuted；net6 6,715/1/593，net8 6,166/0/521。
- 目标测试 `RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` 在 net6/net8 均 **Passed**；没有 ready/ack/dump，目标 hook 未触发，记为 `CAPTURE_INCOMPLETE`（目标未复现，不能宣称根因）。
- 唯一失败是 `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection`（net6），`snapshot.Provider.TryGetTarget(out _)` 为 True；该测试不在目标 hook 范围，未产生目标 dump。正式后对该失败方法做了一次精准单跑（`formal-failure-target-net6`，1/0/0，exit 0），未复现；这不构成因果证明。

### 候选根因（未确认，需 Sol/high 判断）

`BingModuleManager.ShutdownCore` 将 `CompleteShutdownAsync` 作为 fire-and-forget 启动，返回 `completion.Task`；`CompleteShutdownAsync` 在 `completion.TrySetResult`/`TrySetException` **之后**才进入 `finally` 设置 `_insideHook.Value=false`。等待 `Shutdown()` 的调用方可在异步状态机完全退出前继续释放 provider；该状态机仍持有 `BingModuleManager`，而 manager 的 `_provider` 字段仍指向 provider。测试随后立即做弱引用 GC，可能观察到 provider 仍存活。

源码位置：`BingModuleManager.cs` 325 行 fire-and-forget 启动，344–352 行先 `TrySetResult/TrySetException`、后 `finally` 清理。该窗口只在 Cleanup 真正挂起或存在并发 Shutdown 时成立；两个实际同步夹具均同步 UseBing/ShutdownBing/Dispose，没有未完成 await 或并发证据，因此不能用它解释这两个失败。当前没有目标失败 dump/引用根，不能升级为确认根因，也不能据此做生产修补。

### 停止状态

- 本次正式采证只运行一次；没有第二次 full-solution、没有新压力循环、没有无限 GC。
- 目标测试未复现，目标 `CAPTURE_INCOMPLETE`；相关失败保留为 `NOT_DETERMINED` 证据。
- 建议主线程/Sol/high 审查 shutdown 完成时序候选；若要验证，应先设计可重复的发布后引用根回归（不要靠更多全量运行），再讨论最小生产修复和正常/失败/取消生命周期回归。

## Review Fix Round 2（FIX-002 / FIX-003）

- 当前正式候选明确为 `gc-capture-20261023-01`；首轮 `final-target-net8`/`final2-full-*` 仅为历史候选，不再用于“当前源码”证明。
- 当前正式结果：build PASS；solution FAIL（12,881/1/1,114）；原目标 net6/net8 NOT_REPRODUCED；net6 `RetainedServiceCollection...` provider alive 相关失败。正式后精准单跑 1/0/0 不冲销正式 FAIL。
- 源/二进制：正式命令、mtime、事后再生成 manifest 与已记录 DLL/PDB hash 分别列示；没有 build 前 source hash 同步封存，不称完整冻结。
- `TrackedReference` 在构造时缓存 appId/role/类型字符串；`Describe` 失败路径只读缓存值，不读 `WeakReference.Target`；`run-summary.json` 的 49 TRX 与无 TRX 编译失败分列。
- H6 仅保留为真正异步挂起/并发协议候选；两个实际同步夹具没有未完成 await/并发证据，不能据此解释失败或做生产修复。父命令 30.909 秒不等于 TRX 方法 30.017 秒。
- FIX-003：runner/monitor/collector/timeout 已落实同一冻结 deadline 的正预算检查与有限清理；旧 `max(1/max(5))` 已移除。纯虚拟确认 `confirm.json` 覆盖 build/hash 越界 0 次启动、目录准备跨截止仅 monitor 且 test 0 次、test-pid 发布 OSError 清理 test/monitor 两树并保留异常；timeout-control 45 秒外层 watchdog 保留。
- 本轮没有真实 build/Core/solution/GC/并发/压力/dump；FIX-003 不是 GC 修复。`ROOT_CAUSE=NOT_DETERMINED`、`GC_PROBLEM=未解决`。
- 旧“下一步再准备一次采证”已历史化；停止重复采证。

## Review Fix Round 3（FIX-002/FIX-003）

- FIX-002 已统一三处事实：49 条 TRX 不含无 TRX 编译失败；ack-01 仅 deadline 元数据/缺结果，02–05/10 invalid-target，06–09/11 monitor-error，12 人工控制成功；30.909 秒为父命令 elapsed，TRX 方法约 30.017 秒。当前正式候选/来源限制/H6/停止边界不变。
- FIX-003 已落实最后准入、真实 started/PID、try/finally 清理和按实际剩余 wait；见 `execution.md` 的 Review Fix Round 3。
- 纯虚拟确认：`artifacts/review-fix-round-3/virtual-deadline-confirm/confirm.json`；build+hash 越界 0 次启动，目录准备跨截止仅 monitor/test 0 次，test-pid OSError 清理两树并保留异常；`contractSatisfied=true`。无真实产品运行。
- `ROOT_CAUSE=NOT_DETERMINED`，`GC_PROBLEM=未解决`，正式 solution FAIL 保留；不继续自主采证。
