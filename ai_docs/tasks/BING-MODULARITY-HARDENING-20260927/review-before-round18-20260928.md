<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-28T13:12:00+08:00

# 完整生命周期计划实现审查

## 结论与范围

以用户最新批准的“模块完整生命周期与 ABP 风格钩子完善计划”T1–T5 为本轮验收标准，结合原 plan.md、execution.md、ADR、真实源码、当前 Git Diff、测试及隔离行为探针审查。原 plan.md 是早期模块化计划，最新聊天计划作为已批准增补，不据此阻塞审查。

四组主能力均已接入真实调用链，没有整组功能尚未实现；但确认 **4 项功能缺陷及 1 组直接测试缺口**，当前不能完整验收通过。不使用代码量百分比估算完成度。

- IMPLEMENTATION_STATUS: NEEDS_FIX
- TEST_STATUS: PARTIAL
- RESOURCE_STATUS: PARTIAL
- PERFORMANCE_STATUS: NOT_APPLICABLE（本期未声明性能验收目标）
- EXTERNAL_GATES: NONE
- OPEN_ACTIONABLE: 5（2 MUST_FIX、3 SHOULD_FIX）
- BLOCKED: 0

本轮只审查，不修改业务源码、正式测试、plan.md 或 execution.md。历史已关闭问题保留在 [上一版审查](review-before-lifecycle-20260928.md)，不重新打开；本轮新增编号 FIX-013 至 FIX-017。execution.md 上轮“完成”结论不能覆盖本轮新增的行为证据。

## 计划验收矩阵

| 项目 | 状态 | 已实现 | 剩余差距 |
| --- | --- | --- | --- |
| T1 基类钩子与兼容分派 | PARTIAL | 同步/异步虚钩子、旧接口回退、旧 AddServices/UseModule 桥接、异步预检、旧注册入口迁移提示 | 继承重写被隐藏方法遮蔽；显式调用 base 未续接部分旧接口 |
| T2 异步应用注册 | PARTIAL | 共用模块图、注册占用、重入/提前构建限制、异步失败清理 | 最后配置回调取消但正常返回时仍成功 |
| T3 全局三阶段初始化 | PARTIAL | 全模块 Pre/Main/Post、共享短期作用域、Post 后才完成、错误清理 | 混合 Pre 钩子时关闭顺序不再是模块序列逆序 |
| T4 可选扫描时机 | PASS | 默认兼容时机、Pre 后提前扫描、避免重复 DI、关闭自动 DI、晚注册选项补扫 | 已核查正常路径及相应现有测试；阶段取消边界归 FIX-015 |
| T5 热更新与宿主 | PARTIAL | 候选等待异步注册及三阶段初始化后切换；失败保留旧代；统一关闭 | 新钩子的候选配置失败/取消、Post 等待期间不切换和宿主异步关闭直接验证不足 |

生命周期钩子元数据没有新增静态 Type 缓存；本轮未发现此处新增明确的插件加载上下文静态保留路径。但关闭顺序与兼容回调遗漏可能影响模块资源清理，不能据已有弱引用用例推断全部新分支无泄漏。

## 验证证据

历史基线复用：Core net8.0/net6.0 各 413 通过；MVC 两框架各 50 通过、1 跳过；WebApi 示例构建成功。这是 execution.md 的上轮证据，本轮未重新运行完整矩阵，不能作为新增边界已经通过的证明。

本轮使用锁定 SDK 8.0.424、net8.0 隔离控制台探针，引用当前 Core 测试输出，直接复现四项行为。探针位于仓库 TestResults/lifecycle-review-probe/Program.cs，未改动生产及正式测试项目。进程退出码 0 仅表示探针执行完成，四项输出反映契约不符合，并非验收通过。

```text
MIXED_SHUTDOWN=pre-consumer,init-dependency,init-consumer,stop-dependency,stop-consumer
CANCELED_REGISTRATION_RETURNED_SUCCESS=True
HIDDEN_INHERITED_ASYNC_SYNC_ENTRY=legacy-sync
EXPLICIT_BASE_BRIDGE=new-pre,new-init,new-stop
```

审查基于 HEAD 2c36316e210ad2081520fe56e7413c748bad92dd 加当前未提交改动，包含未跟踪源码。没有通过修改版本、依赖、目标框架或 global.json 绕过验证条件。

## FIX-013：混合前置钩子导致关闭顺序错误

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 状态：OPEN_ACTIONABLE
- 问题：已开始模块按首次进入回调的顺序登记，关闭时反转该列表，不是反转原模块序列。
- 证据：BingModuleManager.cs:214–226 在分阶段循环内首次调用时追加 `_started`；:362 对 `_started` 直接 Reverse。依赖 A 只有主初始化，使用方 B 有 Pre 时，登记顺序是 B、A，实际关闭为 A、B。探针的 stop-dependency 早于 stop-consumer。
- 影响：使用方停止时依赖已停止，正常关闭和失败清理均可能破坏资源依赖关系；违反 T3 明确的逆模块序列关闭契约。
- 修复目标：关闭已开始模块时严格遵守本轮确定模块序列的逆序。
- 修复要求：独立维护冻结的执行序列与按引用身份去重的已开始集合；逆序遍历序列并筛选已开始模块。保持旧入口/Web 既有序列，资源只释放一次。
- 验证方式：混合有/无 Pre 的依赖模块，覆盖同步/异步正常关闭及 Pre/Main/Post 失败清理，断言完整关闭顺序与释放次数。

## FIX-014：隐藏方法遮蔽继承的有效重写

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 状态：OPEN_ACTIONABLE
- 问题：通过最派生类型 GetMethod 找同名成员，无法同时忽略 new 隐藏方法并保留中间基类真实 override。
- 证据：BingModuleHooks.cs:10–15 仅检查 GetMethod 返回成员的 GetBaseDefinition。探针中间基类重写异步初始化，派生类 new 同名方法，同步 UseBing 未预检拒绝，反而执行 legacy-sync。
- 影响：有效异步初始化被跳过，配置或关闭的同类检测也有相同风险；可能遗漏启动工作或资源清理。当前单纯隐藏方法测试不能验证这一继承组合。
- 修复目标：识别 BingModule 原始虚方法槽上的有效重写，排除新建隐藏槽。
- 修复要求：预检与执行使用一致解析结果，覆盖多级继承；不得用静态 Type/MethodInfo 缓存阻止插件卸载。
- 验证方式：配置、初始化、关闭分别覆盖“中间基类 override + 派生类 new”，验证异步入口执行正确重写且仅一次；同步入口在任何阶段副作用前拒绝有效异步实现。

## FIX-015：配置末尾取消仍完成注册

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题：配置钩子执行前检查取消，执行后和注册成功前没有完整的取消检查。
- 证据：BingApplicationServiceCollectionExtensions.cs:64–72、:124–166。最后一个 PostConfigureServicesAsync 取消传入令牌但返回 CompletedTask，AddBingApplicationAsync 返回成功，随后可构建 Bing 容器；探针输出 True。
- 影响：取消请求未转入计划要求的失败与异步资源清理路径；最后 Pre 回调取消也可能仍进入提前扫描产生额外副作用。
- 修复目标：配置回调、扫描阶段及注册提交边界保持一致取消语义。
- 修复要求：在回调完成后、进入后续扫描/阶段前、成功提交前检查令牌；取消后标记失败、等待异步释放、禁止重试和构建。不能撤销已发生的外部副作用。
- 验证方式：各阶段最后回调主动取消且正常返回；提前扫描事件及末尾公开事件取消；断言后续阶段不执行、注册不可重用、异步清理等待完成且只释放一次。

## FIX-016：显式调用 base 未续接旧接口

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题：新 override 被选择后，显式调用 base 无法继续桥接部分对应的旧接口，与 T1 规则 4 不符。
- 证据：BingModule.cs 的 PreConfigureServices/PostConfigureServices/OnApplicationShutdown 基类实现为空；初始化/关闭 Async 基类只调用同步钩子。探针实现显式 IBingPreConfigureServices、IBingAsyncModuleInitializer、IBingAsyncModuleShutdown，并在新重写中调用 base，输出只有 new-pre/new-init/new-stop，旧接口均未执行。
- 影响：迁移模块希望保留的旧配置、异步初始化、清理逻辑丢失。已有 ConfigureServices→AddServices 和同步初始化→Web/UseModule 桥接正常，不应删除。
- 修复目标：新重写默认替代旧回调，开发者显式调用对应 base 时可按约定续接旧接口。
- 修复要求：兼容隐式及显式接口实现，避免虚调用与接口调用递归或框架重复分派；保持不调用 base 时的替代语义。
- 验证方式：对应旧接口与新同步/异步重写组合，分别调用/不调用 base，断言完整事件序列、优先级及每条回调至多一次。

## FIX-017：补齐直接分派与集成验收测试

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE（证据缺口，不是额外已证实运行时缺陷）
- 问题：现有测试尚未覆盖计划指定的若干新钩子组合及集成边界。
- 证据：审查 Core/MVC 生命周期、扫描和热插件测试及生产符号映射。已有各阶段全局顺序、异常、抛取消、作用域、插件 Pre/Post 失败与弱引用测试，但未找到下列直接断言。
- 影响：历史全绿结果不能证明所有新增兼容与切换规则成立。
- 修复目标：补足剩余契约证据，并同步维护生产符号到测试方法映射。
- 修复要求与验证方式：
  1. 同一模块同时包含新同步/异步 override 与显式旧接口，断言优先级及至多一次；新异步 Pre/Main/Post 使同步初始化在其他模块产生任何 Pre 副作用前拒绝。
  2. 热更新候选异步配置各阶段失败/取消时旧代继续服务并清理候选；候选 Post 尚未完成时入口不能切换；成功/失败后加载上下文弱引用回收。
  3. Web 的新异步 Pre/Main/Post 共享宿主上下文与管道装配序列；Generic Host Stop 等待新 OnApplicationShutdownAsync override 完成。
  4. 与 FIX-013–016 回归用例统一验收，避免重复测试；顺序执行锁定 SDK 的 Core/MVC 双框架、示例构建及资源专项，记录实际结果。

现有 BeforeConfigureServices 模式的晚注册 AddOptionsType 补扫测试已经存在，不列为缺失项。已有旧关闭接口的 Host 测试不能替代新 override 路径的直接测试。

## TODO 与收口

### Completed

- [x] 四组主能力接入，三阶段初始化、异步应用注册与扫描模式存在真实调用链。
- [x] 热更新构建候选时等待异步注册及全部初始化阶段后才发布服务入口。
- [x] 本轮完成源码、测试覆盖及隔离行为验证，形成可执行 Finding。

### OpenActionable

- [ ] FIX-013：修正逆序关闭，补全混合 Pre 钩子回归。
- [ ] FIX-014：修正虚方法槽识别及一致分派，补全继承隐藏组合测试。
- [ ] FIX-015：补齐配置取消提交边界与异步释放验证。
- [ ] FIX-016：补齐显式 base 的旧接口桥接与至多一次验证。
- [ ] FIX-017：补齐新钩子的 Web、Host、热更新直接测试；更新测试映射和实际执行证据。

### Blocked / AcceptedLimitations / Deferred

- Blocked：无；本轮不因历史证据复用或未重复完整矩阵而自动修改代码。
- AcceptedLimitations：配置外部副作用不能回滚；可信插件不是安全沙箱；默认加载上下文程序集不可卸载，这些不算本轮新缺陷。
- Deferred：自定义生命周期阶段、应用上下文便利 API、模块附加程序集与细化扫描、插件在线分发及多版本求解；不可信插件隔离、动态 Web 管道替换另立任务。

建议先修 FIX-013/014，再修 FIX-015/016，随修随补直接回归，最后完成 FIX-017 集成门槛。审查到此结束，不启动自动修复循环。
