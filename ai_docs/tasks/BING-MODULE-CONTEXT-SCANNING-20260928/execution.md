<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_EXECUTION_FINISHED_AT: 2026-09-28T12:43:00.547057+00:00

# 实施执行报告

## 后续能力实施：UX-001 / UX-002

上一轮 [review.md](review.md) 提出的两项可选易用性 TODO 已按建议顺序完成。该审查是实施前的独立快照，不能把其中的旧待办状态当作本轮最终实现状态。

- UX-001：`ReloadAsync` 保留原异常类型，在异常数据中提供 `BingHotPluginReloadFailureInfo`。统一的代 ID 关联本次尝试、切换前和失败时当前代；阶段区分等待、候选构建、旧代排空与关闭。候选失败明确 `Switched=false`，旧代关闭失败明确 `Switched=true`。
- UX-002：`BingHotPluginHost.Status` 返回当前代及退役代的只读快照。每代包含稳定 ID、插件 ID/版本、活动调用数及 `Active` / `Draining` / `Closing` 状态。状态来源于已有宿主和代锁，快照不持有插件类型、程序集、作用域或服务对象；没有新增轮询、超时强制释放或静态缓存。
- 文档：插件指南增加实际异常处理示例、状态解读与快照边界；迁移指南说明新增 API 不要求既有调用方更改 `ReloadAsync` 签名。生产符号到测试映射见 [test-mapping.md](test-mapping.md)。

变更影响：生产仅 `Bing.PluginRuntime`，新增公开诊断/快照契约且保留现有方法签名与异常类型；新增 Core 测试文件并更新文档及本报告。Core 测试项目直接引用 PluginRuntime；MVC 与 WebApi 不引用该项目，本轮复用其上一轮未变化范围的验证证据。目标框架、包版本、生产依赖、SQL 和 Benchmark 均未变。风险 MEDIUM，先定向测试，再双框架 Core 全量。

| SDK `8.0.424` 本轮验证 | 实际结果 | 证据路径（TestResults/modularity-audit-20260928） |
| --- | --- | --- |
| PluginRuntime 构建，net8.0/net6.0 | 均成功，0 警告、0 错误 | 构建命令输出 |
| HotPluginObservabilityTest，net8.0/net6.0 | 各 7 通过、0 失败 | hot-observability-final-net8.0/net6.0.trx |
| Core 全量，net8.0/net6.0 | 各 504 通过、0 失败 | core-observability-final-net8.0/net6.0.trx |
| L0 | `git diff --check` 与相关文档本地链接、UTF-8 检查通过 | 当前工作区 |

同版本重载仍产生不同代 ID；受控长调用显示 `Draining` 与活动数，受控关闭钩子显示 `Closing`；结束后退役代从快照消失。候选配置/初始化失败保持旧代，旧代关闭失败仍可使用新代。持有旧代快照时，旧代加载上下文弱引用仍可回收。测试帮助库的取消异常断言最初未保留异常数据，改为直接捕获调用方实际接收的异常后验证通过；生产代码无需为此调整。

当前分类：Completed 为 UX-001、UX-002；Open Actionable 无。Blocked External 仍是 NuGet 漏洞数据源审计。Deferred 仍为自定义阶段、在线分发/版本求解、不可信插件隔离和动态 Web 管道替换。Accepted Limitations 为可信插件、协作排空、默认上下文不可卸载及静态依赖预加载边界。No-Progress Check: CHANGED；Next Action: STOP（本轮可执行 TODO 已完成）。未 commit/push/发布。

## 当前修复结论：FIX-006 / FIX-007

本轮模式 REVIEW_FIX，范围 recommended，纳入 2 个 HIGH / MUST_FIX；均已实施并完成验证。保留 [review.md](review.md) 的修复前独立审查内容，不把 Executor 结论写成 Reviewer PASS。对三个生产文件及新增测试的独立窄范围复核未发现新可执行问题；后续正式复审可依据下列证据更新审查记录。

- IMPLEMENTATION_STATUS: COMPLETED（本轮 2/2）
- TEST_STATUS: PASS
- RESOURCE_EVIDENCE_STATUS: PASS（释放次数、内部引用清空、成功旧代及失败候选弱引用）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（NuGet 漏洞数据源 TLS 失败，未重新宣称完成依赖安全审计）
- RELEASE_STATUS: BLOCKED（安全审计证据未齐；本轮未发布）
- OPEN_ACTIONABLE: 0（本轮修复范围）

### 修复与直接证据

| Finding | 根因与处理 | 直接测试 |
| --- | --- | --- |
| FIX-006 | 宿主 Build 后集合只读，RemoveAt 异常打断内部清理并覆盖启动异常。只读集合跳过描述符移除；可变集合仍移除，枚举/移除错误收集后继续清空实例、插件及所有权记录。初始化兜底捕获清理异常，保留原始异常并进入 Failed。未释放宿主根容器 | MVC `WebModuleResourceReleaseTest` 4 项真实 WebApplication 用例；Core `ModuleDescriptorCleanupFailureTest.InitializationFailure_WhenRemoveAtThrows_ShouldPreserveCauseAndFinishCleanup` |
| FIX-007 | 类型预检不会加载仅在方法体引用的私有 DLL。临时解析会话内预加载已部署、静态引用可达的托管依赖闭包，补齐附加程序集的依赖；沿用身份校验和宿主契约复用。解析事件仍 finally 退订，候选表/已访问集合不进静态缓存，不扩大 DI 扫描 | `PluginPrivateDependencyTest` 3 项独立入口/Helper/Leaf 夹具测试：默认与热更新的配置、初始化、服务调用；旧代和失败候选回收、旧代继续服务、扫描范围不变 |

新增夹具均 `ReferenceOutputAssembly=false`，测试宿主无直接程序集引用。缺失依赖、身份冲突与事件退订复用本轮实际执行的 `HotPluginHostTest.ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate`、`PluginDependencyGraphTest.AddBingApplication_AssemblyIdentityConflict_ShouldFailBeforeModuleLoading`、`PluginResourceRegressionTest.LoadedAssemblyIdentityMismatch_ShouldFailResolver`、`SuccessfulRegistration_ShouldUnsubscribeAssemblyResolver`、`FailedRegistration_ShouldUnsubscribeAssemblyResolver`。

部署边界：缺失但未实际使用的可选引用仍按旧行为留到使用路径报错；字符串动态加载、运行中新出现 DLL、本机依赖不属于静态闭包预加载。只读集合保留的描述符随宿主结束，不建议外部长时间保留已关闭的宿主。此处不宣称任意插件无泄漏或构成安全沙箱。

### 影响分析与本轮验证

ChangedFiles：Core 三个内部实现、Core/MVC 直接测试、三个隔离测试夹具项目、测试复制目标及文档。ChangedProjects：Bing.Core、Bing.Core.Tests、Bing.AspNetCore.Mvc.Tests；AffectedDependents：PluginRuntime、WebApi。ChangedRuntimePaths：程序集加载与关闭/启动失败清理。PublicContracts、Providers、TFMs、生产依赖/版本、BenchmarkHarness 均未改动。RiskLevel: HIGH；先 L1 定向再 L2/L3 回归，无需全解决方案、SQL 或容量基准。

使用仓库锁定 SDK `8.0.424`，顺序执行 Core net8/net6、MVC net8/net6、WebApi。测试恢复时显式复用本机 NuGet 缓存，未修改 global.json 或生产依赖。初次还原未指定 NUGET_PACKAGES 时因新 CLI home 查找源失败；指定已有缓存后成功，后续均 --no-restore。初次新增测试将兼容阶段误写为 ConfigureServices，按已有契约纠正为 AddServices 后通过。

| 本轮验证 | 结果 | 证据（位于 TestResults/modularity-audit-20260928） |
| --- | --- | --- |
| L1 私有依赖 | net8/net6 各 3 通过 | core-private-dependency-net8/net6.trx |
| L1 真实 Web 资源释放 | net8/net6 各 4 通过 | mvc-resource-release-net8/net6.trx |
| L1 描述符移除异常 | net8 1 通过；net6 随全量通过 | core-descriptor-cleanup-net8.trx |
| L2 Core | net8/net6 各 497 通过，0 失败 | core-fix-final-net8.0/net6.0.trx |
| L2/L3 MVC | net8/net6 各 57 通过、1 既有跳过，0 失败 | mvc-fix-final-net8.0/net6.0.trx |
| L3 WebApi 构建 | 0 错误，1 个 net6 支持结束警告 | webapi-fix-final.log |
| L0 | git diff --check 通过，文档本地链接及 UTF-8 检查通过 | 当前工作区 |

核心资源结论依据实际释放一次、描述符引用清空及 ALC 弱引用收集，不使用工作集波动作为证据。此次保留并更新 Core README、插件指南和迁移指南中的使用说明，新增测试映射见 [test-mapping.md](test-mapping.md)。

### 分类 TODO 与收口

- Completed：FIX-006、FIX-007，本轮 2/2；新增 Core 4 项、MVC 4 项用例（均双框架覆盖）。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：NuGet 直接/传递依赖漏洞数据源审计，沿用上轮阻碍证据，不因其失败循环改代码。
- Not Applicable：SQL、吞吐基准、容量矩阵、发布动作。
- Accepted Limitations：默认 ALC 不卸载、可信整组热更新、协作排空、动态字符串/本机依赖不由本次预加载处理。
- Verified Boundaries：未新增容量边界结论。
- Deferred：UX-001 切换结果诊断、UX-002 排空可观测性；自定义生命周期阶段、在线分发/版本求解、不可信进程隔离、动态 Web 管道替换。

No-Progress Check: CHANGED；Next Action: STOP（当前可执行修复已完成，正式 Review 保持独立）；Goal Status: STOPPED_BLOCKED（外部审计）。未 commit/push。task-state.mjs 因 .agents/runtime 写权限 EPERM 未完成状态登记；随后 task-finish.mjs --no-notify 因 current-task.json 不存在或任务 ID 不匹配退出。未发送外部通知；不影响代码与测试交付。

## 历史实施记录（以下测试数字不是本轮结果）

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
