<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULE-INTERFACE-MERGE-20260929
AI_REVIEWED_AT: 2026-09-29T06:55:34+08:00

# 接口合并计划审查

## 范围与结论

以用户本轮完整计划为验收依据，结合目录内计划摘要、执行记录、暂存区/工作区改动和当前源码审查。基准提交：4255a969ab54240c25f9ce5ea9c8009b907664f6；工作区含更早的模块拆分和插件改动，不将这些改动全部归因于本轮。

未发现本轮缺失的运行时能力组。T1、T2 已实现，T3 文档与追溯仍需收尾。NEEDS_FIX 来自文档可用性问题，不表示生命周期实现缺失。

IMPLEMENTATION_STATUS: PASS（本轮运行时范围）
TEST_STATUS: PASS（复用上一轮实际执行证据）
RESOURCE_EVIDENCE_STATUS: REUSED_UNCHANGED_SCOPE
PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
EXTERNAL_GATE_STATUS: 8.0.424 NOT_VERIFIED，8.0.425 已有验证
RELEASE_STATUS: 本审查不作发布判定
GOAL_STATUS: 审查结束，未启动自动修复

## 验收矩阵

| 计划项 | 结果 | 证据 |
|---|---|---|
| 10 个阶段接口合并为 5 个 | PASS | 五个统一接口均声明同步/异步方法，无默认接口方法；旧类型仅在测试的字符串反射断言中出现 |
| BingModule 默认实现、关闭显式桥接 | PASS | BingModule.cs:9、297、312、328；默认实现不会被预检当成独立异步回调 |
| 逐方法接口映射、继承/隐藏识别 | PASS | BingModuleHooks.cs:16、39；旧配置接口仍比较基类虚方法，统一接口比较基类接口映射 |
| 前后阶段分派与异步预检 | PASS | BingModuleHooks.RequiresAsyncConfiguration/RequiresAsyncInitialization、ConfigureAsync/InitializeAsync 及管理器调用链 |
| 关闭选择规则与防递归 | PASS | RequiresAsyncShutdown；ModuleLifecycleReviewRound18Test.LegacyInterfacesCallingBase_RunOnce 同时验证同步/异步关闭；ModuleLifecycleReviewFixTest.SyncShutdownBaseCall_BridgesOldInterfaceOnce |
| 生产、测试、隔离插件引用迁移 | PASS | 当前源码无被删除接口的类型引用；主初始化接口和旧配置契约保留 |
| Web、Host、热更新与资源路径 | PASS | 现有 WebModuleLifecycleTest、HotPluginHostTest、ModuleResourceRegressionTest 和上一轮回归结果 |
| 文档与方法级测试映射 | PARTIAL | FIX-001、FIX-002 |
| 目标框架、依赖、版本约束 | PASS | 本轮未修改 Core 项目依赖或 global.json |
| SDK 8.0.424 验证 | NOT_VERIFIABLE | 本次列出的已安装 SDK 无 8.0.424；8.0.425 是计划允许记录的替代验证环境 |

## FIX-001：迁移文档需准确表达可注册模块与兼容边界

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：OPEN_ACTIONABLE
- 问题与证据：docs/migrations/modularity-runtime.md:192 的 ExplicitModule 未继承 BingModule，不能作为 AddBingApplication 的启动模块；注册入口明确具有 where TStartupModule : BingModule 约束。文档 :207 将“声明新接口”表述为旧入口拒绝条件，但基类已统一实现接口，真正条件是有效重写或独立实现。README.md:67 的“原有 IBing* 接口继续兼容”又涵盖了本轮删除的关闭异步接口，与 :79 的破坏性迁移说明不一致。
- 影响：按示例注册会遇到泛型约束错误；用户容易误判普通基类模块是否需要迁移。
- 修复目标：文档示例可作为模块注册，兼容性措辞与真实预检一致。
- 修复要求：为 ExplicitModule 增加 BingModule 基类或明确它仅展示不可注册的接口实现；旧入口限制写成实际配置钩子实现；明确保留的是 IBingAsyncModuleInitializer、IBingPreConfigureServices、IBingPostConfigureServices。
- 验证方式：将修正的示例纳入最小消费者编译/注册检查；文档检查即可，不要求重跑全量资源测试。

## FIX-002：测试追溯需落到具体方法

- 严重程度：LOW
- 处理要求：OPTIONAL
- 状态：OPEN_ACTIONABLE
- 问题与证据：test-mapping.md:7 的“组合钩子测试”和 :11 的测试类名未给出具体方法；末尾 CombinedOverridesModule 是夹具类型而非测试方法。
- 影响：难以由映射直接选择关闭分派、Host 停止和热更新回收的定向验收用例；不影响运行行为。
- 修复目标：补足计划要求的方法级追溯。
- 修复要求：引用 LegacyInterfacesCallingBase_RunOnce、CombinedOverrides_AsyncWinsAndSyncPreflightHasNoSideEffects、HostStop_ShouldRunAsyncModuleShutdown、ReloadAsync_AsyncPostInterface_ShouldPublishThenCollectCandidate 等实际测试方法，并写清对应行为。
- 验证方式：核对方法存在且断言覆盖所列行为。无须新增同义测试或再次执行完整测试集。

## 验证证据

本轮仅执行静态检查、源码/测试/文档核对、SDK 清单检查及 git diff --check，未重新运行测试，未修改生产代码和测试。

复用上一轮本会话实际结果：Core net8.0/net6.0 各 548 项全量通过；随后新增消费者用例在两个框架下各 1 项定向通过。MVC 两框架各 58 项通过、1 项跳过；跳过项为既有 RemoteStreamContentTestControllerTest 的 HTTP 415 用例。WebApi 构建成功。不得将这些结果写成本轮重新运行了 549 项全量测试。

## TODO 分类

- Completed：T1、T2 及主要测试迁移；未确认运行时缺失能力。
- Open Actionable：FIX-001 文档修正；FIX-002 方法级映射（建议收尾）。
- Blocked External：取得 SDK 8.0.424 后补锁定版本验证；环境缺失不转换为代码修复项。
- Blocked Approval：无。
- Accepted Limitations：用户已批准破坏性接口调整，外部模块/插件需重新编译。
- Not Applicable：本轮不涉及性能算法改造，不要求新增 benchmark。
- Verified Boundaries：无新增。
- Deferred：主初始化接口合并及更广泛模块能力不属于本轮范围。

建议先修正 FIX-001，再补齐 FIX-002；停止本轮审查，不自动开始修改。
