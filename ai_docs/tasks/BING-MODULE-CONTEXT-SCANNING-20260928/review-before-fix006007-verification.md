<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_REVIEWED_AT: 2026-09-28T12:18:00Z

# 模块能力、可靠性与使用便捷性审查

## 结论

对照用户批准的完整生命周期计划 T1–T5、当前任务 CTX/SCAN/DOC 计划、执行报告、真实源码和直接验证：**没有整组尚未实现的必需能力，但有 2 项新确认的可靠性缺陷需要修复。** 两项均通过独立探针复现，既有回归并未覆盖。不能据此宣称完整计划已经无条件通过验收。

上一份报告中尚未关闭的 FIX-002 已修复：原查找器由注册流程局部变量持有，并在同步/异步 finally 中直接清理，不再查找最后一个服务描述符。本轮双框架模块测试包含追加查找器后的六条成功、异常及异步取消路径，均通过。FIX-001–005 全部 CLOSED。

本轮只修改审查及使用文档；未修改生产代码、正式测试、项目依赖、目标框架或版本。独立探针保存在被忽略的 TestResults 目录。上一份审查见 [历史备份](review-before-capability-audit-20260928.md)。

- Candidate：`4255a969ab54240c25f9ce5ea9c8009b907664f6`；开始审查时 Git 工作区干净。
- IMPLEMENTATION_STATUS: NEEDS_FIX（2 个 OPEN_ACTIONABLE 根因）
- TEST_STATUS: PARTIAL（现有定向回归通过；两个新增场景复现缺陷，尚无正式回归测试）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（已有回收用例通过；真实 Web 清理终态存在缺口）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL（NuGet 在线漏洞数据源 TLS 失败）
- RELEASE_STATUS: NOT_ASSESSED（本轮不是发布验收）
- GOAL_STATUS: STOPPED_REVIEW

## 计划对照

下表按 11 个任务项计为 8 PASS、3 PARTIAL、0 完全未实现；3 个 PARTIAL 来自同两个根因，不代表 3 个独立缺失功能，不折算为主观代码完成百分比。

| 计划项 | 状态 | 实际能力及本轮结论 |
| --- | --- | --- |
| T1 基类同步/异步钩子 | PASS | 配置、初始化 Pre/Main/Post、单阶段关闭；新重写、继承重写、隐藏方法排除、旧接口及 base 桥接已接入 |
| T2 异步注册入口 | PASS | 完整图、注册占用、异步配置、失败不可重试及原查找器确定清理 |
| T3 初始化与失败清理 | PARTIAL | 三轮初始化、作用域、取消、至多一次及逆序清理已实现；真实 Web 只读集合导致清理失败并覆盖启动异常（FIX-006） |
| T4 扫描时机 | PASS | Compatible / BeforeConfigureServices、一次事件、晚选项补扫和 AutoRegisterServices 边界已接入 |
| T5 热更新与宿主 | PARTIAL | 候选初始化成功后切换、旧调用排空、逆序关闭和协作式卸载已实现；延迟私有依赖无法在生命周期中解析（FIX-007），真实 Web 停止还受 FIX-006 影响 |
| CTX-01 配置与环境读取 | PASS | 最后直接单例实例优先，无直接注册才回退 HostBuilderContext，不提前执行工厂 |
| CTX-02 生命周期便捷访问 | PASS | context.GetConfiguration/GetHostEnvironment 及 OrNull 使用当前作用域，支持运行期工厂，无全局缓存 |
| SCAN-01 声明和快照 | PASS | 模块声明、继承、应用附加程序集、只读描述符、自定义 loader 校验、跨代拒绝及冻结 |
| SCAN-02 DI、选项、事件 | PASS | 共享扫描范围；筛选只作用约定 DI；FIX-002 的追加注册保留路径已闭环 |
| SCAN-03 插件解析和回收 | PARTIAL | 显式附加程序集、错误诊断、两代版本及弱引用已有证据；未声明扫描且仅在方法体引用的私有 DLL 仍无法延迟解析（FIX-007） |
| DOC-01 使用说明与映射 | PASS | 原符号映射存在；本轮补全入口选择、完整例子、配置环境、实际扫描范围、异步释放和已知问题 |

原模块依赖图、环检测、失败污染状态、静态兼容入口解绑、启动期插件清单、精确版本/本地区间验证均已有实现。历史计划中的“暂不支持热更新”等范围描述以用户后续批准内容为准，不再计为本期 TODO。

## FIX-006：只读服务集合使 Web 清理失败，并掩盖启动异常

- 状态：OPEN_ACTIONABLE；严重程度：HIGH；处理要求：MUST_FIX；优先级：P1。
- 证据：`BingModuleRegistration.cs:340` 调用 `RemoveOwnedModuleDescriptors`，`:360` 无条件执行 `Services.RemoveAt`。实际 .NET 8 `WebApplicationBuilder.Build()` 后服务集合只读。
- 调用链：`ShutdownBingAsync → BingModuleManager.CleanupAsync → registration.ReleaseAsync → Services.RemoveAt`。异常发生后，插件容器清空、所有权集合清空及 `_runtimeReleased = true` 均未执行。
- 放大路径：`BingModuleManager.cs:257` 的初始化 catch 直接 await 清理，清理抛错后跳过 `:262` 的 Failed 状态写入和原始异常重抛。原始启动异常被只读集合异常替代，后续 DisposeAsync 仍提示正在初始化。

独立探针使用真实 Web 宿主且不监听网络端口：

```csharp
var builder = WebApplication.CreateBuilder(Array.Empty<string>());
await builder.Services.AddBingApplicationAsync<AuditRoot>();
var app = builder.Build();
await app.Services.UseBingAsync();
await app.Services.ShutdownBingAsync();
```

实际结果：`WebServicesReadOnly=True`，关闭抛出 `InvalidOperationException: The service collection cannot be modified because it is read-only.`。

第二个场景让 AuditRoot 的主初始化抛出 `original-startup-error`。实际收到的仍是只读集合异常；随后 DisposeAsync 抛出“请先等待模块初始化完成，再关闭应用”。这证明原始异常和终态都受到影响。

修复目标与要求：

1. 资源所有权释放不以修改宿主服务集合为必要条件；区分可变/只读集合，保留可变独立容器已有的引用清理行为。
2. 将内部终态、插件容器、所有权集合的清理放入确定执行的路径；某项描述符清理失败不能阻止其他内部清理。
3. 启动失败始终保留原始异常，将清理错误附加汇总，确保进入 Failed；不能仅吞掉 RemoveAt 异常。
4. 不解除宿主的只读保护，不创建第二个根容器，不手动释放宿主提供的根容器。

验收：增加真实 net8 WebApplication 的正常关闭、初始化失败、Host 停止、重复异步释放测试；检查原始异常实例/消息、终态、模块释放次数及插件容器清空。保留 net6 Web 与普通可变 ServiceCollection 回归。

## FIX-007：生命周期首次使用私有 DLL 时解析器已退订

- 状态：OPEN_ACTIONABLE；严重程度：HIGH；处理要求：MUST_FIX；优先级：P1。
- 证据：`BingPluginLoader.cs:106` 挂接解析回调，`:138` 只覆盖模块/类型预检，`:157` 在进入配置和初始化前退订。`BingHotPluginHost.cs:214` 使用普通可回收 AssemblyLoadContext，没有代内持久的自定义 Load 实现。
- 触发条件：入口模块只在 `OnApplicationInitialization` 方法体中调用独立 Helper DLL 的静态方法。入口类型的基类、字段与方法签名均不引用 Helper，因此 GetTypes 不会预先解析它。
- 部署已确认：入口 DLL 和 Helper DLL 同在插件目录，Helper 不在宿主输出目录，也未被宿主直接引用。清单和模块注册通过，初始化失败。
- 默认加载上下文和可回收热更新入口在独立进程中分别复现 `FileNotFoundException: Audit.LazyPrivateHelper, Version=1.0.0.0`，模块阶段均为 Initialize。

这不是缺少 DLL 的部署错误，也不应要求所有私有实现依赖都强制进入服务扫描集合。当前主流程只保证已经在类型预检中触发解析的依赖可用。

修复目标与建议：

1. 在仍保持临时解析事件最终退订的前提下，加载实际静态引用的托管依赖闭包，而非只构造目录候选表；沿用程序集身份校验和当前代隔离。
2. 不把目录中的任意 DLL 纳入自动 DI，也不把候选表写入全局静态缓存。
3. 若选择按代自定义加载上下文，需要明确对原“临时解析回调”策略的调整，并保证上下文引用随该代回收；默认入口也须获得对应解决方案。
4. 对字符串动态加载和本机依赖另行明确支持边界，不把静态依赖修复宣传为任意加载能力。

验收：隔离夹具的 Helper 仅被方法体引用，覆盖配置、初始化和运行期服务调用；默认与热更新入口均成功。缺失/身份冲突仍可诊断，失败候选保留旧代；成功旧代及失败候选弱引用可回收，临时回调继续验证退订。

## 漏洞与资源判断

本轮没有确认可远程利用的注入、越权或任意代码执行漏洞。上述两项为已确认的启动/关闭可靠性问题，其中 FIX-006 还造成异常丢失、资源记录未清空和状态无法收口，不能作为“无内存泄漏”的验收结论。

本模块执行的是调用方明确配置的可信插件。程序集加载上下文、模块依赖图和扫描筛选都不是权限沙箱。路径检查不能替代部署目录的写入权限控制；来自不可信上传或可被其他主体替换的 DLL 不属于当前支持边界。未开展全仓库渗透测试。

`dotnet list framework/src/Bing.Core/Bing.Core.csproj package --vulnerable --include-transitive --format json` 以退出码 1 失败：SSL connection could not be established / Authentication failed / 安全包中没有可用的凭证。未得到漏洞清单，因此第三方依赖漏洞状态为 BLOCKED_EXTERNAL，不能把构建和单元测试通过当作依赖安全审计通过。

## 使用便捷性

本轮已实际更新：

- [Core README](../../../framework/src/Bing.Core/README.md)：可直接理解的控制台全流程、入口选择、旧入口兼容、配置/环境访问、自定义加载器注册时机和已知问题链接。
- [可信插件指南](../../../docs/guides/modularity-plugins.md)：来源和清单、部署目录、生命周期、关闭、常见诊断、整组切换、共享契约、排空和切换后异常语义。
- [迁移指南](../../../docs/migrations/modularity-runtime.md)：修正附加扫描边界；释放示例使用 DisposeAsync，避免只异步关闭模块却同步释放含异步服务的容器；补充已知缺陷提示。

另有 2 个 LOW / OPTIONAL 建议，状态均为 DEFERRED，非原计划遗漏，不触发本轮自动代码修复：

- UX-001：为 ReloadAsync 增加切换阶段/是否已切换的结构化诊断。`:131` 切换后 `:139` 旧代 CloseAsync 可能抛错，用户收到异常时新代已生效；Plugins 快照和文档可以辅助判断，但不能表达同版本代际切换。验收：旧代关闭失败时明确可识别 Switched/Retiring 阶段，且新代仍可服务。
- UX-002：提供旧代排空的可观测状态（代标识、活动调用数、退役中状态或诊断事件）。`:138/:200` 当前无限等待回调退出；不建议超时后强制释放仍被使用的实例。验收：受控挂起回调可定位到占用代，释放回调后正常清理。

## 验证证据

Change Impact Analysis：ChangedFiles 为 3 个使用文档和审查报告；生产契约、运行路径、Provider、TFM、包图均未改动。审查风险 MEDIUM/HIGH（宿主所有权和插件依赖），验证采用 L0 + L1 + 隔离 L3 探针；无需 SQL、全解决方案或性能基准。

| 本轮实际验证，SDK 8.0.424 | 结果 |
| --- | --- |
| Core net8.0，FullyQualifiedName~Modularity | 249 passed，0 failed |
| Core net6.0，同一模块范围 | 249 passed，0 failed |
| MVC net8.0，FullyQualifiedName~WebModuleLifecycleTest | 10 passed，0 failed |
| MVC net6.0，同一生命周期范围 | 10 passed，0 failed |
| net8 真实 WebApplication 正常关闭探针 | 复现 FIX-006 |
| net8 真实 WebApplication 初始化失败探针 | 复现 FIX-006 的异常覆盖及状态滞留 |
| 默认入口与热更新入口的私有依赖探针 | 两者均复现 FIX-007 |

TRX 和探针源码：`TestResults/modularity-audit-20260928/`。MVC 最初按目录名 Modularity 过滤时实际匹配 0 项，该两份 TRX 不作为通过证据；已按真实类名改用上述 10 项测试，文件为 mvc-web-lifecycle-net8/net6.trx。Core 文件为 core-modularity-net8/net6.trx。

本轮没有重新运行全量 Core 493、MVC 53+1 skipped 或 WebApi 示例构建；execution.md 中的这些数字是历史结果，不冒充本轮验证。当前两个探针暴露了旧测试没有覆盖的消费者边界。

## 分类 TODO

### Completed

- [x] 复核 T1–T5、CTX-01/02、SCAN-01/02/03 及 DOC-01 的调用链与边界。
- [x] 关闭历史 FIX-002，保留 FIX-001–005 的已修复结论。
- [x] 完成本轮双框架定向回归和两个根因的独立复现。
- [x] 完善使用、迁移和插件文档，注明未修复边界。

### Open Actionable：2 个必须修复的根因

- [ ] FIX-006：只读集合兼容、清理终态及原始异常保护，并补真实 Web 回归。
- [ ] FIX-007：私有依赖方法体延迟加载，并补默认/热更新双路径及卸载回归。

### Optional：2 项易用性建议（DEFERRED）

- [ ] UX-001：热更新切换阶段和结果诊断。
- [ ] UX-002：旧代排空的可观测性。

### Blocked External：1 项证据

- [ ] 恢复 NuGet 漏洞数据源访问后，检查 Core、PluginRuntime 及受影响 Web 消费者的直接/传递依赖；本轮未得到安全审计结果。

### Deferred：4 组后续能力

- [ ] 自定义生命周期阶段/阶段贡献者：当前固定 Pre/Main/Post，计划明确延期。
- [ ] 插件在线分发与多版本求解：当前只有本地清单和已部署版本约束验证。
- [ ] 不可信插件进程隔离：当前插件完全可信，ALC 不构成安全沙箱。
- [ ] 动态 Web 管道替换：已有管道不随整组插件代更新。

Blocked Approval：无。Not Applicable：SQL/数据库、吞吐基准、生产容量、发布动作。Accepted Limitations：默认上下文不可卸载、整组插件代切换、协作式卸载、旧静态入口单宿主。Verified Boundaries：本轮未新增容量边界结论。

No-Progress Check: CHANGED（2 个新根因有真实运行证据）。Next Action: 优先修复 FIX-006，再修复 FIX-007；本轮停在审查和文档交付，不自动进入代码修复。
