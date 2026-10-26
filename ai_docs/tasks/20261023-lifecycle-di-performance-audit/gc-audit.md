# Bing.Core 生命周期、GC 与 DI 所有权专项审查

日期：2026-10-23。职责：Astra 主审；luna_worker 仅机械核对候选身份和直接测试位置。范围是模块/应用生命周期、注册解绑、静态与异步引用及启动/关闭复杂度；Uow、Dapper、EF、CAP、业务后台链由主线程审查。

**结论：当前 scope 工厂缓存修复有效，未发现新的默认正常生命周期永久 GC 强根。发现 1 项可复现的 P2 错误路径缺陷：同一服务集合重复构建并触发校验失败，会提前释放原应用模块，并破坏原应用的同步关闭。触发方式属于文档禁止的重复建容器；问题是拒绝该误用时发生破坏性副作用，不是正常生命周期泄漏。**

本轮只审查，未实施生产修复，未修改原测试。启动/关闭存在源码可见的二次复杂度，尚无预算超限证据，作为建议单列。没有重跑旧 GC 探针、压力或完整 solution。

## 当前候选与证据复用

- 以 [retention-fix/final-report.md](../20261023-module-lifecycle-retention-fix/final-report.md)、[主线程最终核验](../20261023-module-lifecycle-retention-fix/artifacts/main-thread-final-verification.json) 和 [最终候选目录](../20261023-module-lifecycle-retention-fix/artifacts/final-candidate-after-build-recovery/) 为当前状态。Sol Review2 PASS；Admin 构建恢复专项 PASS。
- 当前正式 Release build exit0；默认全量 12,894 Passed / 0 Failed / 1,114 NotExecuted。103 个 TRX 中 6 个空文件不计为通过。两项原 GC 测试及六项生命周期回归在 net6/net8 均通过。
- 冻结 HEAD 与当前 HEAD 均为 `3919c1509907d661cfef3679dad79368c8b9cb39`。luna_worker 核对 `freeze.json`、`source-identity-after.json` 与当前 `BingModuleManager.cs`、`ModuleLifecycleTest.cs`、`ModuleLifecycleRetentionTest.cs` 的字节数及 SHA-256 一致。
- 新诊断实际加载的两个 TFM 的 `Bing.Core.dll` 均匹配最终候选 `post-build-identity.json`：`3A145508A3670837D2E3FBB2C77CD520C4FE64CDDA93BCCCA98CAC31A6104C32`。运行时分别为 6.0.36 / 8.0.30，DI 程序集版本 6.0.0.0；完整路径/MVID/身份见各批次与原始 stdout。
- 另只读核对 `BingModuleRegistration`、构建扩展、Hooks、Loader、依赖图、ServiceLocator、OptionsTypeRegistration、BingLoader：`git diff --quiet HEAD -- <上述文件>` exit0。它们也未出现在最终冻结时的已修改源码列表中。
- 旧 GC 任务的 UNRESOLVED 不作为当前状态；旧人工 ack dump、旧失败全量也不作为新候选的失败证据。历史自然失败的排他性归因没有因此补证，但这不重新打开已修复机制。

## GC-AUDIT-001：二次构建校验失败错误地清理已运行应用

**优先级：P2；状态：OPEN_ACTIONABLE；证据：源码 + 独立真实 Microsoft DI 复现。**

位置：

- [BingServiceCollectionExtensions.cs:37](../../../framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingServiceCollectionExtensions.cs#L37)：底层 BuildServiceProvider 抛异常后，无条件对集合中的 registration 调用 FailConfiguration。
- [BingModuleRegistration.cs:203](../../../framework/src/Bing.Core/Bing/Core/Modularity/BingModuleRegistration.cs#L203)：EnsureBuildable 仅检查配置中/配置失败，没有拒绝 `_provider != null` 或 `_runtimeReleased`。拒绝多个 provider 的检查在 Attach（:228），晚于底层容器校验。
- 同文件 :390、:311、:355：FailConfiguration 进入 ReleaseAsync，释放模块、清空 descriptor.Instance、解绑 provider、清空所有权集合。
- [BingModuleManager.cs:311](../../../framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs#L311)、:336：原 manager 状态仍是 Initialized；同步关闭检查已被清空的模块实例，进入 `BingModuleHooks.Overrides`（[BingModuleHooks.cs:22](../../../framework/src/Bing.Core/Bing/Core/Modularity/BingModuleHooks.cs#L22)）时空引用。

**触发条件**：对同一已 AddBingApplication 的服务集合，第一次 BuildBingServiceProvider 不开校验并正常 UseBing；第二次以 ValidateOnBuild=true 构建，且存在未使用、缺依赖的注册。本复现没有在两次构建之间修改服务集合。文档 [modularity-di-resources.md:11](../../../docs/guides/modularity-di-resources.md#L11) 明确禁止“为预检另建第二个容器”，Attach 也已有拒绝语义。

**已观测**：

| 场景 | 失败/拒绝返回时原模块 Dispose 次数 | 随后关闭 | 证据范围 |
|---|---:|---|---|
| 首次构建校验失败 | 1（未初始化的新模块） | 不适用 | net6/net8 正常对照 |
| 同集合二次构建，不开校验 | 0 | 同步关闭一次，再释放一次 | net6/net8 正常对照；Attach 拒绝 |
| 同集合二次构建，校验失败 | **1，尚未调用 Shutdown** | **同步关闭 NullReferenceException**；net8 异步收尾发生在模块已 Dispose 之后 | net6 捕获同步释放错误栈；net8 完整计数/顺序 |
| 另一独立集合校验失败 | 0 | 原应用正常关闭/释放一次 | net8；net6 因前述失败未执行此案例 |

net8 案例3的计数为：失败前 `(Initialize=1, Shutdown=0, Dispose=0)` → 校验失败后 `(1,0,1)` → 同步关闭异常仍为 `(1,0,1)` → 为探针自身收尾调用异步关闭后 `(1,1,1, ShutdownAfterDisposed=1)`。未发生重复 Dispose，但关闭顺序已被破坏。net6 的 using 收尾再次抛同类空引用，覆盖了原同步关闭异常；原始堆栈保留，未把该进程标成通过。

**影响**：一次被禁止的二次构建操作会关闭第一个应用仍在使用的模块资源，并使正常同步关闭/Dispose 失败；业务继续调用这些资源可能遇到已释放对象。本证据没有声称永久托管泄漏，也没有实测业务文件句柄、连接或事务损坏。

**最小建议**：在 BuildBingServiceProvider 进入底层构建前，于现有 registration 状态检查中拒绝已附加或已释放的运行时，保持首次构建失败清理。不要通过忽略 null 模块来掩盖原应用已被提前释放的问题。该修改不需要允许共享根容器、不需要新依赖，也不应顺带承诺 IServiceCollection 并发构建安全。具体实施交回主线程。

**建议回归/验收**：在 `BingServiceCollectionExtensionsTest` 补“已运行集合二次 BuildBingServiceProvider(validate=true) 被拒绝，原模块 Dispose=0；原应用随后正常 Shutdown/Dispose，各一次”的直接测试。继续保留首次校验失败释放同步/纯异步模块、独立集合隔离、普通重复构建拒绝的用例。修复后精准运行这些测试的 net6/net8；本审查不自动执行修复或全量。

## 当前 GC 修复边界与引用所有权

| 路径 | 源码判断 | 已有覆盖及边界 |
|---|---|---|
| 初始化/显式关闭创建 scope | `BingModuleManager.cs:300` 缓存应用自己的 IServiceScopeFactory；两阶段仍分别 CreateScope。正常串行生命周期只解析 factory 一次，阻断原先第二次常量 callsite 解析排队编译持有 provider 的触发方式 | `ModuleLifecycleRetentionTest.cs:18,51,71,96,126` 覆盖真实 manager 的工厂计数、不同 scope、正常/异常/取消释放和 provider Dispose 不再创建 scope；原两项 GC 回归当前候选通过 |
| registration/服务描述符 | ReleaseAsync 最终清 `_provider`、descriptor.Instance、插件清单、owned/released；清理旧 builder 的模块引用；可写集合移除直接模块实例描述符。manager 清 `_started`/`_initializationOrder`（:406） | `ModuleResourceRegressionTest` 覆盖保留集合仍可回收；`ModuleDescriptorCleanupFailureTest.cs:18` 覆盖 RemoveAt 抛异常时继续清理。GC-AUDIT-001 是绕开正常 manager 状态的另一错误入口 |
| 静态兼容入口 | ServiceLocator.BindServices（:72）在换集合时丢弃旧 provider；Unbind（:107）按集合/provider 身份解绑；Registration.UnbindLegacy（:282）按配置实例清 Singleton<IConfiguration> | 新 AddBingApplication 不使用静态入口；旧 AddBing 仍是单宿主兼容语义。多集合隔离/解绑测试存在，不承诺旧静态入口同时服务多个宿主 |
| 注册静态表/扫描闭包 | `BingLoader.cs:15` 与 `OptionsTypeRegistration.cs:15` 使用 ConditionalWeakTable 按集合存储；`DependencyTypeFinder.cs:49` 在完成注册时清约定筛选委托。没有找到新的普通 static 字典把 registration/provider 作为强键或值长期保存的默认调用链 | 调用方继续保留 services/configuration/异常/manager/provider 是正常所有权，不等于全局根泄漏；公开 RegisterType 事件订阅者仍须自行退订 |
| AsyncLocal/任务/取消 | manager 的 AsyncLocal 只存 bool，无回调引用 manager；linked CTS 在 CompleteInitializationAsync 的 using 中释放。活动 hook/task、scope 与 root 互相持有期间存在所有权；关闭后异步 finally 的短暂尾部不是永久根链 | 预取消、进入 hook 后取消、共享初始化、并发 Shutdown/Dispose、清理异常均有直接测试。没有把旧 H6 重报成生产泄漏；无新 GC 探针 |
| 插件加载解析事件 | `BingPluginLoader.cs:79,141` 的 Resolving 订阅在 finally 退订；registration 清插件容器 | 默认 ALC 中已加载程序集保留至进程结束属于既有约束；本轮没有重审插件热更新运行时/业务活动调用链 |

缓存 scopeFactory 会随仍被外部持有的 manager 保留 root，和原 readonly `_provider` 属于同一应用所有权；这不是新全局根。修复只消除当前框架生命周期的特定 DI 编译触发，不保证第三方服务的任意解析、闭包、静态订阅或未结束任务都没有引用持有。测试 Recorder 改为 factory 注册后，原先独立的常量实例第二次解析触发也被移除；原 GC 断言没有放宽。

## 正常、取消及异常释放的审查结果

- 初始化 scope 在成功返回前释放；失败/取消先关闭已开始模块，再释放 registration 拥有的全部模块；单模块关闭/Dispose 异常收集后继续其余清理。同步入口对纯异步 hook/释放会 fail fast；异步入口 await 释放。`ModuleDisposalFailureTest.cs:16,35,54`、`ModuleLifecycleReviewRound18Test.cs:103,131,152` 和当前六项回归直接覆盖主要路径。
- Shutdown 并发共享同一 `_shutdown` 任务；初始化中 Shutdown 拒绝；root 异步释放会请求取消并等初始化退出。`ModuleConcurrentDisposalTest.cs:16,44` 覆盖异步关闭与重复/同步释放。不会把不响应取消的用户 hook 判成已释放。
- **未验证边界**：没有直接测试 cancellation callback 本身抛异常、或只读 IServiceCollection 的新回收断言。本轮没有据此制造 finding。特别是不能仅看到 `DisposeCoreAsync:469` 提前抛 cancellationError，就推断 Microsoft DI 下必然漏清理：初始化结束仍有取消检查/根日志解析；根容器已 Dispose 时解析失败会进入已有初始化异常清理。非标准 provider 的不同语义未实测。
- **既有约束**：原生 BuildServiceProvider 且从未激活 manager 的应用无法靠 DI 自动发现外部构造模块；使用 BuildBingServiceProvider、Host 或明确生命周期入口。readonly services 无法删除实例描述符，但仍执行模块 Dispose；继续持有该集合时对象本身可继续存活。AddSingleton(instance)、工厂转发多个 disposable 别名、调用方保存初始化 Scoped 服务的所有权边界按既有 ADR/指南处理。

## 复杂度与静态缓存观察（不是已确认性能回归）

设 M 为模块数、E 为依赖边数。当前场景主要位于启动/关闭阶段，没有请求热路径变更，也没有批准的模块规模/时间预算。

| 源码位置 | 可由源码确定的增长 | 建议与验证条件 |
|---|---|---|
| `BingModuleLoader.cs:27` | 每次仅取一个就绪模块，却重新扫描 remaining；链式依赖会累计 M(M+1)/2 个候选检查，一般图还反复检查依赖。不能因 OrderBy().First 直接声称每轮完整排序 | 大规模模块场景有需求时考虑入度/反向邻接表加稳定就绪队列，保留 Level/Order/FullName 顺序；先定义规模，再以固定图验证顺序与候选检查/分配 |
| `ModuleDependencyGraph.cs:105` | 每个 DFS 节点构造完整 activePath 数组；深链累计 O(M²) 引用复制。显式栈避免递归栈溢出，但不消除该分配 | 仅在错误或需要报错的分支构建路径；用固定深链验证错误路径原语义及分配，不引入全局 Type 缓存 |
| `BingModuleManager.cs:242,393`；`BingModuleRegistration.cs:240` | `_started.Contains` 与 `_owned.Contains` 的引用查重在大集合形成 O(M²)；关闭过滤也逐项线性查找 | 若有规模收益，保留有序列表并加引用比较 HashSet；对应取消/失败逆序及恰好一次释放回归 |
| `BingModuleRegistration.cs:374` | 按倒序扫描服务集合，但 List 型集合中 RemoveAt 仍可能搬移后方非模块描述符；大服务图释放可能出现重复搬移 | 当前没有耗时超限证据；若改写，须保留自定义/只读 IServiceCollection 语义及异常后继续清理，不直接替换为不兼容的集合 API |

相邻公共工具 `DirectoryAssemblyFinder.cs:13,43` 有按目录路径的进程静态缓存，`fromCache=false` 也写入；不同目录键没有淘汰。源码证明该 API 在持续输入不同路径时可增长，但检索 `framework/src`、`framework/tests` 未见当前模块默认路径调用它，因此不报为本次运行路径永久泄漏。若未来接入动态目录扫描，应先明确缓存生命周期并补不同路径的有界测试。

以上优化建议归为 DEFERRED（建议后续 `module-graph-scalability` 专项），原因是本轮没有实测容量违约、没有产品性能改动或正式阈值，不能据此否定当前最终门禁。未运行 benchmark，也没有自行批准预算。

## 本轮执行与原始证据

运行前 CIA、固定输入、截止及中断补全记录见 [observation-plan.md](artifacts/gc-audit/observation-plan.md)。源代码见 [Probe.cs](artifacts/gc-audit/Probe.cs)，首版源码/DLL 在 `compile/` 保留；执行器 [run_probe.py](artifacts/gc-audit/run_probe.py) 只启动本目录独立程序。

| 执行 | UTC 结束时间 | exit / 超时 | 解释与原始证据 |
|---|---|---|---|
| SDK 10.0.400 的 csc 编译首版 | 13:21:29.591514 | 0 / 否；0.737s | [compile](artifacts/gc-audit/compile/)；仅编译独立探针，未构建产品 |
| net6 原诊断 | 13:21:45.857819 | **1** / 否；0.539s | [stdout](artifacts/gc-audit/diagnostic/net6.0/stdout.txt)、[result](artifacts/gc-audit/diagnostic/net6.0/result.json)；前两案例完成，第三案例产品同步关闭空引用中断，第四未运行 |
| 补全异常记录后编译 | 13:23:49.092827 | 0 / 否；0.610s | [compile-observation-completion](artifacts/gc-audit/compile-observation-completion/)；只改探针记录/自身收尾 |
| 原计划尚未运行的 net8 四案例 | 13:23:57.672294 | 0 / 否；0.581s | [stdout](artifacts/gc-audit/diagnostic-net8-completion/net8.0/stdout.txt)、[result](artifacts/gc-audit/diagnostic-net8-completion/net8.0/result.json)；exit0 表示缺陷与对照的预定观测已确认，**不是产品通过** |

每个目录保存 exact argv/cwd/UTC deadline 的 `command.json`、原始 stdout/stderr 和 result.json；批次记录 DLL SHA/MVID。外层命令是 `python <本目录>/run_probe.py build` / `diagnose`。首次 diagnose 外层同样 exit1；补全外层 exit0。首个 net6 失败未删除、未覆盖、未重跑。编译仅有响应文件内 `/noconfig` 被忽略的 CS2023 警告，不影响 `/nostdlib+` 与显式引用。未新增依赖/工具，未改变 SDK/TFM/API/预算。

## 交回状态

- Completed：源码专项审查、当前 GC 修复边界核对、最终候选证据复用、1 项新边界缺陷的确定性复现与修法/验收建议。
- Open Actionable：GC-AUDIT-001（P2，修复由主线程另行组织；本轮不 Fix）。
- Blocked Approval：无本轮必须取得的批准；未建立新的性能阈值。
- Blocked External：外部运行/部署环境未在本轮执行，不用本地重复测试代替。
- Not Applicable：完整 solution 重跑、旧 GC/压力探针、业务邻接模块重复审查。
- Accepted Limitations：旧静态入口单宿主、默认 ALC、外部 instance/事件订阅者所有权、未终止用户任务、只读服务集合保留描述符。
- Verified Boundaries：误用二次构建的释放副作用已复现；不是容量/GC 永久泄漏证明。
- Deferred：模块规模复杂度优化；取消回调抛异常与非标准 provider 的专项契约测试按后续实际需要选择。

Existing final gates：PASS（复用）。Audit implementation finding：NEEDS_FIX / 1 OPEN_ACTIONABLE。New permanent GC leak：NOT_PROVEN。No-progress check：CHANGED（新增错误路径因果证据）。Next action：STOP，本报告交回主线程；没有自动修复或追加实验。
