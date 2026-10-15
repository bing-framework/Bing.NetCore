<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-MODULARITY-DI-RESOURCE-20260929
AI_EXECUTION_STARTED_AT: 2026-09-29T09:31:29.232Z

AI_EXECUTION_FINISHED_AT: 2026-09-29T17:34:15.5813574+08:00

# 执行记录

状态：Round 3 已处理 FIX-018 和 TODO-002；锁定 SDK 8.0.424 验证仍受环境阻碍。原始实施开始时间为 2026-09-29T00:07:34.420Z。

## Change Impact Analysis

- ChangedFiles: `DependencyModule.cs`、Core 直接测试、AspectCore 循环探针与测试项目、资源探针、专项指南和追溯文档。
- ChangedProjects: Bing.Core、Bing.Core.Tests、Bing.Aop.AspectCore.Tests；MVC 与 WebApi 属受影响消费者。
- ChangedPublicContracts: 无。
- ChangedRuntimePaths: 约定 DI 注册、容器解析、模块和插件资源生命周期。
- ChangedProviders: Microsoft DI、AspectCore。
- ChangedTFMs: 无。
- ChangedBuildPackaging: AspectCore 测试项目将独立循环探针的当前构建产物复制到测试输出目录；生产包配置不变。
- ChangedBenchmarkHarness: 新增资源探针，不设置未批准的内存阈值。
- ChangedDocsOnly: 使用指南及本记录。
- AffectedDependents: Bing.AspNetCore.Mvc.Tests、WebApi 示例。
- RiskLevel: HIGH。

## Findings

| ID | 状态 | 说明 |
| --- | --- | --- |
| DI-001 | CLOSED | 排除 `IAsyncDisposable` 业务接口扫描，直接测试通过。 |
| DI-002 | CLOSED | 修复 `TryAdd` 主接口被跳过时次接口转发错误；多接口释放限制已有直接证据。 |
| DI-003 | CLOSED | 构造、工厂、Lazy、作用域、真实 AspectCore 属性注入和危险循环子进程均有直接测试。 |
| RES-001 | CLOSED | 正常、失败、取消、插件换代探针与不响应取消的活动调用测试通过。 |
| RES-002 | CLOSED | AspectCore net6 退出时并发同步释放抛错，改为等待已有关闭任务，Core 直接测试与 AspectCore 宿主验证通过。 |
| DI-004 | CLOSED | 后续替换主接口使次接口静默解析错误实现；现明确拒绝并报告实现和接口。 |
| FIX-001 | CLOSED | 开放泛型多接口共享在写入任何描述符前报明确错误；单接口开放泛型仍可解析。 |
| FIX-002 | CLOSED | Transient 的显式 TryAdd/ReplaceExisting 生效，补充 AddSelf、异步双接口和同步/异步释放次数测试。 |
| FIX-003 | CLOSED | 集合环、跨作用域缓存、真实工厂递归和构造期间 Lazy.Value 环均有实际容器测试；危险路径在有界子进程。 |
| FIX-004 | CLOSED | 模块自身同步/异步释放抛错后继续逆序清理，保留原始启动错误；并发异步关闭及活动插件调用退出后的旧 ALC 回收有直接测试。 |
| FIX-005 | CLOSED | 100/100 测量样本弱引用回收；循环在同步边界等待异步操作，不额外构造收尾代；指标快照先于测试强制 GC。 |
| FIX-006 | CLOSED | 独立循环探针按当前构建配置及 TFM 复制与启动；Debug/Release × net8/net6 定向通过。 |
| FIX-007 | CLOSED | 安全注册示例、直接消费测试、容器所有权差异、映射和本报告已更新。 |
| FIX-008 | CLOSED | 开放泛型多接口预检按实际 TryAdd/ReplaceExisting 状态判断，合法 TryAdd 场景保留次接口注册，危险共享工厂在写入前失败。Core 定向测试通过。 |
| FIX-009 | CLOSED | 资源探针移除额外换代，加入初始化回调中途取消用例；末代插件上下文使用有界 GC 重试确认回收。Core net8/net6 定向通过。 |
| FIX-010 | CLOSED | 跨作用域缓存测试实际走 `LazyGetService` 并断言缓存实例边界；新增缓存弱引用和同一 Lazy 重入测试。Core net8/net6 定向通过。 |
| FIX-011 | CLOSED | 循环探针加入不启用 AspectCore 的普通 Microsoft DI 工厂递归及同一 Lazy 实例路径，net8/net6 定向通过。 |
| FIX-012 | CLOSED | 增加 Singleton/Scoped 开放泛型 TryAdd 的三接口拒绝、全接口已存在和单接口待注册测试；Core DI 定向 net8/net6 各 42/42 通过。 |
| FIX-013 | CLOSED | 工厂缓存测试断言原作用域实例、两作用域隔离及每个实例恰好释放一次，并保留缓存/服务弱引用回收验证；Core DI 定向 net8/net6 各 42/42 通过。 |
| FIX-014 | CLOSED | 循环探针通过 `DependencyModule.AddServices` 使用真实 Bing `Lazier<T>`，构造器访问 `Value` 并在子进程内有界失败；Debug/Release × net8/net6 七模式各 7/7 通过。 |
| FIX-015 | CLOSED | 运行 net8.0 资源探针并保存 TRX 与数值摘要，包含 20 次预热、100 次测量、四种场景指标和弱引用/释放断言。 |
| FIX-016 | CLOSED | 预检逐接口模拟 TryAdd/ReplaceExisting 的共享入口状态，首接口跳过后存在两个待注册接口时在任何描述符写入前失败；三接口 Singleton/Scoped 回归通过。 |
| FIX-017 | CLOSED | 补齐生产符号到新增测试、factory 缓存和 `bing-lazy` 七模式的映射，并区分历史全量证据与本轮定向结果。 |
| FIX-018 | CLOSED | 增加开放泛型 `MultipleDependency` 首接口拒绝及中间接口不建立共享入口的 Singleton/Scoped 回归；Core 定向 net8/net6 各 34/34 通过。 |
| TODO-002 | CLOSED | 修正 Round 2 验证范围表述，补记当前候选 WebApi 构建，并更新新增测试映射。 |

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX；执行范围：`recommended`，处理全部 MUST_FIX/SHOULD_FIX。
- 修复文件：`DependencyRegistrationBehaviorTest.cs`、`LazyServiceProviderTest.cs`、`CycleProbe/Program.cs`、`AutowiredCycleProcessTest.cs`、循环探针项目文件及本执行记录。
- 验证结果：Core 相关定向测试 net8.0/net6.0 各 50/50；AspectCore 循环探针 Debug 与 Release 的 net8.0/net6.0 各 7/7；资源探针 net8.0 5/5。
- 资源证据：[`resource-probe-net8.txt`](artifacts/resource-probe-net8.txt) 和 [`resource-probe-net8.trx`](artifacts/resource-probe-net8.trx)。
- 未修改 `review.md`，保留其作为独立审查证据；本记录只记录实现与验证结果。

### Round 2

- Review 状态：NEEDS_FIX；执行范围：`recommended`，处理 FIX-012、FIX-013、FIX-016、FIX-017。
- FIX-016：修改 `DependencyModule.cs`，使开放泛型预检按实际注册循环跟踪共享接口；新增三接口 Singleton/Scoped TryAdd 回归，验证拒绝前服务集合不变，并覆盖全接口已存在和仅一个接口待注册的合法路径。
- FIX-012：失败断言改为真实 TryAdd 场景，覆盖两个待注册接口；保留 Scoped 合法分支验证。
- FIX-013：`ScopedFactoryDisposable` 改为线程安全释放计数器，断言原作用域对象、关闭后缓存命中不重复释放、第二作用域独立释放一次。
- FIX-017：更新 `test-mapping.md` 及本执行记录；历史全量计数标记为复用证据，Round 2 同时记录了受影响项目的全量回归与新增定向测试。
- 验证结果：Core DI/Lazy 定向 net8.0、net6.0 各 42/42，Core 完整各 606/606，AspectCore 各 45/45，MVC 各 58 通过、1 跳过；构建警告为仓库既有 nullable XML、过时 API 和 ASP.NET 分析器警告。
- `task-state.mjs review-fix` 尝试更新运行状态时因 `.agents/runtime/current-task.json` 写入 EPERM 失败；后续 `task-finish.mjs` 因该状态文件不存在未能收口，已手工维护本执行记录状态；未执行 commit、push 或 PR。

### Round 3

- Review 状态：NEEDS_FIX；Fix Scope：recommended；Review 文件：`ai_docs/tasks/BING-MODULARITY-DI-RESOURCE-20260929/review.md`。
- FIX-018：在 `DependencyRegistrationBehaviorTest.cs` 增加开放泛型首接口 `MultipleDependency` 的 Singleton/Scoped 注册前拒绝测试，以及 TryAdd 跳过首接口后中间 Multiple 接口不建立共享入口的 Singleton/Scoped 合法注册测试；拒绝路径断言服务集合未发生写入，合法路径断言没有工厂描述符且两个待注册接口均解析到当前实现。
- TODO-002：更新 `test-mapping.md`，修正 Round 2 的执行范围描述，并补记当前候选 WebApi 构建结果。
- 验证结果：`DependencyRegistrationBehaviorTest` 在 net8.0、net6.0 各 34/34 通过；WebApi 示例在 SDK 8.0.425 下构建成功，0 错误、1 个 net6.0 停止支持警告；`git diff --check` 通过。
- `task-state.mjs review-fix` 本轮仍因 `.agents/runtime/current-task.json` 写入 EPERM 失败；未执行 commit、push 或 PR。执行状态按脚本失败原因手工记录，下一步交由独立 Review。

## 已确认问题与边界

- 已修复：异步释放接口误注册；`TryAdd` 主接口未注册时次接口指向其他实现；后续替换主接口时次接口静默解析错误实现；异步关闭期间同步释放抛错。
- 本轮新增：开放泛型多接口共享约定注册提前拒绝，避免默认容器的无效工厂描述符；Transient 的显式 TryAdd/ReplaceExisting 标记按预期处理。开放泛型多接口共享仍是 ACCEPTED_LIMITATION，按指南手工注册适配器。
- ACCEPTED_LIMITATION：Microsoft DI 会对两个接口工厂返回的同一 `IDisposable` 实例调用两次 `Dispose`。保留现有跨接口同一实例契约，提供单一资源所有者、非释放适配器或调用方拥有实例的注册方式。
- ACCEPTED_LIMITATION：真实 AspectCore 2.4 容器注入属性及继承属性，但不注入标记字段；缺失属性服务留下 `null`。属性自环和构造/属性混合环在受控子进程中均导致栈溢出，需避免形成这种依赖结构。
- ACCEPTED_LIMITATION：工厂委托、动态解析和属性依赖不受原生构建期校验完整覆盖。`Lazy<T>` 只能延迟解析。
- VERIFIED_BOUNDARY：插件旧代存在不响应取消的活动调用时，`ReloadAsync` 继续等待、`Status.Retiring` 保留旧代；调用退出后完成关闭，并在同一直接测试中确认旧 ALC 弱引用可回收。强杀进程和断电不保证托管清理。

## 资源探针结果

实际运行时：SDK 8.0.425、测试 TFM net8.0。模块场景预热 20 次并取得 100 个弱引用样本，插件更新取得 100 个连续退役代样本；循环在同步边界等待每次异步操作完成，不额外运行收尾代。快照在测试强制 GC 之前，弱引用断言在有界强制 GC 重试之后。另有初始化回调中途取消的直接释放测试。没有批准的内存阈值。

| 场景 | 当前结果 |
| --- | --- |
| 正常关闭、初始化失败、预取消 | net8.0/net6.0 定向通过；各 100/100 弱引用目标回收，释放计数 120/120。 |
| 初始化回调中途取消 | 定向通过；拥有模块释放计数 1/1，原始取消异常保留。 |
| 插件更新 | net8.0/net6.0 定向通过；100/100 退役上下文弱引用在有界 GC 重试后回收。 |

Round 1 的数值证据保存在 [`resource-probe-net8.txt`](artifacts/resource-probe-net8.txt)，原始测试输出保存在 [`resource-probe-net8.trx`](artifacts/resource-probe-net8.trx)。Round 2 未改变资源探针实现，复用该证据；以下快照来自同一次 net8.0 运行，没有批准的内存阈值；分配量、托管堆和工作集仅用于复核，不能单独证明泄漏或无泄漏。

| 场景 | 分配字节 | 托管堆前 → 后 | Gen0/1/2 | 工作集前 → 后 |
| --- | ---: | ---: | ---: | ---: |
| Normal | 12,226,976 | 5,101,920 → 17,375,384 | 0/0/0 | 90,931,200 → 94,851,072 |
| Failure | 12,066,408 | 5,136,024 → 17,246,176 | 0/0/0 | 94,965,760 → 95,150,080 |
| Canceled | 9,457,280 | 5,114,272 → 14,605,152 | 0/0/0 | 91,045,888 → 91,205,632 |
| PluginReload | 48,506,512 | 5,128,072 → 11,506,560 | 4/1/1 | 79,192,064 → 97,591,296 |

模块正常/失败/预取消场景的释放次数均为 120/120，初始化中途取消场景的拥有模块释放次数为 1/1；100/100 个测量样本的服务集合、容器、模块及退役插件上下文弱引用均已回收。末次插件上下文通过有界 GC 重试确认，不依赖额外插件换代；这不证明任意未来工作负载没有泄漏。快照中的托管堆升高反映强制 GC 前的暂时对象，工作集变化不能单独证明泄漏或无泄漏。既有 `OptionsTypeRegistrationTest` 覆盖配置与选项监视器弱引用回收。

## 验证

| 检查 | 结果 |
| --- | --- |
| Core 全量回归（Round 2） | net8.0/net6.0 各 606/606 通过 |
| AspectCore 全量回归（Round 2） | net8.0/net6.0 各 45/45 通过 |
| MVC 全量回归（Round 2） | net8.0/net6.0 各 58 通过、1 跳过 |
| WebApi 示例（当前候选） | SDK 8.0.425 构建通过，0 错误、1 个 net6.0 停止支持警告 |
| Core DI/Lazy 定向（Round 2） | net8.0、net6.0 各 42/42 通过 |
| 危险循环子进程（Round 1 证据复用） | property/mixed/factory/lazy/plain-factory/plain-lazy/bing-lazy 七条路径，Debug/Release × net8/net6 各 7/7 通过 |
| 资源探针（Round 1 证据复用） | net8.0 5/5 通过；数值摘要及原始 TRX 已保存 |
| SDK 8.0.424 | 环境未安装；实际验证使用 8.0.425，未修改 global.json |

Round 2 的 Core DI/Lazy 定向及 Core、AspectCore、MVC 完整回归均在 net8.0/net6.0 通过；资源和循环探针复用相关源范围未变化的 Round 1 证据。Round 3 对当前候选重新构建 WebApi，0 错误。`git diff --check` 通过。锁定 SDK 8.0.424 仍未安装，实际 SDK 为 8.0.425；未修改 global.json。`task-state.mjs` 与 `task-finish.mjs` 因 `.agents/runtime/current-task.json` 写入/状态缺失未能收口；未执行 git commit、push 或创建 PR。

## 最终状态

- Implementation: 原 6/6、FIX-001 至 FIX-018 均已关闭。
- Release Gates: Core、AspectCore、MVC 双框架、WebApi 与资源探针已通过；SDK 8.0.424 为 BLOCKED_EXTERNAL。
- L0: 受影响项目编译、`git diff --check` 通过。
- L1: DI 注册、容器校验、属性注入、危险循环子进程、并发关闭和资源探针通过。
- L2: Core 各 606、AspectCore 各 45 通过。
- L3: MVC 各 58 通过且各 1 跳过；WebApi 当前候选构建通过。
- L4/L5: 未运行全仓库测试或未批准阈值的性能门禁。
- No-Progress Check: CHANGED，所有当前可执行项已处理。
- Next Action: 交由下一轮独立 review 复核；当前没有新增可执行代码修复项。
- IMPLEMENTATION_STATUS: PASS。
- TEST_STATUS: PASS（Core 完整 net8.0/net6.0 各 606/606；定向和映射检查通过）。
- RESOURCE_EVIDENCE_STATUS: PASS（复用 Round 1 限定探针工件）。
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（锁定 SDK 8.0.424 不可用）。
- RELEASE_STATUS: BLOCKED。
- Goal Status: STOPPED_BLOCKED（代码与本地测试项已完成，锁定 SDK 8.0.424 仍属于外部环境阻塞）。

## 本轮 TODO

- Completed: DI-001、DI-002、DI-003、DI-004、RES-001、RES-002、FIX-001 至 FIX-018，以及 TODO-002。
- Open Actionable: 无。
- Blocked Approval: 无。
- Blocked External: 锁定 SDK 8.0.424 未安装。
- Not Applicable: 强杀进程与断电的托管清理保证。
- Accepted Limitations: 多接口可释放实例重复释放；开放泛型多接口共享须改用手工适配器；AspectCore 字段缺失与属性环；动态解析构建期不可完全检测。
- Verified Boundaries: 不响应取消的活动插件调用不会被提前清理。
- Deferred: 无。
