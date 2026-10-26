# Astra GC 根因续查结果

本轮取得了可重复因果证据：**Microsoft DI 6.0.0 的后台服务编译工作项会在 Dispose 返回后持有根容器；两个同步生命周期路径都会触发它。** 在与正式采证相同的 Core/Test DLL 上，暂停真实编译回调后，两个原测试在 net6/net8 均从原断言失败。不是 H6 的异步关闭尾部，也不需要关闭钩子异步挂起。

这证明一个足以导致当前断言失败的具体机制，尚不能独占归因于两份历史自然失败；它们没有失败根链。生产资源泄漏仍未被证明，整改未应用/验收，历史正式 FAIL 保留。

```text
CONTROLLED_CAUSAL_CHAIN = CONFIRMED_DI_BACKGROUND_COMPILATION_RETENTION
ORIGINAL_ASSERTIONS_UNDER_CONTROL = FAILED_4_OF_4
PRODUCT_PERSISTENT_LEAK = NOT_PROVEN
HISTORICAL_RUN_ROOT_CAUSE = NOT_DETERMINED
GC_PROBLEM = UNRESOLVED
FIX_CANDIDATE = READY_NOT_APPLIED_NOT_VALIDATED
FORMAL_BUILD = PASS_REUSED
FORMAL_SOLUTION_RELEASE = FAIL_REUSED
```

## 1. 本轮身份和证据边界

- 本轮实际起点：2026-10-23 17:20:59.328471 +08:00，在规则/旧报告只读阅读后、首次写入与实验之前捕获。见 [startpoint.json](artifacts/astra-investigation/startpoint.json)；不是补造旧任务起点。
- 由本线程完成源码分析、实验设计、根因判断。Luna 仅只读提取旧 TRX 字段/时间；没有委派 OpenCode 排查。
- 没有重建产品/原测试程序集。现有 net6/net8 的 Bing.Core.dll 和 Bing.Core.Tests.dll **均匹配 gc-capture-20261023-01 保存的 SHA-256**。见 [candidate-comparison.json](artifacts/astra-investigation/candidate-comparison.json)。两次探针批次之间二进制也一致。
- 这证明在正式候选二进制上可触发该机制，不补足旧正式 build 前未封存源文件的缺口。起点保存的 7 份相关源码收口时逐字节未变。
- net6 运行时 6.0.36；net8 运行时 8.0.30；二者加载同一个 DI 6.0.0.0 实现，MVID `bdd6787e-f9d8-4182-85a1-bba141319b97`。
- 本轮是独立 console 调用现有 private 工厂/public 测试方法，**不是新的 xUnit/testhost/TRX 或正式 solution gate**。原 Shouldly 异常与行号完整保存在 stdout。

## 2. 确认的引用路径

实际生产路径：

1. `BingModuleManager.InitializeCoreAsync` 第 219 行通过 `_provider.CreateScope()` 创建初始化作用域。
2. 显式 `ShutdownBing()` 进入 `CleanupAsync` 第 371 行，再通过扩展方法创建关闭作用域。
3. 扩展方法每次解析 `IServiceScopeFactory`。DI 6.0.0 把它注册成包含 Root scope 的 `ConstantCallSite`，缓存位置是 `None`，不会走 factory/type singleton 的 Root 快捷路径。
4. `DynamicServiceProviderEngine.RealizeService` 的解析闭包把 `callCount` 加到 2 后，以 `UnsafeQueueUserWorkItem` 排队编译。后台闭包捕获 engine 和 callSite；容器 Dispose 不等待/取消这个工作项。
5. 后台回调需要把新 accessor 写回同一个 provider；编译完成前存在以下可达路径。

```text
线程池待执行工作项 / 正在执行的编译回调
  -> DynamicServiceProviderEngine.<>c__DisplayClass2_0
       -> <>4__this -> DynamicServiceProviderEngine._serviceProvider
       -> callSite -> ConstantCallSite.Value -> Root scope.RootProvider
  -> ServiceProvider（_disposed = true）
       -> CallSiteFactory._descriptors[] -> 模块 ImplementationInstance
       -> Root._disposables -> BingModuleManager -> _provider
       -> 原有服务 accessor / call site 缓存 -> 模块数组 / recorder
```

Probe 对前两条闭包到 provider 的路径使用 `ReferenceEquals`，不是只比较 hash：`callCount=2`、`callbackCapturesSameClosure=true`、`enginePointsToProvider=true`、`callSitePointsToProvider=true`。只返回标量/字符串的 NoInlining helper 在 GC 前退栈。完整路径见 [retained 原始输出](artifacts/astra-investigation/diagnostic/net6.0-retained/stdout.txt) 和 [repeated 原始输出](artifacts/astra-investigation/diagnostic/net6.0-repeated/stdout.txt)。

DI 编译事件记录的栈确实包含 `ILEmitResolverBuilder.BuildTypeNoCache`、`DynamicServiceProviderEngine...<RealizeService>b__1`、`QueueUserWorkItemCallbackDefaultContext.Execute`、`ThreadPoolWorkQueue.Dispatch`。事件 providerHash 与该进程的对象观测匹配；不能跨进程比较 hash。

保留 Services 的正常关闭场景还直接观察到：`registration._provider == null`、`_owned.Count == 0`、全部 descriptor.Instance 已清空、Services 中 BingModule 描述符数为 0。**外部 ServiceCollection 已解除这几条持有，但 DI 构建时复制的描述符仍在被后台工作项持有的容器内。** 因此不能把“模块仍 alive”等同于 registration 未释放。

实现来源：本地实际 DLL 的字段/调用栈，与微软 [DynamicServiceProviderEngine v6.0.0](https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/DynamicServiceProviderEngine.cs)、[ServiceProvider](https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceProvider.cs)、[ConstantCallSite](https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ConstantCallSite.cs)、[CallSiteFactory](https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/CallSiteFactory.cs) 对照。读取的源文件保存在 [upstream](artifacts/astra-investigation/upstream/)。

## 3. 有界实验及实际结果

运行前冻结工作量与截止，见 [Astra 计划](astra-investigation-plan.md)。一次诊断、一次确认，未第三次执行同假设。

EventListener 只在真实 `IServiceScopeFactory` 的 DynamicMethodBuilt 事件暂停回调，保存字符串/数值与栈文本，不保存 provider/scope/委托/事件参数对象。闸门最多 10 秒，独立进程最多 15 秒；finally 释放。没有调小线程池、关闭测试并行、改原断言或延长原 GC 窗口。这是**人为扩大真实后台工作窗口**的控制实验，不冒充历史自然失败采集。

| 批次/固定工作量 | 结果 | 子进程退出码 |
| --- | --- | --- |
| A1 诊断，net6 retained 工厂 1 次 | 容器已 Dispose、registration 已解绑；provider/module 均 alive；1 个 scope 编译回调 | 0（符合诊断预期） |
| A1 诊断，net6 repeated 工厂 1 个应用 | provider/manager/recorder/4 modules 全部 alive；1 个 scope 编译回调 | 0（符合诊断预期） |
| A1 诊断，纯 DI 一次 CreateScope | callCount=1，无编译回调，provider 已回收 | 0 |
| A1 诊断，纯 DI 两次 CreateScope | callCount=2，1 个编译回调，provider alive | 0（符合诊断预期） |
| A1 确认，原 retained 测试，net6/net8 各一次 | 两次均原 `snapshot.Provider.TryGetTarget(out _)` True，ShouldAssertException，源码第 223 行 | 各 1，确实 Failed |
| A1 确认，原 repeated 测试，net6/net8 各一次 | 两次均原 `alive.ShouldBeEmpty()` 失败，各 84 项；12 个 scope 编译回调，源码第 409 行 | 各 1，确实 Failed |
| A1 确认，纯 DI 缓存 factory，仍创建两个 scope，net6/net8 各一次 | callCount=1，无编译回调，provider 已回收 | 各 0 |

所有进程均未超时，所有闸门均未超时。原 repeated 方法自身已有的第二诊断 GC 窗口照常执行，首快照仍失败；本轮未增加 GC。其他诊断每例仅原标准 3 轮。没有在释放 gate 后再追加 GC 争取变绿，所以本轮没有把“同一被暂停实例释放 gate 后已回收”写成实测事实。

命令（工作目录仓库根）：

```powershell
python ai_docs/tasks/20261023-module-lifecycle-gc/artifacts/astra-investigation/run_probe.py build
python ai_docs/tasks/20261023-module-lifecycle-gc/artifacts/astra-investigation/run_probe.py diagnostic
python ai_docs/tasks/20261023-module-lifecycle-gc/artifacts/astra-investigation/run_probe.py confirmation
```

compile 使用现有 SDK 10.0.400 的 csc 直接编译 Probe.cs，45 秒上限，约 0.900 秒退出 0；仅有响应文件 `/noconfig` 和 xunit 引用标识两条警告，未新增包/项目依赖。诊断 4 个进程合计约 0.781 秒；确认 6 个进程合计约 1.641 秒。每批预设总截止 120 秒，没有递增。

每个子目录内 `command.json` 保存实际 argv/cwd/开始/冻结 deadline，`stdout.txt`/`stderr.txt` 保存原始输出，`result.json` 保存真实退出码/PID/结束/耗时。汇总见 [results-summary.json](artifacts/astra-investigation/results-summary.json)，批次输入与候选绑定见 [diagnostic/batch.json](artifacts/astra-investigation/diagnostic/batch.json)、[confirmation/batch.json](artifacts/astra-investigation/confirmation/batch.json)。Python 批次脚本退出 0 只表示固定子进程序列执行完毕；不能覆盖其中 4 个原测试实际退出 1。

## 4. 其他引用解释与未覆盖范围

- **测试/JIT/观察**：两原工厂已有 NoInlining，provider/module 强局部先退栈；本轮反射观测也在 NoInlining helper 内，GC 后仅取 bool。原失败快照仍只读值、不读 Target。已证明一个与测试局部是否优化无关的外部强根，故无需把失败解释成 JIT 假象；没有宣称所有历史 JIT/弱引用问题被排除。
- **AsyncLocal/ExecutionContext**：manager 的 `_insideHook` 存 bool，未注册捕获 manager 的通知回调；两个同步路径的 async 方法不实际挂起，调用返回时上下文恢复。控制实验在前台设的 AsyncLocal 标记，在所有 scope 编译回调中均未流入（ambientFlowed=false），吻合 UnsafeQueueUserWorkItem。因果链不依赖 ExecutionContext 捕获应用。并未据此排除所有别处的上下文泄漏。
- **静态与并行**：BingLoader 的 CWT 只保存 Completed；非 legacy 注册不绑定 ServiceLocator。控制 original repeated 输出 ServiceLocator enabled=false。两个夹具属于已有 DisableParallelization 的 Module static compatibility 集合，但**这不阻止 Microsoft DI 自己的后台编译**。旧 6/12 进程并发也没有直接观察这个根。
- **DI 内部循环**：manager 指回 provider、本体缓存与描述符相互引用本身不是静态泄漏；只有线程池工作项等外部根存在时才使整个图活着。纯 DI 一次解析/缓存 factory 的对照包含同样容器内部循环，仍能回收。
- **H6**：仍不具备实际异步挂起/并发的前提。同步 ShutdownCore 要在 CompleteShutdownAsync 同步调用返回后才把 completion.Task 返回调用方；本轮不修改它的发布协议。
- **生命周期含义**：Dispose 已释放模块资源，不等于默认 DI 的托管对象图立即没有后台使用者。当前更符合运行时后台工作的存活边界与测试即时回收假设冲突；尚无永久产品泄漏证据。不能因本机制成立而说整个框架无其他泄漏。

## 5. 具体最小候选整改（交主线程实施/验收）

提供可审阅、未应用的 [candidate-fix.NOT-APPLIED.patch](artifacts/astra-investigation/candidate-fix.NOT-APPLIED.patch)。它只包含以下两处，原失败条件、两阶段独立 scope、默认关闭 hook、GC 次数和断言不变：

1. **BingModuleManager**：按应用惰性缓存 `IServiceScopeFactory`，两个生命周期阶段调用同一 factory 的 CreateScope。仍分别创建/释放初始化和关闭 scope；容器 Dispose 回调路径继续跳过创建 scope，legacy 仍按原分支处理。减少框架自己对同一个常量工厂的重复解析，消除已证明的 scopeFactory 第二次解析触发点。惰性取得避免在 manager 构造期间提前引入原本不存在的解析/异常。
2. **ModuleLifecycleTest 的 Create helper**：Recorder 仍为同一个测试单例，注册从 ImplementationInstance 改成返回预先创建实例的 singleton factory。理由是 ChildModule 和 ParentModule 各解析一次 Recorder；instance 常量同样会触发 DI 第二次解析后台编译。只缓存 scope factory 会漏掉这个独立触发点。此 Recorder 路径由源码推导，当前 gate 只拦 scopeFactory，尚未单独测量/证明它在历史失败中仍活跃，不能写成已验收修复。

这是一份**针对当前两个受控输入的触发点消除候选**，不是“修复默认 DI 永久泄漏”，也不承诺任意应用注册/其他线程重新解析 scope factory 后都能立即回收。若项目希望坚持默认 DI 下任意应用 Dispose 后瞬时可收集，需要另行明确该契约；不能靠更多 GC/sleep 实现。本轮没有做这类契约扩张。

主线程的最小验收顺序：

- 在隔离候选应用上述两处，精准编译 Core.Tests 两 TFM；保留本轮正式二进制/失败证据。
- 新增直接回归记录生命周期共创建两个独立 scope、factory 仅解析一次；覆盖正常初始化/显式关闭、初始化失败清理、取消后的清理和 provider Dispose 分支。保持原异常与释放次数。
- 对原两个弱引用测试做一次控制确认：监听并暂停 scopeFactory 和 Recorder 的编译回调；候选应不触发这两个编译项，原断言通过。输出事件计数与字段/工厂解析计数，不能只报绿。若出现其它存活根，保留失败并定点检查，不加 GC。
- 验证 `Create<T>` helper 的现有正常/失败/异步生命周期消费者，及非默认容器中 scope factory 的实际契约；交 Sol/high 独立验收。生产字段缓存对自定义提供程序的语义需核对，不能拿纯 DI 对照替代。
- 根因整改验证后才由主线程安排必要的最终全量；本轮不运行。

**当前方案中已验证的是纯 DI 的“解析一次、仍创建两 scope”反事实；完整候选 patch 尚未编译或跑过，不能标为已修复。** 用户要求先交根链/回归/方案，再由主线程实施生产修复，本轮按此交付，不重新请求已有授权。

## 6. 源码 → 失败回归 → 证据

| 符号/行为 | 可执行回归/观测 | 证据 |
| --- | --- | --- |
| Manager.InitializeCoreAsync + CleanupAsync 的两个扩展 CreateScope | Probe retained/repeated 调用原 NoInlining 工厂 | diagnostic/net6.0-retained、net6.0-repeated |
| DI DynamicServiceProviderEngine 第二次解析闭包 | Probe.InspectOwnership + CompilationGate | callCount=2、三条引用等同性、真实编译栈 |
| Registration.ReleaseAsync 的解绑/清空 | Probe retained、外部 Services 保留 | registrationDetached/ownedCount/descriptorInstancesCleared/retainedModuleDescriptors |
| 原 retained 测试第223行 | Probe.InvokeOriginal(original-retained)，两 TFM | confirmation/net6.0-original-retained、net8.0-original-retained，exit1 |
| 原 repeated 测试第409行 | Probe.InvokeOriginal(original-repeated)，两 TFM | confirmation/net6.0-original-repeated、net8.0-original-repeated，84项/exit1 |
| 默认容器本身就存在的机制 | Probe.CreateBare(bare-one/bare-two) | 同一GC预算下无回调可回收、有回调仍alive |
| 候选 scope 工厂复用 | Probe.CreateBare(bare-cached)，两 TFM | 两个scope、callCount=1、零回调、provider=false |
| 候选 Recorder singleton factory | 源码两个 GetRequiredService<Recorder> + DI 常量调用规则 | 尚未单独运行；候选实施时精准覆盖 |

所有相对实验目录均位于 `artifacts/astra-investigation/`。

## 7. 旧失败分离与本轮收口

Luna 核对原 TRX 后返回：

- 正式 gc-capture-20261023-01：net6 retained，duration 0.0578034 秒，原 provider TryGetTarget 为 True，源码第223行。该结果 start/end 只差0.4微秒，与 duration 不一致，不能从这两字段计算精确执行窗口。同一 Core TRX 原 repeated 通过，记录时刻约相隔8秒。
- Stage B 最终 L4-v2：net8 repeated，duration 0.0534215秒；原 `references.All(r => !r.IsAlive)` 为 False，历史源码第375行。同一 Core TRX retained 通过，约相隔11.6秒。它是旧36弱引用候选，不与本轮84引用候选混同。
- 两个正式批次都为 12881/1/1114，却不是同一个失败；后续精准绿与本次控制失败都不改写旧记录。

本轮状态分组：

| 分组 | 内容 |
| --- | --- |
| Completed | 同步路径引用分析；真实闭包等同性/编译栈；纯 DI 反事实；两个原测试两TFM控制失败；可审阅候选差异和验收方案。 |
| Open Actionable | 主线程实施/验收候选触发点消除；本线程调查交付范围已经完成，不自行应用生产补丁。 |
| Blocked Approval | 无；没有重复请求授权。 |
| Blocked External | 历史自然失败的排他归因缺真实失败根链；不是本地生产缺陷 finding。 |
| Not Applicable | 本轮 L2-L5、全量/压力/Benchmark、OpenCode FIX-003 工具工作。 |
| Accepted Limitations | 控制调度放大真实窗口；未实测暂停实例释放后的再次GC；旧source-at-build缺口保持。 |
| Verified Boundaries | 当前默认 DI6 实现中同步 Dispose 可以早于后台编译完成；三轮GC不能回收仍有强根的对象。 |
| Deferred | 候选生产/测试修改后的 Sol/high 验收和最终 solution gate，由主线程统筹。 |

Implementation（本轮调查）5/5；Release gate 本轮新增通过0，原正式 FAIL 保留。Changed Scope 只为 Astra 文档/独立探针/artifact/未应用补丁；L0 独立编译退出0，L1 控制因果观测已完成，L2-L5未运行。No-Progress=CHANGED（取得新根链与可重复失败）。A1一次诊断+一次确认已用尽，停止该假设继续试跑。Next Action：交回主线程实施候选并独立验收；不把已证机制升级为整个GC问题已解决。

交付静态校验：候选补丁转为 UTF-8/LF 后，`git apply --check` 退出0（只检查，未应用）；报告链接、UTF-8/控制字符、10个固定case/4个原断言失败和无超时核对通过。见 [delivery-check.json](artifacts/astra-investigation/delivery-check.json)。
