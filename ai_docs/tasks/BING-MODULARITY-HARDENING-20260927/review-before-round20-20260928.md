<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-28T14:31:57+08:00

# Round 19：当前实现与生命周期计划差距

## 结论

以用户最新批准的“模块完整生命周期与 ABP 风格钩子完善计划”T1–T5 为验收基准，并结合原 plan.md、Round 18 execution.md、当前真实源码（含未跟踪文件）、工作区/暂存区 Diff 和直接测试检查。

**没有整组能力尚未实现。剩余 1 个运行行为偏差和 1 组验收/追溯缺口，共 2 个 OPEN_ACTIONABLE Finding。** FIX-016 可以关闭；FIX-018 仍需修复；FIX-017 已补齐阶段矩阵和优先级测试，但资源释放次数验收及部分映射尚未收口。

本轮纠正上一轮的重要判断：“原始取消异常与 Task.IsCanceled 无法兼得”不成立。net8.0、net6.0 独立探针均证明，标准 async/await 桥接可以同时保留原异常实例、令牌、Data 和 Canceled 状态。当前 Shouldly 的 Should.ThrowAsync 会对已取消任务返回新的 TaskCanceledException；这属于断言工具的观测差异，不能据此修改生产取消语义。

- IMPLEMENTATION_STATUS: NEEDS_FIX
- TEST_STATUS: PARTIAL（历史回归通过；当前取消断言固定了错误状态，候选释放次数缺少直接验收）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（既有弱引用回收与通用模块释放测试通过；热候选配置失败/取消的释放次数未直接覆盖）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- RELEASE_STATUS: NOT_ASSESSED
- OPEN_ACTIONABLE: 2
- BLOCKED_APPROVAL: 0
- BLOCKED_EXTERNAL: 0

本轮只更新 review.md、上一轮审查备份和 TestResults 下的独立探针。未修改生产代码、正式单元测试、plan.md、execution.md 或迁移说明。历史审查见 [Round 18 审查备份](review-before-round19-20260928.md)。

## 计划对照

| 项目 | 状态 | 当前实现 | 剩余 |
| --- | --- | --- | --- |
| T1 基类生命周期钩子 | PASS | 服务配置、初始化 Pre/Main/Post 和 Shutdown 的同步/异步虚方法；继承重写及隐藏槽判定；旧接口/base 桥接；新异步重写优先；旧入口迁移提示 | 未发现整组缺失 |
| T2 异步注册 | PASS | 完整图与配置流程共用；集合占用；配置中拒绝重入/提前构建；取消/失败异步释放 | 未发现新的实现差距 |
| T3 全局初始化阶段 | PARTIAL | 全局三轮初始化、共享短期作用域、失败不可重试、逆序关闭、原异常/清理诊断保留 | 模块运行中取消返回 Faulted，尚未同时满足 Canceled 状态 |
| T4 扫描时机 | PASS | Compatible 默认行为；BeforeConfigureServices 提前扫描；防止重复约定 DI；AutoRegisterServices=false 不关闭选项/公开事件；晚注册补扫 | 未发现新的实现差距 |
| T5 热更新与宿主 | PARTIAL（验收） | 候选异步注册与 Post 成功后才切换；失败/取消保留旧代；Web 立即装配；Host 停止接入；六个配置阶段场景与候选 ALC 回收 | 候选配置失败/取消的模块 Dispose/DisposeAsync 恰好一次缺少直接测试 |

模块图、启动期插件、整组热更新、可回收加载上下文及本地插件版本区间是已实现能力，不列为“尚未实现”。反射检测没有进程级 Type/MethodInfo 缓存；无缓存实现不计为计划差距。关闭保持单阶段，计划明确没有 Pre/Post Shutdown。

## 已关闭和已完成部分

- FIX-013、FIX-014、FIX-015：复用已关闭结论，本轮相关路径没有新的反证。
- FIX-016：CLOSED。BingModuleHooks 的旧接口后备分派经过基类桥接，显式/隐式接口调用 base 不会重复。ModuleLifecycleReviewRound18Test 的 LegacyInterfacesCallingBase_RunOnce、ImplicitLegacyInterfacesCallingBase_RunOnce、LegacyInterfaceFailure_DoesNotLeaveBridgeActive 提供直接覆盖。
- FIX-017 已完成部分：CombinedOverrides_AsyncWinsAndSyncPreflightHasNoSideEffects 在同一模块中验证新同步、新异步、旧显式接口并存时的唯一分派；热候选 Pre/Main/Post 配置失败和合作式取消各三例，旧代可用及 ALC 回收已有断言。
- FIX-018 已完成部分：provider/Web 入口可得到原始取消异常、令牌、消息、模块/阶段及清理汇总；尚缺正确任务状态。

## FIX-018：恢复取消状态并修正断言与文档依据

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题：BingModuleManager 将所有异常通过 TrySetException 发布，返回原始 completion.Task。Core/Web 的 UseBingAsync 又使用 TaskCompletionSource 将取消异常设置为 fault。模块启动后的取消因此表现为 IsFaulted=true、IsCanceled=false；上一轮以“不可兼得”解释该偏差，但本轮运行证据否定了该解释。
- 证据：
  - BingModuleManager.cs:160、164、182 返回 faulted completion task。
  - Core BingServiceProviderExtensions.cs:91、104；Web BingApplicationBuilderExtensions.cs:129、142 再次将异常设置为 faulted 结果。
  - ModuleLifecycleReviewRound18Test.cs:79、106、156，以及 WebModuleLifecycleTest.cs:98 断言 IsFaulted。
  - docs/migrations/modularity-runtime.md:135 和 execution.md:838 将该行为写成必要取舍。
  - TestResults/lifecycle-review-round19/Program.cs 在 net8.0/net6.0 直接 await 证明：对 faulted-with-OCE 的任务增加普通异步桥接后，IsCanceled=true，同时异常引用、令牌及阶段 Data 均保留；Should.ThrowAsync 对同一 canceled 任务返回新的异常。
- 影响：取消被消费者按失败计数、OnlyOnFaulted 延续等逻辑处理；计划约定的取消状态未保留，且测试和迁移文档把可修问题固化为限制。
- 修复目标：管理器、provider/Web 入口在取消时同时保留原始异常诊断和 Canceled 状态；非 Web 并发请求继续共享同一发布任务；失败不可重试。
- 修复要求：
  1. 使用标准 async/await 结果桥接等方式发布取消状态；在执行用户钩子前发布共享任务，保留现有重入/并发保护。
  2. Core/Web 入口不要将已取消的初始化重新转成 Faulted。无需反射内部 Task API，也无需新增公开 API。
  3. 取消异常的身份、消息、令牌和 Data 必须用直接 await + try/catch 获取，避免用当前 Should.ThrowAsync 的返回值作原异常证据。
  4. 覆盖 Pre/Main/Post、有/无清理错误、并发共享、Web 及预先取消路径；同时断言 IsCanceled=true、IsFaulted=false 与原异常身份。
  5. 修正迁移文档和执行报告中的“无法兼得”结论，保留历史记录并注明纠正原因。
- 验证方式：上述定向回归先通过，再按影响范围复核 Core/MVC 双框架；不靠把断言改成 IsFaulted 来通过验收。

## FIX-017：补齐热候选资源释放验收并清理映射漂移

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE（验收缺口；不是已证明的生产资源泄漏）
- 问题：六个候选配置失败/取消场景证明旧代可用和 ALC 可回收，但夹具基类没有实现 IDisposable/IAsyncDisposable，无法证明计划要求的候选模块资源“释放一次”。部分生产符号映射还引用已改名的测试。
- 证据：
  - PluginModules.cs:389、433 的 FixtureHotConfigurationFailureModuleBase / FixtureHotConfigurationCancellationModuleBase 只继承 BingModule，没有释放钩子或计数。
  - HotPluginHostTest.cs:121、155 的两个参数化测试只检查旧代信息、服务访问、阶段和上下文 WeakReference。
  - test-mapping.md:258、260 仍引用 ReloadAsync_AsyncConfigurationFailure_ShouldKeepCurrentGenerationAndCollectCandidate / ReloadAsync_AsyncConfigurationCancellation_ShouldKeepCurrentGenerationAndCollectCandidate；真实方法已改为包含 AtAnyPhase 的名称。
- 影响：ALC 回收无法证明外部资源清理方法被调用；通用 Core 释放测试不能完整代替候选宿主链路的直接资源验收。失效测试名称降低交付追溯准确性。
- 修复目标：复用现有六个参数化场景，证明失败/取消候选释放恰好一次并等待异步释放完成，继续满足旧代可用及 ALC 回收；映射指向真实方法。
- 修复要求：为候选夹具增加不保留插件强引用的宿主侧计数或信号；覆盖释放后旧代仍可访问、重复宿主清理不重复释放候选。至少一个异步释放场景通过门控证明 ReloadAsync 等待其完成。保留现有 ALC 弱引用断言。失败夹具当前同步 throw、取消夹具当前 Cancel 后返回 CompletedTask，补测试时可加入真实异步完成形态，但不据此额外推断生产缺陷。
- 验证方式：Pre/Main/Post × 失败/取消六个场景断言释放次数为 1；异步释放未完成时操作不得提前结束；更新并检查当前测试名称映射。

## 本轮验证与证据复用

Change Impact Analysis：生产、正式测试、TFM、依赖及构建图均未改；本轮仅 Review 文档与独立诊断探针。验证范围为生命周期取消任务结果，风险集中在契约判断；不重复无变化的全量构建和资源矩阵。

复用 Round 18 已实际运行的 SDK 8.0.424 证据：Core net8.0/net6.0 各 449 passed；MVC 各 53 passed、1 既有 skipped；WebApi 构建通过。这些数字是上轮证据，不冒充本轮重跑结果。

本轮新增 Probe.csproj / Program.cs，使用锁定 SDK 8.0.424，引用当前 Core 测试输出，分别在 net8.0/net6.0 执行，退出码均为 0。两框架结果一致：

```text
CLR_BRIDGE canceled=True same=True token=True data=probe
SHOULDLY same=False message=A task was canceled. data=<missing>
CURRENT_MANAGER canceled=False faulted=True same=True phase=PreInitialize
MANAGER_BRIDGE canceled=True same=True token=True phase=PreInitialize
```

该探针证明修复方向可行，不代表生产路径已修复；也不等同发布验收通过。没有新工作集实验，不根据一次内存占用判断无泄漏。

## TODO

### 本期剩余：2 个 Finding

- [ ] FIX-018：恢复运行中取消的 Canceled 状态，同时保留原异常；修正受 Shouldly 观测影响的测试及错误文档结论。
- [ ] FIX-017：补齐热候选三个配置阶段失败/取消的资源释放一次与异步等待断言；更新失效测试映射。

### 已批准延期：不计入本期缺陷

- [ ] 自定义生命周期阶段。
- [ ] 应用上下文与配置便捷 API。
- [ ] 模块附加程序集与细化扫描策略。
- [ ] 插件在线分发与多版本求解（当前已有本地单版本区间校验）。
- [ ] 不可信插件进程隔离。
- [ ] 动态 Web 管道替换。

本轮 Review 完成。No-Progress Check: CHANGED（新增双框架探针推翻不可兼得的假设）；Next Action: STOP_REVIEW，由后续 Fix 处理 2 个 OPEN_ACTIONABLE。未自动进入修复。
