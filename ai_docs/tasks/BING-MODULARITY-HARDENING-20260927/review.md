<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-28T07:49:15Z

# Round 22：生命周期计划差距与 FIX-017 复核

## 结论与范围

对照用户最新批准的“模块完整生命周期与 ABP 风格钩子完善计划”T1–T5、原 plan.md、execution.md Round 21、当前源码及未跟踪文件、工作区和暂存区 Diff、迁移说明与直接测试。

**四组必需能力均已接入，T1–T5 未发现尚未实现的计划项。上一轮唯一可执行问题 FIX-017 已闭环，当前 OPEN_ACTIONABLE 为 0。另有 6 项明确延期方向，不计作本期缺陷。** 这是上述模块生命周期计划的验收，不是全仓库安全审计或所有可能输入的正确性证明。

- IMPLEMENTATION_STATUS: PASS
- TEST_STATUS: PASS（本轮定向验证及未变范围的既有回归证据）
- RESOURCE_EVIDENCE_STATUS: PASS（计划要求的释放计数、异步等待及弱引用回收范围）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- RELEASE_STATUS: NOT_ASSESSED
- OPEN_ACTIONABLE: 0
- BLOCKED_APPROVAL / BLOCKED_EXTERNAL: 0
- GOAL_STATUS: STOPPED_COMPLETED

历史 Round 20 审查保存在 [备份](review-before-round22-20260928.md)。本轮仅更新审查报告并保留备份，未修改生产代码、测试、plan.md 或 execution.md。

## 计划验收矩阵

| 计划项 | 状态 | 实际实现与证据 |
| --- | --- | --- |
| T1 基类钩子与兼容分派 | PASS | BingModule 提供配置、初始化 Pre/Main/Post 及单阶段 Shutdown 的同步/异步虚方法；BingModuleHooks 通过虚槽识别继承重写并排除隐藏方法，分派到新重写或旧接口/base 桥接。ModuleLifecycleReviewFixTest、ModuleLifecycleReviewRound18Test 覆盖继承隐藏、显式/隐式接口与至多一次调用。 |
| T2 异步应用注册 | PASS | BeginRegistration 在配置回调前占用集合；AddBingApplicationAsync 共用 Prepare/ConfigureModulesAsync，失败等待 FailConfigurationAsync。BingBuilder 拒绝旧入口无法执行的新配置重写。ModulePhasedLifecycleTest 覆盖异步配置、重入、提前构建与取消。 |
| T3 全局初始化阶段 | PASS | BingModuleManager.InitializeCoreAsync 对同一序列分三轮执行，共享短期上下文；Post、作用域释放及启动日志完成后才设为 Initialized。已开始模块引用去重，异常路径逆序清理；取消保留原异常诊断及 Canceled 状态。三阶段、作用域、异常、并发和 Web 测试已有直接覆盖。 |
| T4 扫描时机 | PASS | ConfigureModulesAsync 在全部 Pre 后执行可选提前扫描及 RegisterTypes，Compatible 在配置末尾注册类型；DependencyModule 通过注册记录避免重复约定扫描。AutoRegisterServices 只关闭约定 DI。扫描时机、公开事件、晚注册选项及服务替换已有直接测试。 |
| T5 热更新与宿主 | PASS | BingHotPluginHost.BuildGenerationAsync 等待 AddBingApplicationAsync 和 UseBingAsync 完成，ReloadAsync 才替换当前代；异常保留旧代并卸载候选上下文。六个配置失败/取消场景、异步清理门控、候选回收及计数敏感性本轮通过。Web 即时装配及 Generic Host 停止沿用既有验证。 |

既有依赖图校验、启动期插件、可回收加载上下文、整组插件热更新/卸载和本地版本区间校验已实现，不再列为未实现 TODO。原 plan.md 中的历史延期描述以用户后续批准的计划及已验证实现为准。

## FIX-017：CLOSED

- 原问题：测试夹具首次 DisposeAsync 清空计数回调，重复调用也只计一次。
- 当前证据：PluginModules.cs:408 的 DisposeAsync 保留计数回调，每次调用均计数，仅清空可选门控。
- 敏感性证据：HotPluginHostTest.cs:31 的 ConfigurationDisposalFixture_ShouldRecordEveryInvocation 对真实夹具连续调用两次 DisposeAsync，直接断言计数 2。
- 集成证据：HotPluginHostTest.cs:143、203 的两个 Theory 覆盖 Pre/Main/Post 配置失败与取消共六个场景，验证旧代服务可用、候选 ALC 可回收、候选释放计数为 1，宿主释放后仍为 1。
- 异步等待：前置配置失败场景保留释放门控，未放行时 ReloadAsync 不完成且旧代可服务。符合既有验收要求，不扩张为六个场景均须使用相同门控。
- execution.md Round 21 与 test-mapping.md 已记录对应方法及计数行为。

FIX-018 保持 CLOSED；本轮没有发现使其取消状态、异常实例及 Web 兼容证据失效的变化。

## 验证与证据复用

Change Impact Analysis：上一轮 Fix 仅涉及 Core 测试夹具、直接测试和文档；生产路径、公开契约、TFM、依赖及构建图未变。RiskLevel: LOW。本轮选择 FIX-017 的最小有效测试集，未重复全量矩阵。

| 本轮实际执行，SDK 8.0.424 | 结果 |
| --- | --- |
| Core net8.0：计数敏感性 1 项 + 配置失败 3 项 + 配置取消 3 项 | 7 passed、0 failed |
| Core net6.0：同一测试集，顺序运行 | 7 passed、0 failed |

复用未变范围证据：Round 21 Core 全量双框架各 450 passed；Round 19 MVC 双框架各 53 passed、1 既有 skipped；WebApi 构建通过。这些不是本轮重跑结果。NU1900 表示包漏洞数据源不可达，不把单元测试通过当作第三方依赖漏洞扫描通过。资源结论来自实际释放次数、异步完成和弱引用回收，不用一次工作集变化断言没有泄漏。

## TODO

### 本期

- [x] T1–T5 全部能力接入。
- [x] FIX-017 计数敏感性及六个集成场景闭环。
- [x] FIX-018 取消任务状态与原始异常诊断。
- [x] 更新审查状态和差距列表。

当前没有 OPEN_ACTIONABLE，不自动进入下一轮 Fix。

### 后续扩展：6 项 DEFERRED

以下能力在最新计划中明确延期，不阻碍本期验收。建议按应用实际需要另立任务，前两项优先评估。

- [ ] 应用上下文与配置便捷 API：在现有短期初始化/关闭上下文之上提供统一便捷访问，明确 scoped 对象寿命。建议任务：模块应用上下文增强。
- [ ] 模块附加程序集及更细扫描策略：支持模块声明额外程序集和筛选范围，保持宿主隔离。建议任务：模块扫描策略扩展。
- [ ] 自定义生命周期阶段/阶段贡献者：目前是固定 Pre/Main/Post 和单一关闭阶段，另行定义阶段排序及异常策略。建议任务：生命周期阶段扩展点。
- [ ] 插件在线分发与多版本求解：已有本地版本区间校验，尚非远程下载、升级编排和候选版本求解系统。建议任务：插件分发与依赖求解。
- [ ] 不可信插件进程隔离：当前插件完全可信，AssemblyLoadContext 不提供安全沙箱。建议任务：隔离插件宿主。
- [ ] 动态 Web 管道替换：目前在 UseBing/UseBingAsync 中装配，整组插件热更新不替换已装配管道。建议任务：Web 插件路由与管道更新。

Accepted Limitations：启动期默认加载上下文中的程序集不支持卸载；热更新按整组插件代切换；旧静态兼容入口仅支持单宿主语义。完整 ABP 上层生态不在此次模块生命周期对齐范围。

No-Progress Check: CHANGED（FIX-017 计数盲点已修复并获得直接证据）。Next Action: STOP_REVIEW；仅在另立扩展任务或发现新的可执行问题时继续实施。