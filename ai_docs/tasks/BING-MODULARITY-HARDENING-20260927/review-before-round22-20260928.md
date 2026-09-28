<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-28T07:22:19Z

# Round 20：生命周期计划执行差距复核

## 结论

对照用户最新批准的“模块完整生命周期与 ABP 风格钩子完善计划”T1–T5、原 plan.md、Round 19 execution.md、当前源码（含未跟踪文件）、工作区/暂存区 Diff、迁移说明和直接测试。

**四组核心能力均已实现，没有整组功能缺失；本轮未确认新的生产行为缺陷。仍有 1 项可执行的测试验收缺口：热候选的释放计数夹具不能识别第二次调用。** FIX-018 关闭；FIX-017 保留为 OPEN_ACTIONABLE，范围缩小到计数有效性。

- IMPLEMENTATION_STATUS: PASS（本轮审查范围）
- TEST_STATUS: NEEDS_FIX（释放次数断言存在盲点，既有测试运行通过）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（异步等待和 ALC 回收通过，恰好一次的直接证据不足）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- RELEASE_STATUS: NOT_ASSESSED
- OPEN_ACTIONABLE: 1
- BLOCKED_APPROVAL / BLOCKED_EXTERNAL: 0

上一轮审查保存在 [Round 19 备份](review-before-round20-20260928.md)。本轮仅写入审查报告、备份和 TestResults 下的独立探针，未修改生产代码、正式测试、plan.md 或 execution.md。

## 计划验收矩阵

| 计划项 | 状态 | 当前实际行为 | 未闭环项 |
| --- | --- | --- | --- |
| T1 基类钩子与兼容分派 | PASS | 配置 Pre/Main/Post、初始化 Pre/Main/Post、单阶段 Shutdown 的同步/异步虚方法；继承重写检测、隐藏方法排除、旧接口及 base 桥接 | 无新增缺口 |
| T2 异步应用注册 | PASS | AddBingApplicationAsync 接入完整模块图；集合占用、异步配置、失败异步清理；旧入口保留立即 AddServices 并拒绝不支持的新配置重写 | 无新增缺口 |
| T3 全局初始化阶段 | PASS | 同一序列分三轮、共享短期作用域；Post/作用域释放/日志完成后提交成功；失败逆序清理；取消保留原异常及 Canceled 状态；并发共享和 Web 拒绝重入 | FIX-018 已关闭 |
| T4 扫描时机 | PASS | Compatible 与 BeforeConfigureServices；提前约定 DI 扫描及类型注册、防重复扫描；AutoRegisterServices=false 不关闭选项/公开事件 | 无新增缺口 |
| T5 热更新与宿主 | PARTIAL（测试验收） | 候选异步注册并完成 Post 后才切换；失败/取消保留旧代；Web 即时装配与 Host 停止；六个配置阶段故障场景及候选回收测试 | FIX-017：释放次数计数器屏蔽重复调用 |

既有模块图、启动期插件、整组热更新/卸载、可回收加载上下文、本地版本区间校验不列为“未实现”。自定义阶段等已批准延期项也不算本期功能缺陷。

## FIX-018：CLOSED

BingModuleManager.cs:146–188 在钩子执行前发布共享异步桥接任务；实际钩子仍在锁外立即进入。标准 async/await 将原始 OperationCanceledException 发布为 Canceled，保留异常实例及诊断。provider/Web UseBingAsync 直接等待管理器，不再重新包装为 Faulted。

ModuleLifecycleReviewRound18Test.cs:62、91、119、136 使用直接 await 捕获异常，覆盖 Pre/Main/Post、清理失败、预取消和并发共享；断言 IsCanceled=true、IsFaulted=false、异常引用、令牌及阶段 Data。WebModuleLifecycleTest.cs:81 提供 Web 入口直接覆盖。迁移文档已纠正此前“不可兼得”的错误结论。

## FIX-017：候选释放计数无法识别重复调用

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题：夹具首次 DisposeAsync 时清空 _recordDispose。第二次进入同一个 DisposeAsync 不再通知宿主计数。因此 disposalCount==1 只能证明回调曾执行，不能证明框架只调用一次释放。
- 证据：
  - framework/tests/Bing.Core.Tests/PluginFixtures/Bing.Core.PluginFixtures/PluginModules.cs:408–415：复制回调后将字段置 null，再调用计数回调。
  - framework/tests/Bing.Core.Tests/Modularity/HotPluginHostTest.cs:170–172、220–222：六个参数化场景依赖该计数器断言一次及宿主关闭后不重复。
  - TestResults/lifecycle-review-round20/Program.cs 在 net8.0/net6.0 主动调用夹具 DisposeAsync 两次，均输出 DISPOSE_INVOCATIONS=2 OBSERVED_CALLBACKS=1。
- 影响：即使生产释放逻辑将来重复调用候选模块，此断言也可能继续通过。当前生产 ReleaseAsync 存在引用身份去重，尚无证据证明实际发生重复释放或泄漏。
- 已完成部分：六个配置阶段故障/取消用例验证旧代可用、候选 ALC 回收；Pre 配置失败门控直接验证等待异步释放，符合上一轮“至少一个异步释放场景”要求；失效测试映射名称已修正。
- 修复目标：候选释放计数能观察每一次方法调用，然后用六个集成场景证明框架只调用一次。
- 修复要求：
  1. 采用不自我去重的计数回调或等价机制；释放门控可清理，但不能使重复方法调用不再计数。
  2. 增加计数夹具敏感性校验：主动调用两次时应记录两次。
  3. 六个 Pre/Main/Post × 失败/取消场景仍应记录一次；保留门控、旧代可用、重复宿主清理和弱引用回收断言。
  4. 修正 execution.md/test-mapping.md 的“恰好一次”证据说明，避免仅以测试绿灯判定验收完成。
- 验证方式：先验证计数器识别重复调用，再顺序跑 Core 双框架相关热插件测试；仅修改夹具/断言时可复用生产代码未变的其余回归证据。

## 本轮验证与证据复用

Change Impact Analysis：生产、正式测试、公开 API、TFM、依赖和构建图均未修改；新增独立夹具诊断探针及审查文档。验证风险集中在取消任务结果与释放计数有效性，不重跑无变化的完整矩阵。

| 本轮实际执行，SDK 8.0.424 | 结果 |
| --- | --- |
| Core HotPluginHostTest + ModuleLifecycleReviewRound18Test，net8.0/net6.0 | 各 29 passed、0 failed |
| MVC UseBingAsync_CancellationPreservesModuleCause，net8.0/net6.0 | 各 1 passed、0 failed |
| 独立计数探针，net8.0/net6.0 | 两次释放调用均只记录一次，证明测试盲点 |

复用上一轮已实际执行的证据：Core 双框架各 449 passed；MVC 双框架各 53 passed、1 既有 skipped；WebApi 构建通过。本轮不将这些全量数字写成重跑结果。NU1900 是包漏洞数据源不可达，未影响本地编译和验证。不以一次工作集变化证明没有泄漏。

## TODO 分类

### Completed

- [x] T1–T4 与 T5 的生产接入。
- [x] FIX-018：取消状态、原异常诊断及兼容启动时机。
- [x] FIX-017 的阶段矩阵、异步等待门控、候选回收及失效映射修正。

### Open Actionable：1 项

- [ ] FIX-017：修正候选释放计数夹具，补两次调用计两次的敏感性校验，并重新验证六个集成场景只释放一次。

### Deferred：6 项后续能力，不计入本期缺陷

- [ ] 自定义生命周期阶段/阶段贡献者。
- [ ] 应用上下文与配置便捷 API。
- [ ] 模块附加程序集及更细扫描策略。
- [ ] 插件在线分发与多版本求解（当前已有本地版本区间校验）。
- [ ] 不可信插件进程隔离。
- [ ] 动态 Web 管道替换。

### 其他分类

- Blocked Approval / Blocked External：无。
- Accepted Limitations：插件完全可信；默认加载上下文不可卸载；热更新按整组插件代切换；旧静态兼容入口保留单宿主语义。
- Verified Boundaries：本轮未新增容量边界结论。
- Not Applicable：全仓库安全审计、性能基准和发布动作。

No-Progress Check: CHANGED（FIX-018 关闭；探针证实 FIX-017 的计数盲点）。Next Action: STOP_REVIEW。由后续 Fix 处理 1 个 OPEN_ACTIONABLE，本轮不自动修复。
