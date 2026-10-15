<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-MODULE-ASYNC-PHASES-20260928
AI_REVIEWED_AT: 2026-09-28

# 四阶段接口计划复核

## 当前结论

以会话批准的“四阶段接口与 ABP 风格生命周期完善计划”和仓库 plan.md 为范围，六组核心能力均已实现。本轮对照真实调用链及上轮新增测试，未确认新的功能缺失或可执行代码缺陷。原 FIX-001、FIX-002、FIX-003 均关闭；锁定 SDK 验证单独记录，不将环境缺失转换为代码问题。

IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS（复用实际 SDK 8.0.425 的相关测试证据）
RESOURCE_EVIDENCE_STATUS: PASS（受测热更新候选的弱引用与释放次数）
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（未安装 SDK 8.0.424）
RELEASE_STATUS: BLOCKED（锁定 SDK 验证未完成）
OPEN_ACTIONABLE: 0

## 验收矩阵

| 计划能力 | 状态 | 真实实现与证据 |
| --- | --- | --- |
| 四个同步接口、四个独立异步接口、基类八接口实现 | PASS | Modularity 下独立接口文件；BingModule 声明并实现八接口；默认模块旧入口测试 |
| 唯一分派、有效重写优先级、基类桥接 | PASS | BingModuleHooks.ConfigureAsync / InitializeAsync 与 BingModule 的接口桥接；识别继承重写和独立接口，无插件类型静态缓存；Pre/Post 优先级及防递归测试 |
| 同步预检和旧入口兼容 | PASS | 应用注册阶段 RequiresAsyncConfiguration、管理器 RequiresAsyncInitialization、旧构建器 HasNewServiceHooks；仅 Post 异步接口与默认模块直接测试 |
| 全局阶段、取消、失败清理及资源所有权 | PASS | 配置和初始化均逐轮 await；初始化三阶段共用上下文和短期作用域；原始异常附加诊断，失败进入统一清理；真实等待与调用方取消测试、既有逆序清理测试 |
| Web、Generic Host、热更新 | PASS | Web 上下文直接测试及既有 Host 停止测试；BuildGenerationAsync 等待应用注册和完整初始化后才返回候选；失败保留旧代，成功/失败候选回收测试 |
| 文档、中文注释与追溯映射 | PASS | README、迁移说明及 test-mapping.md；完整显式接口示例有临时消费者编译运行证据 |

## 上轮审查项关闭

### FIX-001：CLOSED

四个独立异步接口均由 ModuleAsyncPhaseBoundaryTest 直接覆盖：

- AsyncInterface_WaitsBeforeRunningFollowingPhase：真实 TCS 等待期间注册/初始化不完成，放行后校验阶段序列。
- AsyncInterface_FailureAfterAwaitPreservesOriginalException：挂起后故障保留原始异常实例、阶段诊断并释放模块。
- AsyncInterface_CallerCancellationStopsFollowingPhase：由调用方 CTS 取消等待，验证令牌、初始化阶段诊断、停止后续阶段及释放次数。初始化取消异常通过直接 await 捕获。
- ModuleAsyncNamedPhaseInterfacesTest 扩充 Post 同步/异步重写优先级、接口调用 base 防重复，以及 SyncEntries_RejectPostOnlyAsyncInterfacesBeforeAnyHook。

证据文件：framework/tests/Bing.Core.Tests/Modularity/ModuleAsyncPhaseBoundaryTest.cs、ModuleAsyncNamedPhaseInterfacesTest.cs。无需把共享执行链的所有组合继续展开为新增必修项。

### FIX-002：CLOSED

隔离插件夹具新增 FixtureHotAsyncPreFailureModule 与 FixtureHotAsyncPendingPostModule：

- ReloadAsync_InitializationPhaseFailure_ShouldKeepCurrentGeneration 包含独立异步 Pre/Post 接口失败参数，校验旧代服务及失败上下文回收。
- ReloadAsync_AsyncPostInterface_ShouldPublishThenCollectCandidate 校验 Post 等待期间旧代继续服务、完成后发布候选、再次切代后候选上下文回收，且释放一次。
- 既有异步配置失败测试仍覆盖失败候选释放次数。

资源结论限于受测可回收加载上下文和模块实例，不推断任意插件代码均无泄漏，不要求默认加载上下文中的程序集卸载。

### FIX-003：CLOSED

迁移指南补齐 using、CacheService、CacheModule 和 Program.Main，解决顶级语句排列和未定义类型问题。上轮以临时 net8.0 消费者项目、本机 NuGet 缓存离线还原，成功编译并运行。分派说明已与当前八接口行为对齐。

## 本轮验证与证据复用

本轮仅修改当前审查报告，没有修改生产代码、测试或执行记录。

- L0：读取当前源码与工作区/暂存区差异；git diff --check 通过，仅有现有换行提示。
- SDK：dotnet --version 为 8.0.425；dotnet --list-sdks 未包含 8.0.424；global.json 未修改。
- L1/L3 复用上轮紧邻执行结果：两个异步阶段测试类及 HotPluginHostTest，在 net8.0、net6.0 下各 58 项通过，0 失败。
- L2/L4 复用此前未受本轮改动影响的基线：Core 双框架各 532 项通过；MVC 双框架各 58 项通过、1 项跳过；WebApi 示例构建成功。532 为此前测试数量，不冒充新增测试后的本轮全量数量。
- 本轮未重跑测试或基准；相关实现未发生新的修改，没有扩大验证范围的依据。

## 分类 TODO

- Completed：六组核心能力；原三项本地可执行审查项 3/3 关闭。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：在安装 SDK 8.0.424 的环境，依次验证 Core net8.0/net6.0、MVC net8.0/net6.0 与 WebApi 示例构建，并保存版本及结果；不得修改 global.json 绕过。
- Not Applicable：本轮无性能热路径变更，不要求新增基准。
- Accepted Limitations：可信插件；热更新为独立容器的整组切换；不提供不可信代码隔离或动态替换 Web 管道。
- Verified Boundaries：受测成功/失败候选上下文可回收，释放次数符合断言。
- Deferred：自定义生命周期阶段、不可信插件隔离、动态 Web 管道替换不属于本次四阶段接口计划，不能计入本期缺失数量。

Next Action: STOP
Goal Status: STOPPED_BLOCKED（仅锁定 SDK 验证未完成）
