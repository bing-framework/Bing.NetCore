<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-28T13:47:34.9673358+08:00

# Round 18：生命周期计划复核

## 结论

以最新批准的“模块完整生命周期与 ABP 风格钩子完善计划”T1–T5 为准，结合原 plan.md、Round 17 execution.md、当前源码（包括未跟踪文件）、Git Diff、直接测试及隔离运行探针复核。

**四组主能力均已实现，没有整组功能空缺；剩余 2 类已复现行为缺陷和 1 组验收测试缺口，共 3 项可执行 TODO。** 不以任意百分比衡量完成度。此前 FIX-013、FIX-014、FIX-015 可以关闭；FIX-016、FIX-017 尚未完全满足验收要求；新增 FIX-018 记录取消异常诊断丢失。

- IMPLEMENTATION_STATUS: NEEDS_FIX
- TEST_STATUS: PARTIAL（历史矩阵通过，但新发现的边界尚无正式回归）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（已有弱引用与释放测试通过；重复旧回调的资源副作用尚待修复）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- OPEN_ACTIONABLE: 3
- BLOCKED: 0

本轮仅更新审查证据及临时探针，未修改生产源码、正式测试、plan.md 或 execution.md。上一版证据保存在 [Round 17 修复前审查](review-before-round18-20260928.md)。

## 计划对照

| 项目 | 状态 | 已实现能力 | 剩余 |
| --- | --- | --- | --- |
| T1 基类生命周期钩子 | PARTIAL | 服务配置、Pre/Main/Post 初始化与关闭的同步/异步虚方法；识别继承 override；忽略 new 隐藏槽；同步预检；显式 base 桥接 | 旧接口直接分派后再调用 base 时重复进入旧回调；同模块多种回调优先级直接测试不足 |
| T2 异步注册 | PASS | 共用模块图；服务集合占用；禁止重入及提前构建；回调/扫描/提交取消检查；异步失败释放 | 本轮未发现新增功能缺口 |
| T3 全局初始化阶段 | PARTIAL | 全模块 Pre→Main→Post；共享短期作用域；混合 Pre 的逆模块序列关闭已修复；失败后不可重试；释放及日志处理后完成 | 异步取消异常的原始原因与诊断未保留 |
| T4 扫描时机 | PASS | Compatible 默认兼容；BeforeConfigureServices 提前扫描；避免重复 DI；关闭自动 DI 不关闭类型事件；晚注册选项补扫 | 本轮未发现新增功能缺口 |
| T5 热更新与宿主 | PARTIAL | 等待候选异步注册及全部初始化阶段后切换；Post 未完成保留旧代；候选失败/取消回收；Web 异步钩子；Host 等待异步关闭 | 按上一轮明确要求，热候选配置各阶段失败/取消的直接矩阵尚不完整 |

钩子重写元数据使用临时反射查询，没有进程级 Type/MethodInfo 缓存；相对于计划的“仅保存在当前注册记录”，这是无缓存实现，不计为未实现能力。没有新增 Pre/Post Shutdown，也不将其列入 TODO，因为计划明确只保留一个关闭阶段。

## 已关闭项

| Finding | 状态 | 依据 |
| --- | --- | --- |
| FIX-013 | CLOSED | `_initializationOrder` 冻结实际模块序列，CleanupAsync 逆序筛选已开始模块；MixedPreHooks_ShutdownInReverseModuleOrder 覆盖同步/异步及 Post 故障 |
| FIX-014 | CLOSED | 按继承层级比较 BingModule 原始虚方法槽；HiddenAsyncConfiguration、HiddenAsyncInitialization、HiddenAsyncPreAndPostHooks、HiddenAsyncShutdown 系列直接测试 |
| FIX-015 | CLOSED | 配置回调返回后、扫描结束及提交前检查取消；两种扫描模式取消及 CanceledRegistration_AwaitsAsyncModuleDisposal 等测试 |

## FIX-016：旧接口直接分派后的 base 桥接重复执行

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE（此前修复部分有效，剩余分支未覆盖）
- 问题：只有进入 BingModule 基类桥接方法时才设置桥接标志。BingModuleHooks 在没有新 override 时直接调用旧接口，未设置该标志；旧接口实现中调用对应 base 后，基类又调用同一个旧接口。
- 证据：BingModuleHooks.cs:97、115、141、166、168 的旧接口直达路径，以及 BingModule.cs:42、70、113、137、147 的桥接标志。隔离模块仅实现旧接口、不含新 override，旧方法中调用对应 base，实际 Pre、Post、异步初始化、异步关闭分别运行两次。
- 影响：重复注册、重复启动后台资源或重复关闭，违反每阶段唯一分派和至多一次调用约定。现有“新 override→base→旧接口→base”测试通过，不能证明“直接旧接口→base”路径正确。
- 修复目标：无论框架从新钩子还是旧接口进入，同一阶段旧回调至多执行一次。
- 修复要求：统一旧接口分派与桥接状态；保留显式 base 续接语义，不以删除旧接口兼容或吞异常规避。桥接状态须在异常后复原，不添加静态模块引用。
- 验证方式：无新 override 的显式/隐式旧接口调用 base，覆盖 Pre/Post 配置、异步初始化、同步/异步关闭；断言完整事件序列、每个旧回调一次，并保留已有新 override 桥接测试。

## FIX-018：异步取消丢失原始异常与模块诊断

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题：InitializeCoreAsync 为原始异常补充模块与阶段并完成清理后，CompleteInitializationAsync 对没有清理错误的 OperationCanceledException 调用无参数 TrySetCanceled，导致调用方收到新的 TaskCanceledException。
- 证据：BingModuleManager.cs:182。探针在 Pre 初始化抛出带自定义消息和已取消令牌的 OperationCanceledException；调用方获得默认消息，原始异常引用不同，CancellationToken 不同，Bing.ModulePhase 和 Bing.ModuleType 均缺失。
- 影响：不能定位取消发生的模块和阶段，丢失模块提供的取消原因及令牌；不符合 T3 的原始异常保留约定。这不是清理未执行的证据，问题在结果发布。
- 修复目标：向异步调用方保留取消原因、令牌和模块诊断，并维持取消状态与失败不可重试约定。
- 修复要求：不能只把 TrySetCanceled 改为带 token 版本而继续丢失消息与 Data；同时处理有/无清理错误的取消路径，保留原始原因和清理汇总。
- 验证方式：Pre/Main/Post 分别抛出携带消息、令牌及自定义 Data 的取消异常，断言调用方可观察原始原因、模块/阶段、清理错误、逆序关闭和失败不可重试；覆盖并发初始化共享结果。

## FIX-017：剩余直接验收矩阵

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE（证据缺口，不额外推断生产缺陷）
- 已完成：Web 新异步三阶段上下文与管道，Host 等待新异步关闭，候选主配置失败、后配置取消、Post 暂停期间不切换，以及候选/旧代上下文弱引用回收。
- 剩余问题：尚未找到同一个模块同时具有新同步 override、新异步 override 和显式旧接口的优先级直接断言；候选异步配置的失败用例只覆盖 ConfigureServicesAsync，取消用例只覆盖 PostConfigureServicesAsync，未完成上一轮要求的各阶段矩阵。
- 证据：ModuleLifecycleReviewFixTest.BaseBridgeModule 的前后配置只有同步 override，初始化/关闭只有异步 override；HotPluginHostTest 新增配置失败/取消各一例，PluginModules.cs 的对应夹具固定在主配置/后配置。Core 层逐阶段配置失败及取消已有测试，可复用其通用清理证据。
- 影响：已存在的全绿结果尚不能证明所有明确要求的兼容分派与候选保护分支。
- 修复目标：补齐上述两个窄范围直接测试，并加入 FIX-016、FIX-018 的回归及更新符号映射。
- 修复要求：优先参数化复用夹具，不为每种组合另建生产机制；不要重复已覆盖的扫描、Web、Host、Post 等待或无关资源测试。
- 验证方式：断言异步入口只走新异步 override，同步入口在任何阶段副作用前拒绝异步需求；候选 Pre/Main/Post 配置分别失败/取消时旧代可服务，候选释放一次并可回收。

## 本轮证据

复用 Round 17 已实际运行的锁定 SDK 8.0.424 基线：Core net8.0/net6.0 各 434 passed；MVC 各 52 passed、1 既有 skipped；WebApi 构建成功。本轮生产与正式测试未变，不重复全量矩阵。

本轮新增 TestResults/lifecycle-review-round18/Probe.csproj 与 Program.cs，引用当前 Core net8.0 测试输出，仅运行两个小型行为验证。进程退出码 0 表示探针运行结束，不表示这些契约通过：

```text
LEGACY_CALLBACKS=old-pre,old-pre,old-post,old-post,old-init,old-init,old-stop,old-stop
CANCEL_SAME_EXCEPTION=False
CANCEL_MESSAGE=A task was canceled.
CANCEL_PHASE=<missing>
CANCEL_MODULE=<missing>
CANCEL_TOKEN=False
```

## TODO

### 本期剩余

- [ ] FIX-016：修复旧接口直达分支中的 base 回调重复执行，并补齐回归。
- [ ] FIX-018：保留异步取消原始原因、令牌和模块阶段诊断，并补齐回归。
- [ ] FIX-017：补齐同模块多回调优先级与热候选配置阶段矩阵，更新映射与实际验证记录。

### 已批准延期方向

这些不是本期未修复缺陷，也不意味着已有热更新或版本区间未实现：

- [ ] 自定义生命周期阶段。
- [ ] 应用上下文及配置便捷 API。
- [ ] 模块附加程序集与细化扫描策略。
- [ ] 插件在线分发与多版本求解（当前已有本地单版本区间校验）。
- [ ] 不可信插件进程隔离（独立任务）。
- [ ] 动态 Web 管道替换（独立任务）。

本轮审查到此结束，不修改业务实现，不因延期方向自动启动修复。
