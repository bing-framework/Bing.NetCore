<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_EXECUTION_FINISHED_AT: 2026-09-28T09:42:15Z

# 实施执行报告

## 执行结论

完成计划中的上下文便捷访问与附加程序集扫描扩展。注册期配置、环境读取只取最后一个显式单例实例；初始化和关闭上下文从当前作用域读取。模块声明或应用选项可增加扫描程序集，约定 DI 可单独筛选候选类型；选项绑定与类型事件仍覆盖完整扫描范围。插件附加程序集在临时依赖解析会话内预检，热更新失败保留旧代。

- IMPLEMENTATION_STATUS: COMPLETED
- TEST_STATUS: PASS（MVC 各有 1 项既有跳过）
- RESOURCE_EVIDENCE_STATUS: PASS（插件旧代加载上下文弱引用回收；失败候选不替换当前代）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE（无吞吐量或内存容量改善结论）
- EXTERNAL_GATE_STATUS: PASS（仓库锁定的 .NET SDK `8.0.424`）
- RELEASE_STATUS: PASS（验证完成；未发布、提交或推送）
- OPEN_ACTIONABLE: 0

## 计划任务

| 项目 | 状态 | 实际结果 |
| --- | --- | --- |
| CTX-01 | COMPLETED | 修正注册期配置读取优先级与实例语义；新增通用宿主环境入口，工厂不提前执行 |
| CTX-02 | COMPLETED | 初始化与关闭上下文增加配置、环境必需及可选读取；真实 Pre、主、Post、关闭流程有直接测试 |
| SCAN-01 | COMPLETED | 模块继承属性、应用附加程序集、模块描述符程序集快照及加载上下文校验接入模块图 |
| SCAN-02 | COMPLETED | 统一冻结的程序集和类型目录供约定 DI、选项、晚选项补扫及公开类型事件使用；筛选仅作用于约定 DI |
| SCAN-03 | COMPLETED | 插件私有附加程序集在临时解析回调中预检；成功切换可回收旧代，缺失依赖保留当前代并报告插件阶段 |
| DOC-01 | COMPLETED | Core README、模块迁移指南、插件指南、ADR-0014 与索引、生产符号到测试方法映射 |

## 验证证据

使用 `8.0.424` 且未改动 `global.json`、生产目标框架、包版本或生产依赖。项目还原使用本机 NuGet 包缓存；后续测试与构建使用 `--no-restore`。

| 验证 | 结果 |
| --- | --- |
| `Bing.Core.Tests` `net8.0` | 493 通过、0 失败、0 跳过 |
| `Bing.Core.Tests` `net6.0` | 493 通过、0 失败、0 跳过 |
| `Bing.AspNetCore.Mvc.Tests` `net8.0` | 53 通过、0 失败、1 既有跳过 |
| `Bing.AspNetCore.Mvc.Tests` `net6.0` | 53 通过、0 失败、1 既有跳过 |
| `Bing.Samples.WebApi` 构建 | 成功，0 错误 |
| `git diff --check` | 通过；Git 仅提示已有工作树换行符转换 |

新增直接测试覆盖注册期工厂不执行、非根配置、重复描述符、当前作用域读取、真实生命周期阶段、声明继承、附加程序集早晚选项注册、两种扫描时机、约定 DI 筛选与异常、跨代拒绝、插件私有程序集回收及失败时旧代保留。插件资源测试以 `AssemblyLoadContext` 弱引用和服务可解析性为证据，不以一次工作集变化断言没有泄漏。完整映射见 [test-mapping.md](test-mapping.md)。

## 兼容与限制

注册期 `GetConfiguration` 的旧行为若依赖提前执行工厂，需改成实例注册或在运行期通过 DI 解析；此变化已写入迁移文档。附加程序集只扩大服务扫描目录，不加载额外模块，也不提供可信插件代码的安全隔离。调用方如果长期持有插件类型、程序集或描述符，仍会阻止加载上下文回收。既有 `net6.0` 支持结束及离线 NuGet 审计警告不属于本任务修改范围。

任务状态脚本尝试写 `.agents/runtime/current-task.json` 时遇到 `EPERM`；实现、测试、映射和本终态报告均已完成，未修改受保护的运行时元数据。

## Review 修复记录（2026-09-28）

独立审查记录 [review.md](review.md) 提出 FIX-001 至 FIX-005。本轮完成对应修复和直接测试；审查文件保持原样，尚待独立复审更新其状态。

| Finding | 本轮处理 | 验证方法 |
| --- | --- | --- |
| FIX-001 | 配置回调结束后冻结扫描模式；Pre/Post 修改调用方原选项不会重复或遗漏类型事件，也不会把随后追加的程序集纳入本轮扫描 | `ModuleScanningTest.RegistrationModeMutation_ShouldNotRepeatOrSkipTypeEvents`，双框架、同步与异步、两种模式和两个修改阶段 |
| FIX-002 | 约定类型查找器在注册结束时保留已筛选结果并释放筛选委托；自动 DI 关闭时不持有委托 | `ModuleScanningTest.ConventionalFilter_ShouldReleaseCapturedObject`，服务集合仍存活时检查捕获对象弱引用；覆盖成功、失败及自动 DI 关闭 |
| FIX-003 | 宿主环境没有直接注册时从最后一个有效 `HostBuilderContext` 实例读取；直接注册优先且工厂不提前执行 | `ModuleContextAccessTest.GetHostEnvironment_FallsBackToLastHostBuilderContext`、`GetHostEnvironment_DirectRegistrationPrecedenceAndInvalidRegistration`、`GetHostEnvironment_FallbackDoesNotExecuteFactoriesAndMissingEnvironmentThrows` |
| FIX-004 | 附加程序集类型发现失败带扫描阶段、程序集、声明模块、加载器错误；插件候选补充插件 ID、清单和阶段 | `HotPluginHostTest.ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate` |
| FIX-005 | 补充同名不同版本附加 DLL 的代际切换与弱引用回收；缺失 DLL 和类型依赖缺失两条失败候选回收；未选模块独立范围、重复输入去重、自定义 loader、描述符只读和输入冻结 | `HotPluginHostTest.ReloadAsync_AdditionalAssemblyVersions_ShouldSwitchAndCollectOldContext`、`ReloadAsync_MissingAdditionalAssembly_ShouldKeepCurrentGeneration`、`ModuleScanningTest.UnselectedModuleDeclaration_ShouldNotScanItsUniqueAssembly`、`ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly` 及上述边界测试 |

本轮使用仓库锁定的 SDK `8.0.424` 顺序复测：Core `net8.0` / `net6.0` 各 487 通过；MVC 两框架各 53 通过、1 项既有跳过；WebApi 构建成功、0 错误；`git diff --check` 通过。资源结论依据加载上下文及捕获对象弱引用，不以工作集波动为依据。离线 NuGet 审计和既有代码警告未作为功能失败处理。未修改版本号或生产依赖，未提交或推送。

当前修复轮 `OPEN_ACTIONABLE=0`（按 FIX-001 至 FIX-005 的已知项逐项核对）；此值不代表独立复审已经给出 PASS。

## Review 修复记录：追加查找器的资源所有权

- Review 状态：NEEDS_FIX；Fix Scope：recommended；Review 文件：[review.md](review.md)。
- Finding：FIX-002；严重程度 MEDIUM；处理要求 SHOULD_FIX；执行状态 COMPLETED（待独立复审）。
- 根因：注册结束时按服务集合中最后一个 `IDependencyTypeFinder` 描述符清理，模块追加实例或类型注册后无法定位框架创建的原查找器。
- 修复：`AddBingApplication` 与 `AddBingApplicationAsync` 通过 `Prepare` 的输出参数持有本轮原查找器，在同步/异步 `finally` 中直接完成扫描并解除筛选委托。模块随后追加的服务描述符保持原样。
- 直接测试：`ModuleScanningTest.ConventionalFilter_WithAppendedFinder_ShouldReleaseCapturedObject` 覆盖追加实例/类型后的成功、Post 异常及异步取消，共六条路径。集合仍存活时，弱引用确认筛选对象可回收；成功路径确认允许类型仍在、排除类型仍不在。原 `ConventionalFilter_ShouldReleaseCapturedObject` 同时增加允许类型的正向断言。
- 变更影响分析：仅 Core 内部应用注册资源清理和 Core 测试变更；公开契约、目标框架、生产依赖、Provider、构建图与性能热路径未改变。MVC 为直接消费者。无 SQL 或 Benchmark 要求。
- 验证：SDK `8.0.424`；ModuleScanningTest 双框架各 30 项通过；Core 双框架各 493 项通过；MVC 双框架各 53 项通过、1 项既有跳过；Core 构建通过。WebApi 构建复用同一工作区上轮结果，本轮未重跑，因为公开 API 和 Web 管道均未变。
- 本轮纳入的 MUST_FIX：0；SHOULD_FIX：1，已实施 1；PARTIAL/BLOCKED/FAILED：0。Executor 视角下已知 `OPEN_ACTIONABLE=0`，下一步是独立复审。没有提交或推送。

工作流 `task-state.mjs review-fix` 在更新 execution.md 后因 `.agents/runtime` 写权限返回 `EPERM`；该脚本状态不作为代码验证结果。
